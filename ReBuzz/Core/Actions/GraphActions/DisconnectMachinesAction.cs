using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using BuzzGUI.Common.Settings;
using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

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
        private readonly EngineSettings engineSettings;

        private readonly List<(int, int)> midiBindings = new();

        public DisconnectMachinesAction(
            ReBuzzCore buzz,
            IMachineConnection mc,
            IUiDispatcher dispatcher,
            EngineSettings engineSettings)
        {
            this.dispatcher = dispatcher;
            this.sourceName = mc.Source.Name;
            this.destinationName = mc.Destination.Name;
            this.sourceChannel = mc.SourceChannel;
            this.destinationChannel = mc.DestinationChannel;
            this.amp = mc.Amp;
            this.pan = mc.Pan;
            this.buzz = buzz;
            this.engineSettings = engineSettings;
        }

        public DisconnectMachinesAction(
            ReBuzzCore buzz,
            IMachine src,
            IMachine dst,
            int sourceChannel,
            int destinationChannel,
            int amp,
            int pan,
            IUiDispatcher dispatcher,
            EngineSettings engineSettings)
        {
            this.buzz = buzz;
            this.sourceName = src.Name;
            this.destinationName = dst.Name;
            this.sourceChannel = sourceChannel;
            this.destinationChannel = destinationChannel;
            this.amp = amp;
            this.pan = pan;
            this.dispatcher = dispatcher;
            this.engineSettings = engineSettings;
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
                    int track = machine.Inputs.IndexOf(mc);
                    var group = machine.ParameterGroupsList[0];
                    foreach (var p in group.ParametersList)
                    {
                        midiBindings.Add(p.GetMIDIBinding(track));
                    }
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
                MachineConnectionCore mc = new MachineConnectionCore(dispatcher, engineSettings);
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
                    int track = machine.Inputs.IndexOf(mc);
                    var group = machine.ParameterGroupsList[0];

                    if (!(mc.Source as MachineCore).Hidden && !(mc.Destination as MachineCore).Hidden)
                    {
                        ParameterGroup.AddInputs(mc, group);
                        buzz.SongCore.InvokeConnectionAdded(mc);

                        for (int i = 0; i < group.ParametersList.Count; i++)
                        {
                            var b = midiBindings[i];
                            if (b.Item1 != -1)
                            {
                                buzz.MidiControllerAssignments.BindParameter(group.ParametersList[i], track, b.Item1, b.Item2);
                            }
                        }

                        midiBindings.Clear();
                    }
                    buzz.UpdateMachineDelayCompensation();
                }
            }
        }
    }
}
