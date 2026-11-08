using AwesomeAssertions;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzzTests.Automation;
using ReBuzzTests.Automation.TestMachinesControllers;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace ReBuzzTests
{
    /// <summary>
    /// The .bmxml loader used to size non-Int16 wave layers from the already-adjusted sample count,
    /// so every load cut such a layer to about half its length. After enough load/save cycles the
    /// count went negative and loading or saving threw "Arithmetic operation resulted in an overflow".
    /// </summary>
    public class SavingAndLoadingWavetableTests
    {
        private const int WaveIndex = 3;

        // Loading a .bmxml creates WPF objects, which need an STA thread
        [Apartment(ApartmentState.STA)]
        [TestCase(WaveFormat.Int16, 1001)]
        [TestCase(WaveFormat.Int32, 1001)]
        [TestCase(WaveFormat.Float32, 1001)]
        [TestCase(WaveFormat.Int24, 1001)]
        [TestCase(WaveFormat.Int24, 1000)]
        public void BmxmlSongKeepsEveryWaveSampleOverRepeatedLoadAndSave(WaveFormat format, int sampleCount)
        {
            var song = SaveSongWithWave(format, sampleCount);

            for (int cycle = 0; cycle < 3; cycle++)
            {
                using var driver = new Driver();
                driver.Start();

                var songPath = driver.RandomSongPath(".bmxml");
                File.WriteAllBytes(songPath.ToString(), song);
                driver.DawCommands.LoadSong(DialogChoices.Select(songPath));
                driver.AssertNoErrorsReportedToUser();

                var layer = LoadedLayer(driver);
                layer.Format.Should().Be(format);
                layer.SampleCount.Should().Be(sampleCount);
                layer.LoopEnd.Should().Be(sampleCount);
                LastSample(layer).Should().BeApproximately(ExpectedSample(sampleCount - 1, sampleCount), 1e-3f);

                // The loaded song already has a file name, so a plain save would not ask for one.
                var resavedPath = driver.RandomSongPath(".bmxml");
                driver.DawCommands.SaveCurrentSongAs(DialogChoices.Select(resavedPath));
                driver.AssertNoErrorsReportedToUser();
                song = File.ReadAllBytes(resavedPath.ToString());
            }
        }

        /// <summary>
        /// Saves a song whose wavetable holds one stereo layer filled with a ramp, and returns the file's bytes.
        /// </summary>
        private static byte[] SaveSongWithWave(WaveFormat format, int sampleCount)
        {
            using var driver = new Driver();
            driver.Start();

            driver.MachineGraph.ExecuteMachineCommand(new CoreCommand(core =>
            {
                var wavetable = core.SongCore.WavetableCore;
                wavetable.AllocateWave(WaveIndex, "test.wav", "test", sampleCount, format, true, BuzzNote.FromMIDINote(60), false, false);
                var layer = wavetable.WavesList[WaveIndex].LayersList.Single();
                layer.SampleRate = 44100;
                layer.LoopStart = 0;
                layer.LoopEnd = sampleCount;

                var data = new float[sampleCount * 2];
                for (int i = 0; i < sampleCount; i++)
                    data[i * 2] = data[i * 2 + 1] = ExpectedSample(i, sampleCount);
                layer.SetDataAsFloat(data, 0, 2, 0, 0, sampleCount);
                layer.SetDataAsFloat(data, 1, 2, 1, 0, sampleCount);
            }));

            var songPath = driver.RandomSongPath(".bmxml");
            driver.DawCommands.SaveCurrentSongForTheFirstTime(DialogChoices.Select(songPath));
            driver.AssertNoErrorsReportedToUser();

            return File.ReadAllBytes(songPath.ToString());
        }

        private static float ExpectedSample(int index, int sampleCount) => -0.5f + index / (float)sampleCount;

        private static WaveLayerCore LoadedLayer(Driver driver)
        {
            WaveLayerCore layer = null;
            driver.MachineGraph.ExecuteMachineCommand(new CoreCommand(core =>
                layer = core.SongCore.WavetableCore.WavesList[WaveIndex].LayersList.Single()));
            return layer;
        }

        private static float LastSample(WaveLayerCore layer)
        {
            var output = new float[1];
            layer.GetDataAsFloat(output, 0, 1, 0, layer.SampleCount - 1, 1);
            return output[0];
        }

        /// <summary>
        /// Runs an action on the ReBuzz core.
        /// </summary>
        private class CoreCommand(Action<ReBuzzCore> action) : ITestMachineInstanceCommand
        {
            public void Execute(ReBuzzCore buzzCore, ReBuzzMachines machineCores)
            {
                action(buzzCore);
            }
        }
    }
}
