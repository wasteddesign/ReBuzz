using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuzzGUI.Interfaces
{
    public enum MachineGroupDialog { Rename };
    public interface IMachineGroup : INotifyPropertyChanged
    {
        IMachineGraph Graph { get; }
        string Name { get; set; }
        bool IsGrouped { get; set; }
        Tuple<float, float> Position { get; }

        IEnumerable<IMachine> Machines { get; }

        IMachine MainInputMachine { get; set; }
        IMachine MainOutputMachine { get; set; }

        Tuple<float, float> GetMachinePosition(IMachine machine);

        void ShowDialog(MachineGroupDialog d, int x, int y);
    }
}
