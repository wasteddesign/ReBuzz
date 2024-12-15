using AtmaFileSystem;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using FluentAssertions;
using ReBuzz.Core;
using ReBuzz.MachineManagement;
using ReBuzz.ManagedMachine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReBuzzTests.Automation
{
    public class InitialStateAfterAppStartAssertions : IAdditionalInitialStateAssertions
    {
        public void AssertInitialStateOfSongCore(SongCore songCore, AbsoluteDirectoryPath gearDir, ReBuzzCore reBuzzCore)
        {
            songCore.MachinesList.Should().HaveCount(2);
            AssertMachineCore(reBuzzCore, gearDir, songCore.MachinesList[1],
                reBuzzCore.MachineManager.ManagedMachines.First().Value);
        }

        public void AssertInitialStateOfPatternEditor(ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir, IMachine machine)
        {
            InitialStateAssertions.AssertFakeModernPatternEditor(reBuzzCore, gearDir, (MachineDLL)machine.PatternEditorDLL);
        }

        public void AssertInitialStateOfMachineManager(
            ReBuzzCore reBuzzCore, AbsoluteDirectoryPath gearDir, MachineManager machineManager)
        {
            machineManager.ManagedMachines.Should().HaveCount(1);

            var machineCore = machineManager.ManagedMachines.Keys.Single();
            var managedMachineHost = machineManager.ManagedMachines[machineCore];
            AssertMachineCore(reBuzzCore, gearDir, machineCore, managedMachineHost);

            InitialStateAssertions.AssertFakeModernPatternEditorHostInitialState(reBuzzCore, managedMachineHost);
        }

        private void AssertMachineCore(
            ReBuzzCore reBuzzCore,
            AbsoluteDirectoryPath gearDir,
            MachineCore machineCore,
            ManagedMachineHost managedMachineHost)
        {
            machineCore.workLock.IsHeldByCurrentThread.Should().BeFalse();
            machineCore.Graph.Should().Be(reBuzzCore.SongCore);
            machineCore.parametersChanged.Should().HaveCount(5);
            var ampParam = machineCore.parametersChanged.ToList().Single(p => p.Key.Name == "Amp");
            var panParam = machineCore.parametersChanged.ToList().Single(p => p.Key.Name == "Pan");
            var gainParam = machineCore.parametersChanged.ToList().Single(p => p.Key.Name == "Gain");
            var bypassParam = machineCore.parametersChanged.ToList().Single(p => p.Key.Name == "Bypass");
            var aTrackParam = machineCore.parametersChanged.ToList().Single(p => p.Key.Name == "ATrackParam");

            InitialStateAssertions.AssertMasterParameters(
                ((IMachine)machineCore).ParameterGroups[0],
                ampParam.Key,
                panParam.Key);
            ampParam.Value.Should().Be(0);
            panParam.Value.Should().Be(0);

            InitialStateAssertions.AssertGlobalParameters(gainParam.Key, bypassParam.Key, ((IMachine)machineCore).ParameterGroups[1]);
            gainParam.Value.Should().Be(0);
            bypassParam.Value.Should().Be(0);
            InitialStateAssertions.AssertParameter(
                parameter: aTrackParam.Key, 
                expectedParameter: ExpectedMachineParameter.ATrackParam(),
                expectedParentGroup: ((IMachine)machineCore).ParameterGroups[2], 
                expectedIndexInGroup: 0);
            aTrackParam.Value.Should().Be(0);

            machineCore.Inputs.Should().BeEmpty();
            machineCore.AllInputs.Should().BeEmpty();

            machineCore.Outputs.Should().HaveCount(1);
            machineCore.Outputs[0].Amp.Should().Be(16384);
            machineCore.Outputs[0].DestinationChannel.Should().Be(0);
            machineCore.Outputs[0].HasPan.Should().BeTrue();
            machineCore.Outputs[0].Pan.Should().Be(16384);
            machineCore.Outputs[0].SourceChannel.Should().Be(0);
            machineCore.Outputs[0].Source.Should()
                .Be(reBuzzCore.MachineManager.ManagedMachines.Keys.Single(machine =>
                    machine.Name == machineCore.Outputs[0].Source.Name));
            InitialStateAssertions.AssertIsMasterMachine(machineCore.Outputs[0].Destination, reBuzzCore, gearDir, this);

            machineCore.AllOutputs.Should().Equal(machineCore.Outputs);

            machineCore.InputChannelCount.Should().Be(1);
            machineCore.OutputChannelCount.Should().Be(1);
            machineCore.Name.Should().Be("\u0001pe1"); //!!
            machineCore.Position.Item1.Should().Be(0F);
            machineCore.Position.Item2.Should().Be(0F);
            machineCore.Patterns.Should().BeEmpty();
            machineCore.PatternsList.Should().BeEmpty();

            machineCore.IsControlMachine.Should().BeFalse();
            machineCore.IsActive.Should().BeFalse();
            machineCore.IsMuted.Should().BeFalse();
            machineCore.IsSoloed.Should().BeFalse();
            machineCore.IsBypassed.Should().BeFalse();
            machineCore.IsWireless.Should().BeFalse();

            machineCore.HasStereoInput.Should().BeTrue();
            machineCore.HasStereoOutput.Should().BeTrue();

            machineCore.LastEngineThread.Should().Be(0);
            machineCore.EngineThreadId.Should().Be(0);
            machineCore.Latency.Should().Be(0);
            machineCore.OverrideLatency.Should().Be(0);

            machineCore.PatternEditorDLL.Should().BeNull();
            machineCore.BaseOctave.Should().Be(4);
            machineCore.Data.Should().NotBeNull();
            machineCore.PatternEditorData.Should().BeNull();

            machineCore.CMachinePtr.Should().Be(IntPtr.Zero);
            machineCore.ParameterWindow.Should().BeNull();

            InitialStateAssertions.AssertInitialPerformanceData(machineCore.PerformanceDataCurrent);
            InitialStateAssertions.AssertInitialPerformanceData(machineCore.PerformanceData);

            InitialStateAssertions.AssertFakeModernPatternEditor(
                machineCore.ManagedMachine, 
                managedMachineHost);

            machineCore.TrackCount.Should().Be(1);

            machineCore.AttributesList.Should().NotBeNull().And.BeEmpty();
            machineCore.Attributes.Should().NotBeNull().And.BeEmpty();

            machineCore.Ready.Should().BeTrue();
            machineCore.CMachineHost.Should().NotBe(0);
            machineCore.CMachineEventType.Should().BeEmpty();

            machineCore.MachineMenuCommand.Should().BeOfType<SimpleCommand>();
            machineCore.InstrumentName.Should().BeNull();
            machineCore.Hidden.Should().BeTrue();
            machineCore.EditorMachine.Should().BeNull();

            machineCore.MachineGUIWindow.Should().BeNull();

            machineCore.DLL.Should().Be(machineCore.MachineDLL);
            InitialStateAssertions.AssertFakeModernPatternEditor(reBuzzCore, gearDir, machineCore.MachineDLL);
        }
    }
}