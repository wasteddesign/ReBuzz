using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
using Microsoft.Win32;
using ReBuzz.Common;
using ReBuzz.FileOps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Documents;

namespace ReBuzz.Core.Actions.GraphActions
{
    internal class ImportSongAction : BuzzAction
    {
        private readonly ReBuzzCore buzz;
        private readonly float x;
        private readonly float y;
        private string filename;
        private IReBuzzFile bmxFile;
        private List<MachineCore> machines = new List<MachineCore>();
        private List<int> waveIndexes = new List<int>();

        internal ImportSongAction(ReBuzzCore buzz, IReBuzzFile bmxFile, string file, float x, float y)
        {
            this.buzz = buzz;
            this.x = x;
            this.y = y;
            this.filename = file;
            this.bmxFile = bmxFile;
        }

        protected override void DoAction()
        {
            machines.Clear();
            waveIndexes.Clear();

            lock (ReBuzzCore.AudioLock)
            {
                ReBuzzCore.SkipAudio = true;
                bool playing = buzz.Playing;
                buzz.Playing = false;

                //try
                {
                    bmxFile.Load(filename, x, y, this);
                }
                /*
                catch (Exception ex)
                {
                    Utils.MessageBox("Error importing file " + filename + "\n\n" + ex.ToString(), "Error importing file.");
                }
                */

                ReBuzzCore.SkipAudio = false;
                buzz.Playing = playing;
            }
        }

        protected override void UndoAction()
        {
            foreach (var machine in machines)
            {
                // Remove connections to master
                foreach (var output in machine.AllOutputs.ToArray())
                {
                    if (output.Destination.DLL.Info.Type == MachineType.Master)
                        new DisconnectMachinesAction(buzz, output).Do();
                }

                // Remove connections
                foreach (var input in machine.AllInputs.ToArray())
                {
                    new DisconnectMachinesAction(buzz, input).Do();
                }

                // Clear waves
                foreach (int index in waveIndexes)
                {
                    buzz.SongCore.Wavetable.LoadWave(index, null, null, false);
                }

                buzz.RemoveMachine(machine);
            }
        }

        internal void AddMachine(MachineCore machineNew)
        {
            machines.Add(machineNew);
        }

        internal void AddWaveIndex(int newIndex)
        {
            waveIndexes.Add(newIndex);
        }
    }
}
