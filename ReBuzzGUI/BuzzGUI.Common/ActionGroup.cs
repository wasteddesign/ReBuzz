using BuzzGUI.Interfaces;
using System;

namespace BuzzGUI.Common
{
    public class ActionGroup : IDisposable
    {
        readonly IActionStack actionStack;

        public ActionGroup(IActionStack a)
        {
            actionStack = a;
            actionStack.BeginActionGroup();
        }

        public void Dispose()
        {
            actionStack.EndActionGroup();
        }
    }
}
