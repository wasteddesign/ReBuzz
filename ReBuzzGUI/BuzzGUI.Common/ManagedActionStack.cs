using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BuzzGUI.Common
{
    public class ManagedActionStack : IActionStack
    {
        class ActionRef
        {
            public IAction Action { get; set; }
            public int Group { get; set; }
        }

        readonly List<ActionRef> actions = new List<ActionRef>();
        int position;

        int groupCounter;
        int groupDepth;

        public IEnumerable<IAction> Actions { get { return actions.Select(a => a.Action); } }

        public ManagedActionStack()
        {
            MaxNumberOfActions = int.MaxValue;
        }

        public ManagedActionStack(int maxNumberOfActions)
        {
            MaxNumberOfActions = maxNumberOfActions;
        }

        public void Do(IAction a)
        {
            if (groupDepth == 0) groupCounter++;

            a.Do();

            while (actions.Count > position)
                actions.RemoveAt(actions.Count - 1);

            actions.Add(new ActionRef() { Action = a, Group = groupCounter });
            position++;

            if (PostDoOrUndoGroup != null)
                PostDoOrUndoGroup(new[] { a }, true);

            if (actions.Count > MaxNumberOfActions && actions.Count > 0)
            {
                actions.RemoveAt(0);
                if (position > 0)
                    position--;
            }
        }

        public bool CanUndo { get { return position > 0; } }
        public bool CanRedo { get { return actions.Count > 0 && position < actions.Count; } }

        public int MaxNumberOfActions { get; private set; }

        public void Undo()
        {
            if (position < 1) return;

            int g = actions[position - 1].Group;
            int startpos = position - 1;

            do
            {
                actions[position - 1].Action.Undo();
                position--;
            } while (position >= 1 && actions[position - 1].Group == g);

            if (PostDoOrUndoGroup != null)
                PostDoOrUndoGroup(actions.Skip(position).Take(startpos - position + 1).Reverse().Select(a => a.Action), false);

        }

        public void Redo()
        {
            if (position >= actions.Count) return;

            int g = actions[position].Group;
            int startpos = position;

            do
            {
                actions[position].Action.Do();
                position++;
            } while (position < actions.Count && actions[position].Group == g);

            if (PostDoOrUndoGroup != null)
                PostDoOrUndoGroup(actions.Skip(startpos).Take(position - startpos).Select(a => a.Action), true);
        }

        public void BeginActionGroup()
        {
            if (groupDepth == 0)
                groupCounter++;

            groupDepth++;
        }

        public void EndActionGroup()
        {
            groupDepth--;

            if (groupDepth <= 0)
                groupDepth = 0;
        }

        public event Action<IEnumerable<IAction>, bool> PostDoOrUndoGroup;

    }
}
