using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzzTests.Automation.TestMachinesControllers;
using System.IO;

namespace ReBuzzTests.Automation
{
    public class MachineGraphDriverExtension
    {
        private ReBuzzCore reBuzzCore;
        private readonly ReBuzzMachines reBuzzMachines;
        private readonly FakeMachineDLLScanner fakeMachineDllScanner;
        private readonly FakeDispatcher dispatcher;
        private readonly int defaultPan;
        private readonly int defaultAmp;

        internal MachineGraphDriverExtension(
            ReBuzzCore reBuzzCore,
            ReBuzzMachines reBuzzMachines,
            FakeMachineDLLScanner fakeMachineDllScanner,
            FakeDispatcher dispatcher, int defaultPan, int defaultAmp)
        {
            this.reBuzzCore = reBuzzCore;
            this.reBuzzMachines = reBuzzMachines;
            this.fakeMachineDllScanner = fakeMachineDllScanner;
            this.dispatcher = dispatcher;
            this.defaultPan = defaultPan;
            this.defaultAmp = defaultAmp;
        }

        public void InsertMachineInstanceFor(DynamicMachineController controller)
        {
            var machineDll = fakeMachineDllScanner.GetMachineDLL(controller.Name);
            File.WriteAllText(machineDll.Path + ".txt", controller.InstanceName);
            CreateInstrument(machineDll, controller.InstanceName);
            reBuzzMachines.StoreSongCoreMachine(controller.InstanceName);
        }

        public void Connect(
            DynamicMachineController sourceController,
            DynamicMachineController destinationController)
        {
            ConnectMachineInstances(
                reBuzzMachines.GetSongCoreMachineInstance(sourceController.InstanceName),
                reBuzzMachines.GetSongCoreMachineInstance(destinationController.InstanceName));
        }

        public void DisconnectFromMaster(DynamicMachineController controller)
        {
            DisconnectFromMaster(reBuzzMachines.GetSongCoreMachineInstance(controller.InstanceName));
        }

        public void InsertMachineInstanceConnectedToMasterFor(DynamicMachineController controller)
        {
            InsertMachineInstanceFor(controller);
            ConnectToMaster(reBuzzMachines.GetMachineAddedFromTest(controller.InstanceName));
        }

        private void CreateInstrument(IMachineDLL machineDll, string instanceName)
        {
            reBuzzCore.SongCore.CreateMachine(machineDll.Name, null!, instanceName, null!, null!, null!, -1, 0, 0);
        }

        private MachineCore GetSongCoreMasterInstance()
        {
            return reBuzzMachines.GetSongCoreMachineInstance("Master");
        }

        private void ConnectMachineInstances(IMachine source, IMachine destination)
        {
            reBuzzCore.SongCore.ConnectMachines(source, destination, 0, 0, defaultAmp, defaultPan);
        }

        private void ConnectToMaster(MachineCore instance)
        {
            ConnectMachineInstances(instance, GetSongCoreMasterInstance());
        }

        private void DisconnectMachineInstances(MachineCore source, MachineCore destination)
        {
            reBuzzCore.SongCore.DisconnectMachines(new MachineConnectionCore(
                source,
                0,
                destination,
                0,
                defaultAmp,
                defaultPan,
                dispatcher));
        }

        private void DisconnectFromMaster(MachineCore instance)
        {
            DisconnectMachineInstances(instance, GetSongCoreMasterInstance());
        }

        public void ExecuteMachineCommand(ITestMachineInstanceCommand command)
        {
            command.Execute(reBuzzCore, reBuzzMachines);
        }
    }
}