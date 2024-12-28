using BuzzGUI.Interfaces;
using libsndfile;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BuzzGUI.Common.InterfaceExtensions
{
    public static class WavetableExtensions
    {
        static List<string> ext;

        public static IList<string> GetSupportedFileTypeExtensions(this IWavetable wavetable)
        {
            if (ext == null)
            {
                ext = SoundFile.FormatsMajor.Select(f => "." + f.Extension)
                    .Concat(new[] { ".mp3", ".ogg", ".aif" })
                    .Distinct().ToList();
            }

            return ext;
        }

        public static bool CanLoadFile(string path)
        {
            return ext.Contains(Path.GetExtension(path).ToLowerInvariant());
        }

        const int ReadBufferSize = 4096;

        public static void LoadWaveEx(this IWavetable wavetable, int index, string path, string name, bool add)
        {
            if (Path.GetExtension(path).ToLowerInvariant() == ".mp3")
            {
                // TODO: move mp3 loading code here
                wavetable.LoadWave(index, path, name, add);
                return;
            }

            try
            {
                using (var sf = SoundFile.OpenRead(path))
                {
                    LoadWave(wavetable, index, path, name, add, sf);
                }
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(e.Message, "libsndfile", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

            }
        }

        public static void LoadWaveEx(this IWavetable wavetable, int index, Stream stream, string path, string name, bool add)
        {
            try
            {
                using (var sf = SoundFile.OpenRead(stream))
                {
                    LoadWave(wavetable, index, path, name, add, sf);
                }
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(e.Message, "libsndfile", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

            }
        }

        static void LoadWave(IWavetable wavetable, int index, string path, string name, bool add, SoundFile sf)
        {
            if (sf.ChannelCount != 1 && sf.ChannelCount != 2) throw new Exception("Unsupported channel count.");
            var subformat = sf.Format & Format.SF_FORMAT_SUBMASK;

            WaveFormat wf;

            if (subformat == Format.SF_FORMAT_FLOAT || subformat == Format.SF_FORMAT_DOUBLE) wf = WaveFormat.Float32;
            else if (subformat == Format.SF_FORMAT_PCM_32) wf = WaveFormat.Int32;
            else if (subformat == Format.SF_FORMAT_PCM_24) wf = WaveFormat.Int24;
            else wf = WaveFormat.Int16;

            var inst = sf.Instrument;
            var rootnote = BuzzNote.FromMIDINote(Math.Max(0, inst.basenote - 12));	// -12 for backwards compatibility

            //when adding a new layer to a slot we need to make sure they are all in the same format, convert the old layers in the slot to float / stereo if needed.
            int ChannelCount = sf.ChannelCount;
            if (add == true)
            {
                WaveCommandHelpers.ConvertSlotIfNeeded(wavetable, index, ref wf, ref ChannelCount);
            }

            wavetable.AllocateWave(index, path, name, (int)sf.FrameCount, wf, ChannelCount == 2, rootnote, add, false);

            var wave = wavetable.Waves[index];
            var layer = wave.Layers.Where(l => l.RootNote == rootnote).Last();      // multiple layers may have the same root, so use Last() to get the new one

            layer.SampleRate = sf.SampleRate;

            if (inst.loop_count > 0)
            {
                wave.Flags |= WaveFlags.Loop;
                layer.LoopStart = (int)inst.loops[0].start;
                layer.LoopEnd = (int)inst.loops[0].end + 1;							// buzz loop is right-open, sndfile loop is right-closed
            }
            else //no loop, we should set reasonable values
            {
                layer.LoopStart = 0;
                layer.LoopEnd = layer.SampleCount;
            }

            var buffer = new float[ReadBufferSize * sf.ChannelCount];

            long framesread = 0;
            while (framesread < sf.FrameCount)
            {
                var n = sf.ReadFloat(buffer, ReadBufferSize);
                if (n <= 0) break;

                for (int ch = 0; ch < sf.ChannelCount; ch++)
                {
                    layer.SetDataAsFloat(buffer, ch, sf.ChannelCount, ch, (int)framesread, (int)n);

                    //write the same data to the right channel too in case the new layer is mono but we converted the slot to stereo already
                    //if ((ConvertSlotToStereo == true && sf.ChannelCount == 1) || (AllLayersAreStereo == true && sf.ChannelCount == 1))
                    if ((ChannelCount == 2 && sf.ChannelCount == 1))
                    {
                        //BuzzGUI.Common.Global.Buzz.DCWriteLine("COPY LEFT TO RIGHT ON NEW LAYER");
                        layer.SetDataAsFloat(buffer, 0, 1, 1, (int)framesread, (int)n);
                    }
                }

                framesread += n;
            }

            layer.InvalidateData();

            wavetable.Song.Buzz.DCWriteLine("[libsndfile]\r\n" + sf.LogInfo.Replace("\n", "\r\n"));
        }

    }
}
