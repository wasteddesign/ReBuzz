﻿using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using ReBuzz.Common;
using ReBuzz.Core;
using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using BuzzGUI.Interfaces;
using System.Runtime.InteropServices;

namespace ReBuzz.NativeMachine
{
    internal class NativeMachineHost : IDisposable
    {
        private readonly string sharedId;
        internal Process childProcess;
        private MemoryMappedFile mappedFile;
        private MemoryMappedViewAccessor accessor;
        private ChannelListener channelListenerAudio;
        private ChannelListener channelListenerMIDI;
        private ChannelListener channelListenerUI;
        private ChannelListener channelListenerHost;
        private readonly string buzzPath;
        private readonly IUiDispatcher dispatcher;
        private readonly EngineSettings engineSettings;

        public bool Host64 { get; private set; }
        public HostMessage HostMessage { get; private set; }
        public UIMessage UIMessage { get; private set; }
        public AudioMessage AudioMessage { get; private set; }
        public MidiMessage MidiMessage { get; private set; }

        public event EventHandler<EventArgs> Connected;

        public bool IsConnected { get; set; }

        public NativeMachineHost(
            string sharedId,
            string buzzPath,
            IUiDispatcher dispatcher,
            EngineSettings engineSettings)
        {
            this.sharedId = sharedId + DateTime.Now.Ticks;
            this.buzzPath = buzzPath;
            this.dispatcher = dispatcher;
            this.engineSettings = engineSettings;
        }

        public void InitHost(ReBuzzCore buzz, bool host64)
        {
            if (IsConnected)
                return;

            Host64 = host64;
            string path = host64 ? buzzPath + "\\bin64\\ReBuzzEngine64.exe" : buzzPath + "\\bin32\\ReBuzzEngine32.exe";

            int mapSize = IPC.GetSharedPageSize();
            mappedFile = MemoryMappedFile.CreateNew(sharedId, mapSize);
            accessor = mappedFile.CreateViewAccessor();

            HostMessage = new HostMessage(ChannelType.HostChannel, accessor, this, engineSettings);
            UIMessage = new UIMessage(ChannelType.UIChannel, accessor, this, dispatcher, engineSettings);
            AudioMessage = new AudioMessage(ChannelType.AudioChannel, accessor, this, engineSettings);
            MidiMessage = new MidiMessage(ChannelType.MidiChannel, accessor, this, engineSettings);

            // Host messages. Only this one listens to incoming messages from native machine.
            channelListenerHost = new ChannelListener(ChannelType.HostChannel,
                ThreadPriority.Normal, "Host" + sharedId, HostMessage, buzz);
            HostMessage.ChannelListener = channelListenerHost;
            channelListenerHost.Start();

            channelListenerMIDI = new ChannelListener(ChannelType.MidiChannel,
                ThreadPriority.Normal, "MIDI" + sharedId, null, buzz);
            MidiMessage.ChannelListener = channelListenerMIDI;

            // Audio messages
            channelListenerAudio = new ChannelListener(ChannelType.AudioChannel,
                ThreadPriority.Normal, "Audio" + sharedId, AudioMessage, buzz);
            AudioMessage.ChannelListener = channelListenerAudio;

            // UI Messages
            channelListenerUI = new ChannelListener(ChannelType.UIChannel,
                ThreadPriority.Normal, "UI" + sharedId, UIMessage, buzz);
            UIMessage.ChannelListener = channelListenerUI;

            ProcessStartInfo processInfo = new ProcessStartInfo(path, sharedId);

            childProcess = Process.Start(processInfo);
            childProcess.PriorityClass = ProcessAndThreadProfile.ProcessPriorityClassNativeHostProcess;
            childProcess.EnableRaisingEvents = true;

            childProcess.WaitForInputIdle(); // Wait for the process to be ready for input
            IsConnected = true;

            childProcess.Exited += (sender, e) =>
            {
                if (childProcess.ExitCode != 0)
                {
                    if (buzz.MachineManager.IsSingleProcessMode)
                    {
                        foreach (var machine in buzz.Song.Machines)
                        {
                            if (!machine.DLL.IsManaged)
                            {
                                var machineCore = machine as MachineCore;
                                machineCore.MachineDLL.IsCrashed = true;
                                machineCore.Ready = false;
                            }
                        }
                    }
                    else
                    {
                        var kv = buzz.MachineManager.NativeMachines.FirstOrDefault(nm => nm.Value == this);

                        var machine = kv.Key;
                        if (machine != null)
                        {
                            machine.MachineDLL.IsCrashed = true;
                            machine.Ready = false;
                        }
                    }

                    Dispose();
                }
            };

            // Track child processes and close them is main app crashes/closes
            ChildProcessTracker.AddProcess(childProcess);

            UIMessage.SendMessageBuzzInitSync(buzz.MainWindowHandle, host64);
            UIMessage.UIDSPInitSync(ReBuzzCore.masterInfo.SamplesPerSec);
        }

        public void Dispose()
        {
            if (IsConnected)
            {
                channelListenerUI.StopAndJoin();
                channelListenerMIDI.StopAndJoin();
                channelListenerAudio.StopAndJoin();
                channelListenerHost.StopAndJoin();

                try
                {
                    childProcess.Kill();
                }
                catch { }

                childProcess.Dispose();
                accessor.Dispose();
                mappedFile.Dispose();
                IsConnected = false;
            }
        }
    }
}
