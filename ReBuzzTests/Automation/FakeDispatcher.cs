using System;
using System.Windows.Threading;
using ReBuzz.Core;

namespace ReBuzzTests.Automation
{
    public class FakeDispatcher : IUiDispatcher
    {
        public void Invoke(Action action)
        {
            action();
        }

        public void BeginInvoke(Action action)
        {
            action();
        }

        public void BeginInvoke(Action action, DispatcherPriority priority)
        {
            // this is a little stretch, but BeginInvoke does not guarantee when
            // the action will be executed, so might as well get executed immediately.
            action();
        }
    }
}