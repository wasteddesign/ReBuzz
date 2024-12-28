using BuzzGUI.Interfaces;
using libsndfile;
using System;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace BuzzGUI.WavetableView.Commands
{
    public class SaveFileCommand : ICommand
    {
        readonly WaveSlotVM waveSlotVM;

        public SaveFileCommand(WaveSlotVM waveSlotVM)
        {
            this.waveSlotVM = waveSlotVM;
        }

        const int WriteBufferSize = 4096;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }


        public bool CanExecute(object parameter)
        {
            return waveSlotVM.Wave != null;
        }

        public void Execute(object parameter)
        {
            var wave = waveSlotVM.Wave.Layers.FirstOrDefault();
            if (wave == null) return;

            // Configure save file dialog box
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = waveSlotVM.Wave.Name; // Default file name
            dlg.DefaultExt = ".wav"; // Default file extension

            dlg.Filter = "Wave files|*.wav|Apple/SGI AIFF|*.aif|Sun/NeXT AU format|*.au|RAW PCM|*.raw|FLAC lossless|*.flac|Ogg Vorbis|*.ogg"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                // Save document

                // setup streams and format
                FileStream outf = System.IO.File.Create(dlg.FileName); //System.IO.File.Create(currentdir + filename);
                libsndfile.Format fmt;

                // get bit depth
                if (wave.Format == WaveFormat.Float32) fmt = Format.SF_FORMAT_FLOAT;
                else if (wave.Format == WaveFormat.Int32) fmt = Format.SF_FORMAT_PCM_32;
                else if (wave.Format == WaveFormat.Int24) fmt = Format.SF_FORMAT_PCM_24;
                else fmt = Format.SF_FORMAT_PCM_16;

                // use filterindex to figure out format (and open a dialog for options?)
                switch (dlg.FilterIndex)
                {
                    case (1):
                        {
                            fmt = fmt | libsndfile.Format.SF_FORMAT_WAV;
                            break;
                        }

                    case (2):
                        {
                            fmt = fmt | libsndfile.Format.SF_FORMAT_AIFF;
                            break;
                        }

                    case (3):
                        {
                            fmt = fmt | libsndfile.Format.SF_FORMAT_AU;
                            break;
                        }

                    case (4):
                        {
                            fmt = fmt | libsndfile.Format.SF_FORMAT_RAW;
                            break;
                        }

                    case (5):
                        {
                            fmt = fmt | libsndfile.Format.SF_FORMAT_FLAC;
                            break;
                        }

                    case (6):
                        {
                            fmt = libsndfile.Format.SF_FORMAT_VORBIS | libsndfile.Format.SF_FORMAT_OGG;
                            break;
                        }

                    default:
                        {
                            fmt = libsndfile.Format.SF_FORMAT_FLOAT | libsndfile.Format.SF_FORMAT_WAV;
                            break;
                        }
                }

                // declare file
                var outFile = SoundFile.Create(outf, wave.SampleRate, wave.ChannelCount, fmt);

                // code modified from WaveformBaseExtention
                var buffer = new float[WriteBufferSize * wave.ChannelCount];

                long frameswritten = 0;
                while (frameswritten < wave.SampleCount)
                {
                    var n = Math.Min(wave.SampleCount - frameswritten, WriteBufferSize);

                    for (int ch = 0; ch < wave.ChannelCount; ch++)
                        wave.GetDataAsFloat(buffer, ch, wave.ChannelCount, ch, (int)frameswritten, (int)n);

                    outFile.WriteFloat(buffer, 0, n);

                    frameswritten += n;
                }
                // close
                outFile.Close();
            }
        }
    }
}
