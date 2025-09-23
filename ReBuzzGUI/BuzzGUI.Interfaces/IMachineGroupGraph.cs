using System.Collections.ObjectModel;
using System.ComponentModel;

namespace BuzzGUI.Interfaces
{
    public interface IMachineGroupGraph : IActionStack, INotifyPropertyChanged
    {
        ReadOnlyCollection<IMachineGroup> MachineGroups { get; }
        public IDictionary<IMachine, IMachineGroup> MachineToGroupDict { get; }

        void CreateMachineGroup(string name, float x, float y);

        void DeleteMachineGroups(IEnumerable<IMachineGroup> mg);
        void AddMachineToGroup(IMachine machine, IMachineGroup machineGroup);
        void GroupMachines(IMachineGroup machineGroup, bool group);
        void RemoveMachineFromGroup(IMachine machine);
        public void MoveMachineGroups(IEnumerable<Tuple<IMachineGroup, Tuple<float, float>>> mmg);
        void UpdateGroupedMachinesPositions(IEnumerable<Tuple<IMachine, Tuple<float, float>>> mm);

        public void InvokeImportGroupedMachinePositions(IMachine machine, float x, float y);

        event Action<IMachineGroup> MachineGroupAdded;
        event Action<IMachineGroup> MachineGroupRemoved;

        event Action<IMachine, float, float> ImportGroupedMachinePosition;
    }
}
