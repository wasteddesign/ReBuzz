using BuzzGUI.Interfaces;
using System;

namespace BuzzGUI.Common.Actions
{
    public class ActionException : Exception
    {
        readonly IAction action;

        public ActionException(IAction action)
        {
            this.action = action;
        }

        public override string ToString()
        {
            return "ActionException: " + action.ToString();
        }
    }
}
