using AtmaFileSystem;
using BuzzGUI.Interfaces;
using FluentAssertions;
using ReBuzz.Core;
using System.Linq;

namespace ReBuzzTests.Automation.Assertions
{
    public class InitialSongStateAssertions : ISongStateAssertions
    {
        public void AssertStateOfSongAndSongCore(
            SongCore songCore,
            ISong song,
            ReBuzzCore reBuzzCore,
            AbsoluteDirectoryPath gearDir,
            IAdditionalInitialStateAssertions additionalAssertions)
        {
            songCore.BuzzCore.Should().Be(reBuzzCore);
            songCore.ActionStack.Actions.Should().BeEmpty();
            songCore.ActionStack.CanRedo.Should().BeFalse();
            songCore.ActionStack.CanUndo.Should().BeFalse();
            songCore.ActionStack.MaxNumberOfActions.Should().Be(int.MaxValue);
            songCore.Associations.Should().BeEmpty();
            songCore.CanRedo.Should().BeFalse();
            songCore.CanUndo.Should().BeFalse();
            songCore.LoopStart.Should().Be(0);
            songCore.LoopEnd.Should().Be(16);
            songCore.PlayPosition.Should().Be(0);
            songCore.Sequences.Should().BeEmpty();
            songCore.SequencesList.Should().BeEmpty();
            songCore.SongName.Should().BeNullOrEmpty();
            songCore.SoloMode.Should().BeFalse();
            songCore.Wavetable.Song.Should().Be(song);
            songCore.Wavetable.Volume.Should().Be(ISongStateAssertions.DefaultInitialVolume);
            songCore.Wavetable.Waves.Should().Equal(Enumerable.Range(0, 200).Select(_ => null as IWave).ToArray());

            songCore.Machines.Should().ContainSingle();
            InitialStateAssertions.AssertIsMasterMachine(songCore.Machines[0], reBuzzCore, gearDir, additionalAssertions);
            songCore.MachinesList[0].Should().Be(songCore.Machines[0]);
            additionalAssertions.AssertStateOfSongCore(songCore, gearDir, reBuzzCore);

            song.Should().BeSameAs(songCore);
        }
    }
}