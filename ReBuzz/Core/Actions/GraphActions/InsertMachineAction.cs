using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using BuzzGUI.Common.Settings;
using BuzzGUI.Interfaces;
using System.Linq;

namespace ReBuzz.Core.Actions.GraphActions
{
    internal class InsertMachineAction : BuzzAction
    {
        readonly ConnectionInfoRef oc;
        private readonly float x;
        private readonly float y;
        private readonly int id;
        private readonly ReBuzzCore buzz;
        private string name;
        private readonly string machineLib;
        private readonly string instrument;
        private readonly IUiDispatcher dispatcher;
        private readonly EngineSettings engineSettings;

        public InsertMachineAction(
            ReBuzzCore buzzCore,
            IMachineConnection m,
            string machineLib,
            string instrument,
            float x,
            float y,
            IUiDispatcher dispatcher,
            EngineSettings engineSettings)
        {
            this.engineSettings = engineSettings;
            this.dispatcher = dispatcher;
            id = -1;
            oc = new ConnectionInfoRef(m);
            this.buzz = buzzCore;
            this.machineLib = machineLib;
            this.instrument = instrument;
            this.x = x;
            this.y = y;
        }

        internal InsertMachineAction(
            ReBuzzCore buzz,
            IMachineConnection m,
            int id,
            float x,
            float y,
            IUiDispatcher dispatcher,
            EngineSettings engineSettings)
        {
            this.engineSettings = engineSettings;
            this.dispatcher = dispatcher;
            oc = new ConnectionInfoRef(m);
            this.x = x;
            this.y = y;
            this.id = id;
            this.buzz = buzz;
        }

        protected override void DoAction()
        {
            MachineCore machine;
            if (id >= 0)
            {
                machine = buzz.CreateMachine(id, x, y);
            }
            else
            {
                machine = buzz.CreateMachine(machineLib, instrument, null, null, null, null, -1, x, y);
            }

            // Remove original connection
            var source = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == oc.Source);
            var destination = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == oc.Destination);

            if (machine != null && source != null && destination != null)
            {
                this.name = machine.Name;
                var mc = source.Outputs.FirstOrDefault(o => o.Destination == destination);
                new DisconnectMachinesAction(buzz, mc, dispatcher, engineSettings).Do();

                MachineConnectionCore c = new MachineConnectionCore(machine, 0, destination, 0, 0x4000, 0x4000, dispatcher, engineSettings);
                new ConnectMachinesAction(buzz, c, dispatcher, engineSettings).Do();

                c = new MachineConnectionCore(source, 0, machine, 0, oc.Amp, oc.Pan, dispatcher, engineSettings);
                new ConnectMachinesAction(buzz, c, dispatcher, engineSettings).Do();
            }
        }

        protected override void UndoAction()
        {
            var machine = buzz.SongCore.MachinesList.FirstOrDefault(x => x.Name == name);

            var source = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == oc.Source);
            var destination = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == oc.Destination);

            var mc = source.Outputs.FirstOrDefault(x => x.Destination == machine);
            new DisconnectMachinesAction(buzz, mc, dispatcher, engineSettings).Do();

            mc = machine.Outputs.FirstOrDefault(x => x.Destination == destination);
            new DisconnectMachinesAction(buzz, mc, dispatcher, engineSettings).Do();

            if (machine != null)
            {
                buzz.RemoveMachine(machine);
            }

            // Restore original connection
            MachineConnectionCore mcc = new MachineConnectionCore(source, oc.SourceChannel, destination, oc.DestinationChannel, oc.Amp, oc.Pan, dispatcher, engineSettings);
            new ConnectMachinesAction(buzz, mcc, dispatcher, engineSettings).Do();
        }
    }
}
