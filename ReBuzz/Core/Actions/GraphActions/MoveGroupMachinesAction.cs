using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReBuzz.Core.Actions.GraphActions
{
    internal class MoveGroupMachinesAction : BuzzAction
    {
        private readonly ReBuzzCore buzz;
        readonly List<Tuple<string, Tuple<float, float>>> mmOld = new List<Tuple<string, Tuple<float, float>>>();
        readonly List<Tuple<string, Tuple<float, float>>> mmNew = new List<Tuple<string, Tuple<float, float>>>();

        public MoveGroupMachinesAction(ReBuzzCore buzz, IEnumerable<Tuple<IMachineGroup, Tuple<float, float>>> mm)
        {
            this.buzz = buzz;

            // Save new and old positions
            foreach (var machinePos in mm)
            {
                var machine = machinePos.Item1;
                Tuple<float, float> pos = machinePos.Item2;

                // New
                Tuple<string, Tuple<float, float>> posInfo = new Tuple<string, Tuple<float, float>>(machine.Name, pos);
                mmNew.Add(posInfo);

                // Old
                posInfo = new Tuple<string, Tuple<float, float>>(machine.Name, machine.Position);
                mmOld.Add(posInfo);
            }
        }

        protected override void DoAction()
        {
            foreach (var machineInfo in mmNew)
            {
                MachineGroupCore machine = buzz.SongCore.MachineGroups.FirstOrDefault(g => g.Name == machineInfo.Item1) as MachineGroupCore;
                Tuple<float, float> position = machineInfo.Item2;
                machine.Position = position;
            }
        }

        protected override void UndoAction()
        {
            foreach (var machineInfo in mmOld)
            {
                MachineGroupCore machine = buzz.SongCore.MachineGroups.FirstOrDefault(g => g.Name == machineInfo.Item1) as MachineGroupCore;
                Tuple<float, float> position = machineInfo.Item2;
                machine.Position = position;
            }
        }
    }
}
