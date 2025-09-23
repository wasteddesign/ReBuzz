using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
using System.Linq;

namespace ReBuzz.Core.Actions.GraphActions
{
    internal class DisconnectMachinesAction : BuzzAction
    {
        private readonly string sourceName;
        private readonly string destinationName;
        private readonly int sourceChannel;
        private readonly int destinationChannel;
        private readonly int amp;
        private readonly int pan;

        //private MachineConnectionCore mc;
        private readonly ReBuzzCore buzz;
        private readonly IUiDispatcher dispatcher;

        public DisconnectMachinesAction(ReBuzzCore buzz, IMachineConnection mc, IUiDispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            this.sourceName = mc.Source.Name;
            this.destinationName = mc.Destination.Name;
            this.sourceChannel = mc.SourceChannel;
            this.destinationChannel = mc.DestinationChannel;
            this.amp = mc.Amp;
            this.pan = mc.Pan;
            this.buzz = buzz;
        }

        public DisconnectMachinesAction(ReBuzzCore buzz, IMachine src, IMachine dst, int sourceChannel, int destinationChannel, int amp, int pan, IUiDispatcher dispatcher)
        {
            this.buzz = buzz;
            this.sourceName = src.Name;
            this.destinationName = dst.Name;
            this.sourceChannel = sourceChannel;
            this.destinationChannel = destinationChannel;
            this.amp = amp;
            this.pan = pan;
            this.dispatcher = dispatcher;
        }

        protected override void DoAction()
        {
            lock (ReBuzzCore.AudioLock)
            {
                var machine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == sourceName);
                var mc = machine.AllOutputs.FirstOrDefault(o => o.Destination.Name == destinationName);

                if (machine != null)
                {
                    machine.RemoveOutput(mc);
                }
                machine = mc.Destination as MachineCore;
                if (machine != null)
                {
                    machine.RemoveInput(mc);
                }

                if (!(mc.Source as MachineCore).Hidden && !(mc.Destination as MachineCore).Hidden &&
                    !mc.Source.IsControlMachine)
                {
                    buzz.SongCore.InvokeConnectionRemoved(mc as MachineConnectionCore);
                }

                ((MachineConnectionCore)mc).ClearEvents();
                buzz.UpdateMachineDelayCompensation();
            }
        }

        protected override void UndoAction()
        {
            lock (ReBuzzCore.AudioLock)
            {
                MachineConnectionCore mc = new MachineConnectionCore(dispatcher);
                mc.Source = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == sourceName);
                mc.Destination = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == destinationName);
                mc.SourceChannel = sourceChannel;
                mc.DestinationChannel = destinationChannel;
                mc.Amp = amp;
                mc.Pan = pan;

                var machine = mc.Source as MachineCore;
                if (machine != null)
                {
                    machine.AddOutput(mc);
                }
                machine = mc.Destination as MachineCore;
                if (machine != null)
                {
                    machine.AddInput(mc);
                }

                if (!(mc.Source as MachineCore).Hidden && !(mc.Destination as MachineCore).Hidden)
                {
                    buzz.SongCore.InvokeConnectionAdded(mc);
                }
                buzz.UpdateMachineDelayCompensation();
            }
        }
    }
}
