using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzzTests.Automation.TestMachinesControllers;
using System.IO;
using System.Linq;

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
        private readonly EngineSettings engineSettings;

        internal MachineGraphDriverExtension(
            ReBuzzCore reBuzzCore,
            ReBuzzMachines reBuzzMachines,
            FakeMachineDLLScanner fakeMachineDllScanner,
            FakeDispatcher dispatcher,
            int defaultPan,
            int defaultAmp,
            EngineSettings engineSettings)
        {
            this.reBuzzCore = reBuzzCore;
            this.reBuzzMachines = reBuzzMachines;
            this.fakeMachineDllScanner = fakeMachineDllScanner;
            this.dispatcher = dispatcher;
            this.defaultPan = defaultPan;
            this.defaultAmp = defaultAmp;
            this.engineSettings = engineSettings;
        }

        public void InsertMachineInstanceFor(DynamicMachineController controller) =>
            InsertMachineInstanceFor(controller, TestMachineConfig.Empty);

        public void InsertMachineInstanceFor(DynamicMachineController controller, TestMachineConfig config)
        {
            var machineDll = fakeMachineDllScanner.GetMachineDLL(controller.Name);
            WriteInstanceInitConfig(controller, machineDll, config);
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

        public void InsertMachineInstanceConnectedToMasterFor(DynamicMachineController controller) =>
            InsertMachineInstanceConnectedToMasterFor(controller, TestMachineConfig.Empty);

        public void InsertMachineInstanceConnectedToMasterFor(DynamicMachineController controller, TestMachineConfig config)
        {
            InsertMachineInstanceFor(controller, config);
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
                dispatcher,
                engineSettings));
        }

        private void DisconnectFromMaster(MachineCore instance)
        {
            DisconnectMachineInstances(instance, GetSongCoreMasterInstance());
        }

        public void ExecuteMachineCommand(ITestMachineInstanceCommand command)
        {
            command.Execute(reBuzzCore, reBuzzMachines);
        }

        /// <summary>
        /// Writes all per-instance startup configuration to a single .init file consumed
        /// by the native machine constructor. Contains MachineName plus any extra config.
        /// </summary>
        private static void WriteInstanceInitConfig(
            DynamicMachineController controller, IMachineDLL machineDll, TestMachineConfig config)
        {
            var lines = new[] { $"MachineName={controller.InstanceName}" }
                .Concat(config.Config.Select(kv => $"{kv.Key}={kv.Value}"));
            File.WriteAllLines(machineDll.Path + ".init", lines);
        }
    }
}