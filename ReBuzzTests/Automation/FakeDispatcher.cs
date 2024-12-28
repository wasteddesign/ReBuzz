using System;
using System.Windows.Threading;
using ReBuzz.Core;

namespace ReBuzzTests.Automation
{
    /// <summary>
    /// A test-only dispatcher that does not depend on the System.Windows.
    /// More suitable for tests as there can only be one Application object in a process,
    /// and we create a new instance of ReBuzzCore (which closes the Application on exit) for each test.
    /// Also, not depending on the Application.Current.Dispatcher removes the necessity to declare
    /// the tests as [STAThread].
    /// </summary>
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