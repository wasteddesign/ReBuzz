using BuzzGUI.Common;
using ReBuzz.Core;
using ReBuzz.MachineManagement;
using System.Collections.Generic;
using System.Threading;

namespace ReBuzz.Audio
{
    internal class WorkThreadEngine
    {
        // Dispatcher-owned wave gate. Workers only READ generation; they never
        // write the go-signal. (The previous design had workers reset the
        // go-signal handle in GetWorkItem, giving it two writers and no owner.)
        // The dispatcher advances generation in AllJobsAdded to open each wave.
        private readonly object gate = new();
        private long generation;
        private readonly ManualResetEvent allDoneHandle;
        readonly Thread[] threads;
        readonly List<MachineWorkInstance> workList = new List<MachineWorkInstance>();

        public WorkThreadEngine(int numThreads)
        {
            allDoneHandle = new ManualResetEvent(false);
            threads = new Thread[numThreads];
        }

        public void PrepareEngine(int samples)
        {
            nSamples = samples;
        }

        public void Start()
        {
            for (int i = 0; i < threads.Length; i++)
            {
                Thread t = new Thread(() =>
                {
                    // Per-thread: last wave generation this worker has observed.
                    long seen = 0;
                    while (true)
                    {
                        if (stopped)
                            break;

                        // Wait for the dispatcher to open the next wave. The gate is
                        // dispatcher-owned: we only read generation and never write it,
                        // so a lagging worker can no longer clobber the next wave's open.
                        // The predicate re-check under the lock makes a missed Pulse - or
                        // a fully-skipped wave (this worker lagged past one) - safe.
                        lock (gate)
                        {
                            while (!stopped && generation == seen)
                                Monitor.Wait(gate);
                            seen = generation;
                        }

                        // B2 drain: pull ready INDICES until none are runnable, then park.
                        // Parking on an empty ready list is correct even mid-buffer: any
                        // machine still in flight will pulse the gate when it releases a
                        // successor, and the dispatcher's wait is bounded regardless.
                        if (b2Mode)
                        {
                            while (true)
                            {
                                if (stopped)
                                    break;

                                int idx = B2GetReady();
                                if (idx < 0)
                                    break;

                                var bwi = b2Items[idx];
                                try
                                {
                                    bwi.TickAndWork(nSamples, true);
                                }
                                catch (System.Exception ex)
                                {
                                    RecordFault(bwi.Machine?.Name, ex);
                                }
                                finally
                                {
                                    // MUST run for every dequeued index, on every path -
                                    // throw, or TickAndWork's early return when !Ready.
                                    // A machine that never releases its successors deadlocks
                                    // the buffer. A faulted machine counts as completed with
                                    // stale output for this chunk, exactly as wave mode does.
                                    B2WorkDone(idx);
                                }
                            }
                            continue;   // back to the gate
                        }

                        while (true)
                        {
                            if (stopped)
                                break;

                            // Try to get work item
                            var wi = GetWorkItem();
                            if (wi != null)
                            {
                                var machine = wi.Machine;
                                var buzz = Global.Buzz as ReBuzzCore;

                                try
                                {
                                    wi.TickAndWork(nSamples, true);
                                }
                                catch (System.Exception ex)
                                {
                                    // Contain a single machine's failure: an exception thrown
                                    // from Work() must not tear down the whole audio engine
                                    // (otherwise it surfaces as an unhandled exception on this
                                    // worker thread and the process is killed). Record the
                                    // culprit for diagnosis and carry on - the machine's output
                                    // is stale for this chunk only.
                                    RecordFault(machine?.Name, ex);
                                }
                                finally
                                {
                                    // Always balance the job counter, even on throw: a leaked
                                    // counter would leave allDoneHandle unset and hang the
                                    // per-wave barrier.
                                    WorkDone();
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                });
                t.IsBackground = true;
                t.Priority = ProcessAndThreadProfile.WorkThreadEngineThread;
                threads[i] = t;
                t.Start();
            }
        }

        private readonly Lock workLock = new();

        public void AddWork(MachineWorkInstance workItem)
        {
            lock (workLock)
            {
                if (!stopped)
                {
                    workCounter++;
                    workList.Add(workItem);
                    allDoneHandle.Reset();
                }
            }
        }

        // Install the dependency graph. TOPOLOGY-time only, never per buffer.
        // succ carries one entry per EDGE (parallel/multi-channel connections repeat), so
        // each successor's countdown is decremented exactly as many times as indeg counted.
        internal void B2SetGraph(MachineWorkInstance[] items, int[] indeg, int[][] succ)
        {
            int n = items.Length;
            int total = 0;
            for (int i = 0; i < n; i++)
                total += succ[i].Length;

            var flat = new int[total];
            var starts = new int[n];
            var counts = new int[n];
            int at = 0;
            for (int i = 0; i < n; i++)
            {
                starts[i] = at;
                counts[i] = succ[i].Length;
                for (int j = 0; j < succ[i].Length; j++)
                    flat[at++] = succ[i][j];
            }

            lock (workLock)
            {
                b2Items = items;
                b2Indeg0 = indeg;
                b2Remaining = new int[n];
                b2SuccFlat = flat;
                b2SuccStart = starts;
                b2SuccCount = counts;
                b2Mode = true;
            }
        }

        // Diagnostic for the dispatcher's bounded wait: which machines never completed,
        // and what their countdowns were stuck at. This is the difference between
        // "the app froze" and "machine X is waiting on 1 input that never arrived".
        internal string B2DescribePending()
        {
            var sb = new System.Text.StringBuilder();
            lock (workLock)
            {
                sb.Append("pending=").Append(b2Pending)
                  .Append(" ready=").Append(b2Ready.Count).Append(" stuck=[");
                int shown = 0;
                for (int i = 0; i < b2Remaining.Length && shown < 8; i++)
                {
                    if (b2Remaining[i] > 0)
                    {
                        if (shown > 0)
                            sb.Append(", ");
                        sb.Append(b2Items[i].Machine?.Name ?? "(null)")
                          .Append(" waiting on ").Append(b2Remaining[i]);
                        shown++;
                    }
                }
                sb.Append("]");
            }
            return sb.ToString();
        }

        internal void B2Disable()
        {
            lock (workLock)
            {
                b2Mode = false;
            }
        }

        // Per-buffer reset + seed. Called with the workers parked, same contract as
        // AddWork. Returns false if there is nothing to do (caller must not wait).
        internal bool B2StartBuffer(int[] seed, int samples)
        {
            nSamples = samples;
            lock (workLock)
            {
                if (stopped)
                    return false;

                System.Array.Copy(b2Indeg0, b2Remaining, b2Indeg0.Length);
                b2Pending = b2Indeg0.Length;
                b2Ready.Clear();
                allDoneHandle.Reset();

                if (b2Pending == 0)
                    return false;   // empty cone: nothing to wait for

                for (int i = 0; i < seed.Length; i++)
                    b2Ready.Add(seed[i]);

                return true;
            }
        }

        // Pop a ready cone index, or -1 if none are currently runnable. -1 does NOT mean
        // the buffer is finished: successors may still be released by in-flight machines.
        private int B2GetReady()
        {
            lock (workLock)
            {
                if (stopped || b2Ready.Count == 0)
                    return -1;
                int last = b2Ready.Count - 1;
                int idx = b2Ready[last];
                b2Ready.RemoveAt(last);
                return idx;
            }
        }

        internal void AllJobsAdded()
        {
            // Open the wave: advance the generation and wake the workers. This is the
            // ONLY writer of the go-signal, and it is the dispatcher. Workers observe
            // the new generation under gate and start draining workList.
            lock (gate)
            {
                generation++;
                Monitor.PulseAll(gate);
            }
        }

        // Release idx's successors, then account for the buffer. All of it under workLock
        // so the countdown, the enqueue and the pending count move atomically together:
        // a successor cannot be enqueued twice, and allDoneHandle cannot be set while
        // newly-ready work is still unqueued.
        private void B2WorkDone(int idx)
        {
            bool addedWork;
            bool finished;

            lock (workLock)
            {
                addedWork = false;
                int start = b2SuccStart[idx];
                int count = b2SuccCount[idx];
                for (int k = 0; k < count; k++)
                {
                    int s = b2SuccFlat[start + k];
                    if (--b2Remaining[s] == 0)
                    {
                        b2Ready.Add(s);
                        addedWork = true;
                    }
                }

                b2Pending--;
                finished = (b2Pending == 0);

                // Completion is b2Pending, never "ready list is empty": the list empties
                // transiently mid-buffer whenever every runnable machine is in flight and
                // none has released its successors yet. Only b2Pending tracks real progress.
                if (finished)
                    allDoneHandle.Set();
            }

            // The wake flag is a LOCAL, deliberately. A shared field would let two workers
            // completing concurrently race on it and lose a wake, stranding ready work with
            // every worker parked - a hung audio thread. Pulsing more often than strictly
            // necessary is harmless: workers simply re-check the ready list.
            if (addedWork && !finished)
            {
                lock (gate)
                {
                    generation++;
                    Monitor.PulseAll(gate);
                }
            }
        }

        private void WorkDone()
        {
            lock (workLock)
            {
                workCounter--;
                if (workCounter == 0)
                {
                    allDoneHandle.Set();
                }
            }
        }

        public MachineWorkInstance GetWorkItem()
        {
            lock (workLock)
            {
                if (workList.Count > 0 && !stopped)
                {
                    // Pop the tail in O(1). Remove(Last()) linear-scans to find an
                    // item already at the end; RemoveAt(Count-1) removes the same
                    // element (each machine is queued once per wave) without the scan,
                    // shortening the time workLock is held.
                    int last = workList.Count - 1;
                    var wi = workList[last];
                    workList.RemoveAt(last);
                    return wi;
                }
                else
                {
                    // Empty: just report "no work". Workers no longer reset the
                    // go-signal here (that shared ownership was the hazard); they fall
                    // out of the drain loop and re-block on the dispatcher-owned gate.
                    return null;
                }
            }
        }

        private bool stopped;
        private int workCounter;
        private int nSamples;

        // ==== B2 (#107): dependency-driven dispatch ===============================
        // Wave mode: the dispatcher adds a whole DAG level, opens the gate, waits.
        // Workers only POP. One barrier per level, so the buffer costs O(depth) barriers.
        //
        // B2 mode: the dispatcher seeds only the in-degree-zero machines, opens the gate
        // ONCE and waits ONCE for the whole buffer. Workers also PUSH: on finishing
        // machine i they decrement each successor's countdown and enqueue any that hit
        // zero. A machine runs the moment ITS OWN inputs are done, not when its level is.
        //
        // Bit-identity: a machine still runs only after ALL its inputs have completed and
        // sums them in connection-list order regardless of sibling timing - the same
        // argument that makes wave mode order-independent.
        //
        // THE INVARIANT THAT MATTERS: every cone machine must complete EXACTLY ONCE.
        // Complete twice and a successor is enqueued twice; complete zero times and its
        // successors' countdowns never reach zero - either way the audio thread HANGS.
        // It holds by construction here:
        //   * an index enters b2Ready only when its countdown transitions to exactly 0,
        //     under workLock, and a countdown reaches 0 once (it only ever decrements);
        //   * an index is dequeued once (popped under workLock, never re-added);
        //   * B2WorkDone runs in the worker's finally, so it fires even if TickAndWork
        //     throws OR early-returns (it returns immediately when !Machine.Ready).
        // The dispatcher additionally bounds its wait, so any residual stall surfaces as
        // a log line rather than a frozen app.
        private bool b2Mode;
        private int[] b2Indeg0;                  // pristine in-degrees (topology-time)
        private int[] b2Remaining;               // live countdown, reset per buffer
        private int[] b2SuccFlat;                // successors, CSR-flattened (alloc-free)
        private int[] b2SuccStart;
        private int[] b2SuccCount;
        private MachineWorkInstance[] b2Items;   // cone index -> work item
        private readonly List<int> b2Ready = new List<int>();  // ready INDICES, not items
        private int b2Pending;                   // cone machines not yet completed

        // Diagnostics for contained Work() faults (see the worker loop above).
        // Written only on the exceptional path, so there is no steady-state cost;
        // inspect via a debugger or reflection when a fault is suspected.
        internal static int WorkFaultCount;
        internal static string LastFaultMachine = "(none)";
        internal static string LastFaultMessage = "";

        private static void RecordFault(string machineName, System.Exception ex)
        {
            Interlocked.Increment(ref WorkFaultCount);
            LastFaultMachine = machineName ?? "(null)";
            LastFaultMessage = ex.GetType().Name + ": " + ex.Message;
        }

        public void Stop()
        {
            lock (workLock)
            {
                stopped = true;
            }
            // Wake any workers parked on the gate so they observe stopped and exit.
            lock (gate)
            {
                generation++;
                Monitor.PulseAll(gate);
            }

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }
            allDoneHandle.Set();
        }

        public ManualResetEvent AllDoneEvent()
        {
            return allDoneHandle;
        }
    }
}
