using BuzzGUI.Interfaces;
using System.Linq;

namespace BuzzGUI.Common.Actions
{
    public abstract class MachineAction : BuzzAction
    {
        readonly ISong song;
        readonly string machineName;

        protected IMachine Machine
        {
            get
            {
                var m = song.Machines.FirstOrDefault(x => x.Name == machineName);
                if (m == null) throw new ActionException(this);
                return m;
            }
        }

        public MachineAction(IMachine machine)
        {
            song = machine.Graph.Buzz.Song;
            machineName = machine.Name;
        }

    }
}
