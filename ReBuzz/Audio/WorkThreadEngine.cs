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
