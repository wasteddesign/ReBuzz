using BuzzGUI.Interfaces;
using libsndfile;
using System;
using System.IO;

namespace BuzzGUI.Common.InterfaceExtensions
{
    public static class WaveformBaseExtensions
    {
        const int WriteBufferSize = 4096;

        public static void SaveAsWAV(this IWaveformBase w, Stream s)
        {
            Format f;
            if (w.Format == WaveFormat.Float32) f = Format.SF_FORMAT_FLOAT;
            else if (w.Format == WaveFormat.Int32) f = Format.SF_FORMAT_PCM_32;
            else if (w.Format == WaveFormat.Int24) f = Format.SF_FORMAT_PCM_24;
            else f = Format.SF_FORMAT_PCM_16;

            using (var sf = SoundFile.Create(s, w.SampleRate, w.ChannelCount, f | Format.SF_FORMAT_WAV))
            {
                var buffer = new float[WriteBufferSize * w.ChannelCount];

                long frameswritten = 0;
                while (frameswritten < w.SampleCount)
                {
                    var n = Math.Min(w.SampleCount - frameswritten, WriteBufferSize);

                    for (int ch = 0; ch < w.ChannelCount; ch++)
                        w.GetDataAsFloat(buffer, ch, w.ChannelCount, ch, (int)frameswritten, (int)n);

                    sf.WriteFloat(buffer, 0, n);

                    frameswritten += n;
                }


            }


        }
    }
}
