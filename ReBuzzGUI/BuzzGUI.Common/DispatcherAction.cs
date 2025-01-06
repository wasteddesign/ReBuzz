using System;
using System.Windows.Threading;

namespace BuzzGUI.Common
{
    public class DispatcherAction
    {
        DispatcherOperation op;
        readonly Action action;

        public DispatcherAction(Action action)
        {
            this.action = action;
        }

        public void Dispatch(DispatcherPriority priority)
        {
            if (op != null)
            {
                op.Priority = priority;
            }
            else
            {
                op = Dispatcher.CurrentDispatcher.BeginInvoke
                (
                    priority,
                    new Action
                    (
                        delegate
                        {
                            op = null;
                            action();
                        }
                    )
                );
            }
        }
    }
}
