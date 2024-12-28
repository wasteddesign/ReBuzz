using BuzzGUI.Interfaces;
using System;

namespace BuzzGUI.Common
{
    public class SimpleAction<U> : IAction
    {
        public U NewData;
        public U OldData;

        public Action<SimpleAction<U>> DoDelegate { get; set; }
        public Action<SimpleAction<U>> UndoDelegate { get; set; }

        public void Do()
        {
            DoDelegate(this);
        }

        public void Undo()
        {
            UndoDelegate(this);
        }

    }

    public class SimpleAction<T, U> : IAction
    {
        public T Target;
        public U NewData;
        public U OldData;

        public Action<SimpleAction<T, U>> DoDelegate { get; set; }
        public Action<SimpleAction<T, U>> UndoDelegate { get; set; }

        public void Do()
        {
            DoDelegate(this);
        }

        public void Undo()
        {
            UndoDelegate(this);
        }

    }
}
