using FluentAssertions;
using ReBuzz.Core;
using System.Collections.Generic;
using System.Linq;

namespace ReBuzzTests.Automation
{
    public class ReBuzzMachines(ReBuzzCore reBuzzCore)
    {
        private Dictionary<string, MachineCore> addedMachineInstances = new(); //bug

        public MachineCore GetSongCoreMachine(string name)
        {
            return reBuzzCore.SongCore.MachinesList.Single(m => m.Name == name);
        }

        public MachineCore GetMachineManagerMachine(string instanceName)
        {
            return reBuzzCore.MachineManager.NativeMachines.Single(kvp => kvp.Key.Name == instanceName).Key;
        }

        public MachineCore GetMachineAddedFromTest(string instanceName)
        {
            addedMachineInstances.Should().ContainKey(instanceName);
            addedMachineInstances[instanceName].Should().NotBeNull();
            var instrument = addedMachineInstances[instanceName];
            return instrument;
        }

        public void StoreSongCoreMachine(string controllerInstanceName)
        {
            var addedInstance = GetSongCoreMachine(controllerInstanceName);
            addedMachineInstances[controllerInstanceName] = addedInstance;
        }
    }
}