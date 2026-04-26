using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ReBuzz.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ReBuzz.Core
{
    public class MachineGroupCore : IMachineGroup
    {
        public IMachineGraph Graph { get; private set; }

        string name;
        public string Name { get => name; set { name = value; PropertyChanged?.Raise(this, "Name"); } }

        bool isGrouped;
        public bool IsGrouped { get => isGrouped; set { isGrouped = value; PropertyChanged?.Raise(this, "IsGrouped"); } }

        Tuple<float, float> position = new Tuple<float, float>(0, 0);
        public Tuple<float, float> Position { get => position; set { position = value; PropertyChanged?.Raise(this, "Position"); } }

        public List<MachineCore> machines = new List<MachineCore>();
        public IEnumerable<IMachine> Machines => machines;

        public IMachine MainInputMachine { get; set; }
        public IMachine MainOutputMachine { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public MachineGroupCore(IMachineGraph g)
        {
            Graph = g;
        }

        public void ShowDialog(MachineGroupDialog d, int x, int y)
        {
            if (d == MachineGroupDialog.Rename)
            {
                RenameMachineWindow rmw = new RenameMachineWindow("Rename Groups", Name, false);
                rmw.SetStartUpLocation(x, y);
                if (rmw.ShowDialog() == true)
                {
                    var newName = rmw.tbName.Text.Trim();
                    var song = Global.Buzz.Song as SongCore;
                    newName = song.GetNewGroupName(newName);
                    song.RenameMachineGroupUndoable(this, newName);
                }
            }
        }

        public Tuple<float, float> GetMachinePosition(IMachine machine)
        {
            var song = Global.Buzz.Song as SongCore;

            Tuple<float, float> pos = machine.Position;
            if (song.GroupedMachinePositions.ContainsKey(machine))
            {
                pos = song.GroupedMachinePositions[machine];
            }

            return pos;
        }
    }
}
