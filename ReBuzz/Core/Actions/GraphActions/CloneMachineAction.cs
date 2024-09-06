using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
using System.Linq;

namespace ReBuzz.Core.Actions.GraphActions
{
    internal class CloneMachineAction : BuzzAction
    {
        readonly MachineInfoRef mi;
        private readonly ReBuzzCore buzz;
        private readonly float newX;
        private readonly float newY;
        private string newName;

        internal CloneMachineAction(ReBuzzCore buzz, IMachine machine, float x, float y)
        {
            this.buzz = buzz;
            this.newX = x;
            this.newY = y;
            mi = new MachineInfoRef(machine as MachineCore);
        }

        protected override void DoAction()
        {
            var clone = buzz.CreateMachine(mi.MachineLib, mi.Instrument, mi.Name, mi.Data, mi.PatternEditorDllName, mi.PatternEditorData, mi.TrackCount, newX, newY);
            this.newName = clone.Name;
        }

        protected override void UndoAction()
        {
            var machine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == this.newName);
            if (machine != null)
            {
                buzz.RemoveMachine(machine);
            }
        }
    }
}
