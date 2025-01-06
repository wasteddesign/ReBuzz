using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace BuzzGUI.Interfaces
{
    public interface IMachineGraph : IActionStack, INotifyPropertyChanged
    {
        IBuzz Buzz { get; }
        ReadOnlyCollection<IMachine> Machines { get; }

        // undoable actions
        void CreateMachine(int id, float x, float y);
        void CreateMachine(string machine, string instrument, string name, byte[] data, string patterneditor, byte[] patterneditordata, int trackcount, float x, float y);      // NOTE: patterneditor == null means default editor, patterneditor == "" means built-in editor
        void ReplaceMachine(IMachine m, int id, float x, float y);
        void ReplaceMachine(IMachine m, string machine, string instrument, float x, float y);
        void InsertMachine(IMachineConnection m, int id, float x, float y);
        void InsertMachine(IMachineConnection m, string machine, string instrument, float x, float y);
        void CloneMachine(IMachine m, float x, float y);
        void MoveMachines(IEnumerable<Tuple<IMachine, Tuple<float, float>>> mm);
        void ConnectMachines(IMachine src, IMachine dst, int srcchn, int dstchn, int amp, int pan);
        void DisconnectMachines(IMachineConnection mc);
        void DeleteMachines(IEnumerable<IMachine> m);
        void SetConnectionParameter(IMachineConnection mc, int index, int oldvalue, int newvalue);
        void SetConnectionChannel(IMachineConnection mc, bool destination, int channel);

        bool CanConnectMachines(IMachine src, IMachine dst);

        void ShowContextMenu(int x, int y);
        void DoubleClick(int x, int y);
        void QuickNewMachine(char firstch);
        void ImportSong(float x, float y);

        void BeginImport(IDictionary<string, string> machinerename);
        void EndImport();

        event Action<IMachine> MachineAdded;
        event Action<IMachine> MachineRemoved;
        event Action<IMachineConnection> ConnectionAdded;
        event Action<IMachineConnection> ConnectionRemoved;

    }
}
