using BuzzGUI.Common;
using ReBuzz.Core;
using ReBuzz.MachineManagement;
using System.Collections.Generic;
using System.Linq;
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

                                wi.TickAndWork(nSamples, true);

                                WorkDone();
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
                    var wi = workList.Last();
                    workList.Remove(wi);
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
