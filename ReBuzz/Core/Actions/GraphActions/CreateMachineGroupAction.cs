using BuzzGUI.Common.Actions;
using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace ReBuzz.Core.Actions.GraphActions
{
    internal class CreateMachineGroupAction : BuzzAction
    {
        private readonly ReBuzzCore buzz;

        private readonly float x;
        private readonly float y;
        private readonly string name;
        private string realName;
        private Dictionary<string, Tuple<float, float>> machinePos = new Dictionary<string, Tuple<float, float>>();

        readonly Dictionary<int, IEnumerable<KeyValuePair<int, SequenceEventRef>>> sequences = new Dictionary<int, IEnumerable<KeyValuePair<int, SequenceEventRef>>>();
        private string actualName;

        public CreateMachineGroupAction(ReBuzzCore buzz, string name, float x, float y)
        {
            this.buzz = buzz;
            this.name = name;
            this.x = x;
            this.y = y;
        }

        protected override void DoAction()
        {
            var song = buzz.SongCore;
            realName = song.GetNewGroupName(name);

            MachineGroupCore group = buzz.CreateMachineGroup(realName, x, y);

            foreach (var kv in machinePos)
            {
                var machine = song.Machines.FirstOrDefault(m => m.Name == kv.Key);
                if (machine != null)
                {
                    buzz.SongCore.AddMachineToGroup(machine, group);
                    buzz.SongCore.UpdateGroupedMachinesPositions([new Tuple<IMachine, Tuple<float, float>>(machine, kv.Value)]);
                    buzz.SongCore.InvokeImportGroupedMachinePositions(machine, kv.Value.Item1, kv.Value.Item2);
                }
            }
        }

        protected override void UndoAction()
        {
            var song = buzz.SongCore;
            var group = buzz.SongCore.MachineGroups.FirstOrDefault(g => g.Name == realName) as MachineGroupCore;
            machinePos = group.Machines.ToDictionary(k => k.Name, v => group.GetMachinePosition(v));

            group.machines.Clear();

            song.MachineGroupsList.Remove(group);
            song.InvokeMachineGroupRemoved(group);

            song.RemoveGroupFromDictionary(group);
        }
    }
}
