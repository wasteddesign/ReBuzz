using BuzzGUI.Common;
using ReBuzz.Core;
using System.Threading;
using BuzzGUI.Interfaces;

namespace ReBuzz.NativeMachine
{
    internal class ChannelListener
    {
        internal ReBuzzCore buzz;

        public ChannelType Channel { get; }
        private readonly NativeMessage msg;
        Thread threadPing;
        //Task taskPing;

        public EventWaitHandle WaitHandlePing { get; }
        public EventWaitHandle WaitHandlePong { get; }

        public ChannelListener(ChannelType channel, ThreadPriority priority, string eventId, NativeMessage msg, ReBuzzCore buzz)
        {
            this.buzz = buzz;
            Channel = channel;
            this.msg = msg;

            // Message from A is ready when Ping event signaled. Message handled when B send Pong nad channel is available
            WaitHandlePing = new EventWaitHandle(false, EventResetMode.AutoReset, @"Global\Ping" + eventId);
            WaitHandlePong = new EventWaitHandle(false, EventResetMode.AutoReset, @"Global\Pong" + eventId);

            Stopped = true;
            this.priority = priority;
        }

        public void Start()
        {
            Stopped = false;
            threadPing = new Thread(this.ThreadTaskPing);
            threadPing.Priority = priority;
            threadPing.Start();

            //taskPing = Task.Factory.StartNew(ThreadTaskPing, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }

        private void ThreadTaskPing()
        {
            while (!stop)
            {
                WaitHandlePing.WaitOne();
                WaitHandlePing.Reset();
                if (stop)
                    break;

                msg.ReceaveMessage();
                msg.Notify();
            }
            Stopped = true;
        }

        public void Stop()
        {
            stop = true;

            WaitHandlePing.Set();
            WaitHandlePong.Set();
        }

        public void StopAndJoin()
        {
            Stop();

            if (threadPing != null && threadPing.IsAlive)
            {
                threadPing.Join();
            }
            /*
            if (taskPing != null && !taskPing.IsCompleted)
            {
                taskPing.Wait();
            }
            */
            WaitHandlePing.Dispose();
            WaitHandlePong.Dispose();
        }

        internal void WaitHandlePongWaitOne()
        {
            if (!stop)
            {
                WaitHandlePong.WaitOne();
            }
        }

        internal bool WaitHandlePongWaitOne(MachineCore machine, int waitTime)
        {
            if (!WaitHandlePong.WaitOne(waitTime)) // Wait reply. If nothing happens in 2 seconds, consider crashed
            {
                if (machine != null)
                {
                    machine.MachineDLL.IsCrashed = true;
                    machine.Ready = false;
                    Global.Buzz.DCWriteLine(machine.Name + " crashed.");
                }
                return false;
            }
            return true;
        }

        private bool stop = false;
        public bool Stopped { get; set; }

        private readonly ThreadPriority priority;
    }
}
