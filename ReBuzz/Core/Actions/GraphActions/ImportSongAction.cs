﻿using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using BuzzGUI.Common.Settings;
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
        private List<MachineGroupCore> machineGroups = new List<MachineGroupCore>();
        private List<int> waveIndexes = new List<int>();
        private readonly IUiDispatcher dispatcher;
        private readonly EngineSettings engineSettings;

        internal ImportSongAction(
            ReBuzzCore buzz,
            IReBuzzFile bmxFile,
            string file,
            float x,
            float y,
            IUiDispatcher dispatcher,
            EngineSettings engineSettings)
        {
            this.buzz = buzz;
            this.x = x;
            this.y = y;
            this.filename = file;
            this.bmxFile = bmxFile;
            this.dispatcher = dispatcher;
            this.engineSettings = engineSettings;
        }

        protected override void DoAction()
        {
            machines.Clear();
            machineGroups.Clear();
            waveIndexes.Clear();

            lock (ReBuzzCore.AudioLock)
            {
                ReBuzzCore.SkipAudio = true;
                bool playing = buzz.Playing;
                buzz.Playing = false;

                buzz.NotifyOpenFile(filename);

                bmxFile.FileEvent += (type, eventText, o) =>
                {
                    buzz.NotifyFileEvent(type, eventText, o);
                };

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
                        new DisconnectMachinesAction(buzz, output, dispatcher, engineSettings).Do();
                }

                buzz.RemoveMachine(machine);
            }

            // Clear waves
            foreach (int index in waveIndexes)
            {
                buzz.SongCore.Wavetable.LoadWave(index, null, null, false);
            }

            // Remove Groups
            foreach (var g in machineGroups)
            {
                buzz.RemoveMachineGroup(g);
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

        internal void AddGroupMachine(MachineGroupCore group)
        {
            machineGroups.Add(group);
        }
    }
}
