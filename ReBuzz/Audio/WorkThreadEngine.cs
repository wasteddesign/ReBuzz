using BuzzGUI.Common;
using ReBuzz.Core;
using ReBuzz.MachineManagement;
using System.Collections.Generic;
using System.Threading;

namespace ReBuzz.Audio
{
    internal class WorkThreadEngine
    {
        private readonly ManualResetEvent workWaitHandle;
        private readonly ManualResetEvent allDoneHandle;
        readonly Thread[] threads;
        readonly List<MachineWorkInstance> workList = new List<MachineWorkInstance>();

        public WorkThreadEngine(int numThreads)
        {
            workWaitHandle = new ManualResetEvent(false);
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
                    while (true)
                    {
                        if (stopped)
                            break;

                        // Wait for a job
                        workWaitHandle.WaitOne();

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
            workWaitHandle.Set();
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
                    workWaitHandle.Reset(); // Wait until there are work available;
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
            workWaitHandle.Set();

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
