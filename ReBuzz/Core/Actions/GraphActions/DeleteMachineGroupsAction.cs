using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace ReBuzz.Core.Actions.GraphActions
{


    internal class DeleteMachineGroupsAction : BuzzAction
    {
        private readonly ReBuzzCore buzz;
        private readonly IEnumerable<string> groups;
        private Dictionary<string, IEnumerable<MachinePos>> machinePos = new Dictionary<string, IEnumerable<MachinePos>>();

        private string actualName;
        private Dictionary<string, Tuple<float,float>> groupPos;

        struct MachinePos
        {
            public string Name;
            public Tuple<float, float> Position;
        }

        public DeleteMachineGroupsAction(ReBuzzCore buzz, IEnumerable<IMachineGroup> g)
        {
            this.buzz = buzz;

            groups = g.Select(s => s.Name);
            groupPos = g.ToDictionary(k => k.Name, v => v.Position);
            machinePos = g.ToDictionary(k => k.Name, v => v.Machines.Select(m => new MachinePos() { Name = m.Name, Position = m.Position }));
        }

        protected override void DoAction()
        {
            var song = buzz.SongCore;
            foreach (var gName in groups)
            {
                var group = buzz.SongCore.MachineGroups.FirstOrDefault(g => g.Name == gName) as MachineGroupCore;

                buzz.RemoveMachineGroup(group);
            }
        }

        protected override void UndoAction()
        {
            var song = buzz.SongCore;

            foreach (var gName in groups)
            {
                var group = new MachineGroupCore(song);
                group.Position = groupPos[gName];
                group.Name = gName;
                song.MachineGroupsList.Add(group);
                song.InvokeMachineGroupAdded(group);

                foreach (var v in machinePos[gName])
                {   
                    
                    var machine = song.Machines.FirstOrDefault(m => m.Name == v.Name);
                    if (machine != null)
                    {
                        buzz.SongCore.AddMachineToGroup(machine, group);
                        buzz.SongCore.UpdateGroupedMachinesPositions([new Tuple<IMachine, Tuple<float, float>>(machine, v.Position)]);
                        buzz.SongCore.InvokeImportGroupedMachinePositions(machine, v.Position.Item1, v.Position.Item2);
                    }
                }
            }
        }
    }
}
