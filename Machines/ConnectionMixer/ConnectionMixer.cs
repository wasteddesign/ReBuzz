using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using Sini;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace WDE.ConnectionMixer
{
    [MachineDecl(Name = "Connection Mixer Console", ShortName = "ConnMixer", Author = "WDE", MaxTracks = 1)]
    public class ConnectionMixer : IBuzzMachine, INotifyPropertyChanged
    {
        public IBuzzMachineHost host;

        internal string version = "1.1.3";

        public static readonly int NUM_MIXERS = 16;
        public static readonly int NUM_PARAMS = 4;

        MIDIHelperUtil MIDIHelper;
        public bool SendParameterChangesPending { get; private set; }

        // If machines renamed
        private Dictionary<IMachine, string> MachineAssociations = new Dictionary<IMachine, string>();

        public ConnectionMixer(IBuzzMachineHost host)
        {
            this.host = host;

            MIDIHelper = new MIDIHelperUtil(NUM_MIXERS, NUM_PARAMS);

            SendParameterChangesPending = false;

            Global.Buzz.Song.MachineRemoved += Song_MachineRemoved;
            Global.Buzz.Song.MachineAdded += Song_MachineAdded;
            Global.Buzz.Song.ConnectionRemoved += Song_ConnectionRemoved;
            Global.Buzz.Song.ConnectionAdded += Song_ConnectionAdded;

            Global.Buzz.PropertyChanged += Buzz_PropertyChanged;
        }

        private void Buzz_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "MIDIFocusMachine" && host.Machine == Global.Buzz.MIDIFocusMachine)
            {
                if (RegSettings.SoftTakeoverOnMIDIFocus)
                    MIDIHelper.ClearAllSoftTakeoverData();
            }
        }

        public void ImportFinished(IDictionary<string, string> machineNameMap)
        {
            // Need to update current param values for vol & pan
            for (int i = 0; i < NUM_MIXERS; i++)
            {
                var mcd = machineState.Mcd[i];
                if (mcd.source != "" && mcd.destination != "")
                {
                    IMachineConnection conn = GetConnection(mcd.source, mcd.destination);
                    UpdatePan(i, conn);
                    UpdateVol(i, conn);
                }
            }

            SetupMidiOutputDevice();
        }

        private void Song_ConnectionAdded(IMachineConnection conn)
        {
            bool notify = true; // always notify

            for (int i = 0; i < NUM_MIXERS; i++)
            {
                var mcd = machineState.Mcd[i];
                if (mcd.source == conn.Source.Name && mcd.destination == conn.Destination.Name)
                {
                    UpdateVol(i, conn);
                    UpdatePan(i, conn);
                    notify = true;
                }
            }

            if (notify && PropertyChanged != null)
                PropertyChanged.Raise(this, "ConnectionChanged");
        }

        private void Song_MachineAdded(IMachine obj)
        {
            obj.PropertyChanged += Machine_PropertyChanged; // for renaming etc
            MachineAssociations.Add(obj, obj.Name);

            // Add machines added before this
            if (obj == host.Machine)
            {
                foreach (IMachine mac in Global.Buzz.Song.Machines)
                {
                    if (mac != host.Machine)
                    {
                        mac.PropertyChanged += Machine_PropertyChanged;
                        MachineAssociations.Add(mac, mac.Name);
                    }
                }
            }

            bool notify = true; // Allways notify
            // Check parameters
            for (int i = 0; i < NUM_MIXERS; i++)
            {
                var mcd = machineState.Mcd[i];
                for (int j = 0; j < NUM_PARAMS; j++)
                {
                    var mpd = mcd.paramTable[j];
                    if (mpd.machine == obj.Name)
                    {
                        notify = true;
                        break;
                    }
                }
            }

            if (notify && PropertyChanged != null)
                PropertyChanged.Raise(this, "MachineAdded");
        }

        public void Work()
        {
            if (host.SubTickInfo.PosInSubTick != 0)
                return;

            if (!SendParameterChangesPending)
            {
                SendParameterChangesPending = true;
                var t = Task.Run(() =>
                {
                    for (int i = 0; i < NUM_MIXERS; i++)
                    {
                        var mcd = machineState.Mcd[i];
                        if (MIDIHelper.IsInterpolatorActive(i, EMIDIControlType.Volume))
                        {
                            IParameter par = this.host.Machine.ParameterGroups[1].Parameters[i * 2];

                            int newamp = MIDIHelper.GetInterpolatorValue(i, EMIDIControlType.Volume);

                            par.SetValue(0, newamp);
                            if (par.Group.Machine.DLL.Info.Version >= 42)
                                par.Group.Machine.SendControlChanges();
                        }
                        if (MIDIHelper.IsInterpolatorActive(i, EMIDIControlType.Pan))
                        {
                            IParameter par = this.host.Machine.ParameterGroups[1].Parameters[i * 2 + 1];

                            int newPan = MIDIHelper.GetInterpolatorValue(i, EMIDIControlType.Pan);

                            par.SetValue(0, newPan);
                            if (par.Group.Machine.DLL.Info.Version >= 42)
                                par.Group.Machine.SendControlChanges();
                        }
                    }
                    SendParameterChangesPending = false;
                });
            }
        }

        internal static ResourceDictionary GetBuzzThemeResources()
        {
            ResourceDictionary skin = new ResourceDictionary();

            try
            {
                string selectedTheme = Global.Buzz.SelectedTheme == "<default>" ? "Default" : Global.Buzz.SelectedTheme;
                string skinPath = Global.BuzzPath + "\\Themes\\" + selectedTheme + "\\Gear\\ConnectionMixer\\ConnectionMixerConsole.xaml";
                //skin = (ResourceDictionary)BuzzGUI.Common.XamlReaderEx.LoadHack(skinPath);
                skin.Source = new Uri(skinPath, UriKind.Absolute);
            }
            catch (Exception e)
            {
                try
                {
                    Global.Buzz.DCWriteLine(e.Message);
                    string skinPath = Global.BuzzPath + "\\Themes\\Default\\\\Gear\\ConnectionMixer\\ConnectionMixerConsole.xaml";
                    skin.Source = new Uri(skinPath, UriKind.Absolute);
                }
                catch (Exception)
                {
                    skin = null;
                }
            }

            return skin;
        }

        private void Machine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Name")
            {
                IMachine mac = (IMachine)sender;
                string oldName = MachineAssociations[mac];
                string newName = mac.Name;
                MachineAssociations[mac] = mac.Name; // New name

                bool notify = false;
                for (int i = 0; i < NUM_MIXERS; i++)
                {
                    var mcd = machineState.Mcd[i];

                    if (mcd.source == oldName)
                    {
                        mcd.source = newName;
                        notify = true;
                    }
                    else if (mcd.destination == oldName)
                    {
                        mcd.destination = newName;
                        notify |= true;
                    }

                    for (int j = 0; j < NUM_PARAMS; j++)
                    {
                        var mpd = mcd.paramTable[j];
                        if (mpd.machine == oldName)
                        {
                            mpd.machine = newName;
                            notify = true;
                        }
                    }
                }

                if (notify && PropertyChanged != null)
                    PropertyChanged.Raise(this, "Name");
            }
        }

        private void Song_ConnectionRemoved(IMachineConnection obj)
        {
            bool notify = false;

            for (int i = 0; i < NUM_MIXERS; i++)
            {
                var mcd = machineState.Mcd[i];
                if (mcd.source == obj.Source.Name && mcd.destination == obj.Destination.Name)
                {
                    // mcd.source = mcd.destination = "";
                    mcd.destination = "";
                    notify = true;
                }
            }

            if (notify && PropertyChanged != null)
                PropertyChanged.Raise(this, "ConnectionChanged");
        }

        private void Song_MachineRemoved(IMachine obj)
        {
            obj.PropertyChanged -= Machine_PropertyChanged;
            MachineAssociations.Remove(obj);

            if (obj == host.Machine)
            {
                Global.Buzz.Song.MachineRemoved -= Song_MachineRemoved;
                Global.Buzz.Song.MachineAdded -= Song_MachineAdded;
                Global.Buzz.Song.ConnectionRemoved -= Song_ConnectionRemoved;
                Global.Buzz.Song.ConnectionAdded -= Song_ConnectionAdded;

                for (int i = 0; i < NUM_MIXERS; i++)
                {
                    ConnectionSelected(i, null);
                }
            }
            else
            {
                bool notify = false;

                for (int i = 0; i < NUM_MIXERS; i++)
                {
                    var mcd = machineState.Mcd[i];
                    if (mcd.source == obj.Name)
                    {
                        mcd.source = mcd.destination = "";
                        notify = true;
                    }
                    else if (mcd.destination == obj.Name)
                    {
                        mcd.source = mcd.destination = "";
                        notify = true;
                    }
                }

                // Check parameters
                for (int i = 0; i < NUM_MIXERS; i++)
                {
                    var mcd = machineState.Mcd[i];
                    for (int j = 0; j < NUM_PARAMS; j++)
                    {
                        var mpd = mcd.paramTable[j];
                        if (mpd.machine == obj.Name)
                        {
                            mpd.machine = mpd.param = "";
                            mpd.group = 0;
                            notify = true;
                        }
                    }
                }

                if (notify && PropertyChanged != null)
                    PropertyChanged.Raise(this, "MachineRemoved");
            }
        }

        public void MidiControlChange(int ctrl, int channel, int value)
        {
            for (int i = 0; i < NUM_MIXERS; i++)
            {
                var mcd = machineState.Mcd[i];
                if (mcd.MIDIChannelMute == channel && mcd.MIDICCMute == ctrl)
                {
                    IMachineConnection conn = GetConnection(mcd.source, mcd.destination);
                    if (conn != null)
                    {
                        Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                        {
                            if (value == 0)
                                conn.Source.IsMuted = false;
                            else if (value == 127)
                                conn.Source.IsMuted = true;

                        }));
                    }
                }
                if (mcd.MIDIChannelSolo == channel && mcd.MIDICCSolo == ctrl)
                {
                    IMachineConnection conn = GetConnection(mcd.source, mcd.destination);
                    if (conn != null)
                    {
                        Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                        {
                            if (value == 0)
                                conn.Source.IsSoloed = false;
                            else if (value == 127)
                                conn.Source.IsSoloed = true;

                        }));
                    }
                }
                if (mcd.MIDIChannelVolume == channel && mcd.MIDICCVolume == ctrl)
                {
                    // 1 = Non track parameters
                    IParameter par = this.host.Machine.ParameterGroups[1].Parameters[i * 2];

                    if (!MIDIHelper.IsMIDIConnected(i, EMIDIControlType.Volume))
                    {
                        int currentVal = par.GetValue(0);

                        double db = value / 127.0;
                        double MinAmp = 66;
                        int newamp = db == 0 ? 0 : (int)Math.Round(Decibel.ToAmplitude(db * (MinAmp + Decibel.FromAmplitude((double)0xfffe / 0x4000)) - MinAmp) * 0x4000);

                        double newDB = Decibel.FromAmplitude(newamp * (1.0 / 0x4000));
                        double currentDB = Decibel.FromAmplitude(currentVal * (1.0 / 0x4000));

                        MIDIHelper.UpdateMidiConnectionStatus(i, currentDB, newDB, EMIDIControlType.Volume);
                    }

                    if (MIDIHelper.IsMIDIConnected(i, EMIDIControlType.Volume))
                    {
                        double db = value / 127.0;
                        double MinAmp = 66;
                        int newamp = db == 0 ? 0 : (int)Math.Round(Decibel.ToAmplitude(db * (MinAmp + Decibel.FromAmplitude((double)0xfffe / 0x4000)) - MinAmp) * 0x4000);

                        int currentVal = par.GetValue(0);

                        MIDIHelper.SetInterpolator(i, currentVal, newamp, EMIDIControlType.Volume);
                    }
                }
                if (mcd.MIDIChannelPan == channel && mcd.MIDICCPan == ctrl)
                {
                    // 1 = Non track parameters
                    IParameter par = this.host.Machine.ParameterGroups[1].Parameters[i * 2 + 1];
                    double scale = (par.MaxValue - par.MinValue) / 127.0;

                    if (!MIDIHelper.IsMIDIConnected(i, EMIDIControlType.Pan))
                    {
                        int currentVal = par.GetValue(0);
                        int currentValueToMIDI = (int)((currentVal - par.MinValue) / scale);

                        MIDIHelper.UpdateMidiConnectionStatus(i, currentValueToMIDI, value, EMIDIControlType.Pan);
                    }

                    if (MIDIHelper.IsMIDIConnected(i, EMIDIControlType.Pan))
                    {
                        int currentVal = par.GetValue(0);
                        int newVal = (int)(value * scale + par.MinValue);

                        MIDIHelper.SetInterpolator(i, currentVal, newVal, EMIDIControlType.Pan);
                    }
                }
                if (mcd.MIDIChannelP1 == channel && mcd.MIDICCP1 == ctrl)
                {
                    MIDIUpdateParam(i, 0, value);
                }
                if (mcd.MIDIChannelP2 == channel && mcd.MIDICCP2 == ctrl)
                {
                    MIDIUpdateParam(i, 1, value);
                }
                if (mcd.MIDIChannelP3 == channel && mcd.MIDICCP3 == ctrl)
                {
                    MIDIUpdateParam(i, 2, value);
                }
                if (mcd.MIDIChannelP4 == channel && mcd.MIDICCP4 == ctrl)
                {
                    MIDIUpdateParam(i, 3, value);
                }
            }
        }

        internal void MIDIUpdateParam(int mixerNum, int paramNum, int MIDIvalue)
        {
            var mcd = machineState.Mcd[mixerNum];
            var machine = Global.Buzz.Song.Machines.FirstOrDefault(x => x.Name == mcd.paramTable[paramNum].machine);
            if (machine != null)
            {
                IParameter par = machine.GetParameter(mcd.paramTable[paramNum].param);
                if (par != null)
                {
                    double scale = (par.MaxValue - par.MinValue) / 127.0;

                    if (!MIDIHelper.IsMIDIConnected(mixerNum, paramNum))
                    {
                        int currentVal = par.GetValue(0);
                        int currentValueToMIDI = (int)((currentVal - par.MinValue) / scale);

                        MIDIHelper.UpdateMidiConnectionParameterStatus(mixerNum, paramNum, currentValueToMIDI, MIDIvalue);
                    }

                    if (MIDIHelper.IsMIDIConnected(mixerNum, paramNum))
                    {
                        int newVal = (int)(MIDIvalue * scale + par.MinValue);
                        par.SetValue(0, newVal);
                        if (par.Group.Machine.DLL.Info.Version >= 42)
                            par.Group.Machine.SendControlChanges();
                    }
                }
            }
        }

        private IMachineConnection GetConnection(string source, string destination)
        {
            IMachineConnection conn = null;

            IMachine mSource = Global.Buzz.Song.Machines.FirstOrDefault(x => x.Name == source);
            if (mSource != null)
            {
                conn = mSource.Outputs.FirstOrDefault(x => x.Destination.Name == destination);
            }
            return conn;
        }

        private void UpdateConnectionVolume(int index, int vol)
        {
            IMachineConnection conn = GetConnection(machineState.Mcd[index].source, machineState.Mcd[index].destination);
            if (conn != null)
                conn.Amp = vol;
        }

        private void UpdateConnectionPan(int index, int pan)
        {
            IMachineConnection conn = GetConnection(machineState.Mcd[index].source, machineState.Mcd[index].destination);
            if (conn != null)
                conn.Pan = pan;
        }

        internal void PropertyChangedUI(string property)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (PropertyChanged != null)
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(property));
            }));
        }

        // Machine parameters start

        private int vol1;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 1")]
        public int Vol1
        {
            get => vol1; set
            {
                vol1 = value;
                UpdateConnectionVolume(0, vol1);
                PropertyChangedUI("Vol");
            }
        }

        private int pan1;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 1")]
        public int Pan1
        {
            get => pan1; set
            {
                pan1 = value;
                UpdateConnectionPan(0, pan1);
                PropertyChangedUI("Pan");
            }
        }

        private int vol2;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 2")]
        public int Vol2
        {
            get => vol2; set
            {
                vol2 = value;
                UpdateConnectionVolume(1, vol2);
                PropertyChangedUI("Vol");
            }
        }

        private int pan2;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 2")]
        public int Pan2
        {
            get => pan2; set
            {
                pan2 = value;
                UpdateConnectionPan(1, pan2);
                PropertyChangedUI("Pan");
            }
        }
        private int vol3;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 3")]
        public int Vol3
        {
            get => vol3; set
            {
                vol3 = value;
                UpdateConnectionVolume(2, vol3);
                PropertyChangedUI("Vol");
            }
        }

        private int pan3;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 3")]
        public int Pan3
        {
            get => pan3; set
            {
                pan3 = value;
                UpdateConnectionPan(2, pan3);
                PropertyChangedUI("Pan");
            }
        }
        private int vol4;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 4")]
        public int Vol4
        {
            get => vol4; set
            {
                vol4 = value;
                UpdateConnectionVolume(3, vol4);
                PropertyChangedUI("Vol");
            }
        }

        private int pan4;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 4")]
        public int Pan4
        {
            get => pan4; set
            {
                pan4 = value;
                UpdateConnectionPan(3, pan4);
                PropertyChangedUI("Pan");
            }
        }
        private int vol5;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 5")]
        public int Vol5
        {
            get => vol5; set
            {
                vol5 = value;
                UpdateConnectionVolume(4, vol5);
                PropertyChangedUI("Vol");
            }
        }

        private int pan5;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 5")]
        public int Pan5
        {
            get => pan5; set
            {
                pan5 = value;
                UpdateConnectionPan(4, pan5);
                PropertyChangedUI("Pan");
            }
        }
        private int vol6;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 6")]
        public int Vol6
        {
            get => vol6; set
            {
                vol6 = value;
                UpdateConnectionVolume(5, vol6);
                PropertyChangedUI("Vol");
            }
        }

        private int pan6;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 6")]
        public int Pan6
        {
            get => pan6; set
            {
                pan6 = value;
                UpdateConnectionPan(5, pan6);
                PropertyChangedUI("Pan");
            }
        }
        private int vol7;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 7")]
        public int Vol7
        {
            get => vol7; set
            {
                vol7 = value;
                UpdateConnectionVolume(6, vol7);
                PropertyChangedUI("Vol");
            }
        }

        private int pan7;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 7")]
        public int Pan7
        {
            get => pan7; set
            {
                pan7 = value;
                UpdateConnectionPan(6, pan7);
                PropertyChangedUI("Pan");
            }
        }
        private int vol8;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 8")]
        public int Vol8
        {
            get => vol8; set
            {
                vol8 = value;
                UpdateConnectionVolume(7, vol8);
                PropertyChangedUI("Vol");
            }
        }

        private int pan8;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 8")]
        public int Pan8
        {
            get => pan8; set
            {
                pan8 = value;
                UpdateConnectionPan(7, pan8);
                PropertyChangedUI("Pan");
            }
        }
        private int vol9;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 9")]
        public int Vol9
        {
            get => vol9; set
            {
                vol9 = value;
                UpdateConnectionVolume(8, vol9);
                PropertyChangedUI("Vol");
            }
        }

        private int pan9;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 9")]
        public int Pan9
        {
            get => pan9; set
            {
                pan9 = value;
                UpdateConnectionPan(8, pan9);
                PropertyChangedUI("Pan");
            }
        }
        private int vol10;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 10")]
        public int Vol10
        {
            get => vol10; set
            {
                vol10 = value;
                UpdateConnectionVolume(9, vol10);
                PropertyChangedUI("Vol");
            }
        }

        internal void SwitchMixers(int m1, int m2)
        {
            var md1 = machineState.Mcd[m1];
            var md2 = machineState.Mcd[m2];
            machineState.Mcd[m1] = md2;
            machineState.Mcd[m2] = md1;
        }

        private int pan10;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 10")]
        public int Pan10
        {
            get => pan10; set
            {
                pan10 = value;
                UpdateConnectionPan(9, pan10);
                PropertyChangedUI("Pan");
            }
        }
        private int vol11;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 11")]
        public int Vol11
        {
            get => vol11; set
            {
                vol11 = value;
                UpdateConnectionVolume(10, vol11);
                PropertyChangedUI("Vol");
            }
        }

        private int pan11;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 11")]
        public int Pan11
        {
            get => pan11; set
            {
                pan11 = value;
                UpdateConnectionPan(10, pan11);
                PropertyChangedUI("Pan");
            }
        }
        private int vol12;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 12")]
        public int Vol12
        {
            get => vol12; set
            {
                vol12 = value;
                UpdateConnectionVolume(11, vol12);
                PropertyChangedUI("Vol");
            }
        }


        private int pan12;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 12")]
        public int Pan12
        {
            get => pan12; set
            {
                pan12 = value;
                UpdateConnectionPan(11, pan12);
                PropertyChangedUI("Pan");
            }
        }
        private int vol13;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 13")]
        public int Vol13
        {
            get => vol13; set
            {
                vol13 = value;
                UpdateConnectionVolume(12, vol13);
                PropertyChangedUI("Vol");
            }
        }

        private int pan13;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 13")]
        public int Pan13
        {
            get => pan13; set
            {
                pan13 = value;
                UpdateConnectionPan(12, pan13);
                PropertyChangedUI("Pan");
            }
        }
        private int vol14;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 14")]
        public int Vol14
        {
            get => vol14; set
            {
                vol14 = value;
                UpdateConnectionVolume(13, vol14);
                PropertyChangedUI("Vol");
            }
        }

        private int pan14;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 14")]
        public int Pan14
        {
            get => pan14; set
            {
                pan14 = value;
                UpdateConnectionPan(13, pan14);
                PropertyChangedUI("Pan");
            }
        }
        private int vol15;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 15")]
        public int Vol15
        {
            get => vol15; set
            {
                vol15 = value;
                UpdateConnectionVolume(14, vol15);
                PropertyChangedUI("Vol");
            }
        }

        private int pan15;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 15")]
        public int Pan15
        {
            get => pan15; set
            {
                pan15 = value;
                UpdateConnectionPan(14, pan15);
                PropertyChangedUI("Pan");
            }
        }
        private int vol16;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0xFFFE, IsStateless = true, Description = "Volume 16")]
        public int Vol16
        {
            get => vol16; set
            {
                vol16 = value;
                UpdateConnectionVolume(15, vol16);
                PropertyChangedUI("Vol");
            }
        }

        private int pan16;
        [ParameterDecl(DefValue = 0x4000, MinValue = 0, MaxValue = 0x8000, IsStateless = true, Description = "Pan 16")]
        public int Pan16
        {
            get => pan16; set
            {
                pan16 = value;
                UpdateConnectionPan(15, pan16);
                PropertyChangedUI("Pan");
            }
        }

        // Machine parameters end

        // actual machine ends here. the stuff below demonstrates some other features of the api.

        public class MachineParamData
        {
            public string machine;
            public int group;
            public string param;
            public int track;
            public bool flip;

            public MachineParamData()
            {
                machine = param = "";
                group = 0;
                track = 0;
                flip = false;
            }
        }

        public class MachineConnectionData
        {
            public string source;
            public string destination;

            public MachineParamData[] paramTable;

            public int MIDIChannelVolume;
            public int MIDICCVolume;

            public int MIDIChannelPan;
            public int MIDICCPan;

            public int MIDIChannelMute;
            public int MIDICCMute;

            public int MIDIChannelSolo;
            public int MIDICCSolo;

            public int MIDIChannelP1;
            public int MIDICCP1;

            public int MIDIChannelP2;
            public int MIDICCP2;

            public int MIDIChannelP3;
            public int MIDICCP3;

            public int MIDIChannelP4;
            public int MIDICCP4;

            public MachineConnectionData()
            {
                paramTable = new MachineParamData[NUM_PARAMS];
                for (int i = 0; i < NUM_PARAMS; i++)
                    paramTable[i] = new MachineParamData();

                source = destination = "";
                ResetMIDI();
            }

            public void ResetMIDI()
            {
                MIDIChannelVolume = -1;
                MIDICCVolume = 0;
                MIDIChannelPan = -1;
                MIDICCPan = 0;
                MIDIChannelMute = -1;
                MIDICCMute = 0;
                MIDIChannelSolo = -1;
                MIDICCSolo = 0;
                MIDIChannelP1 = -1;
                MIDICCP1 = 0;
                MIDIChannelP2 = -1;
                MIDICCP2 = 0;
                MIDIChannelP3 = -1;
                MIDICCP3 = 0;
                MIDIChannelP4 = -1;
                MIDICCP4 = 0;
            }
        }

        public class State : INotifyPropertyChanged
        {
            public State()
            {
                mcd = new MachineConnectionData[NUM_MIXERS];
                for (int i = 0; i < NUM_MIXERS; i++)
                    mcd[i] = new MachineConnectionData();

                NumMixerConsoles = NUM_MIXERS;
            }

            MachineConnectionData[] mcd;
            public MachineConnectionData[] Mcd
            {
                get { return mcd; }
                set
                {
                    mcd = value;
                    if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Mcd"));
                    // NOTE: the INotifyPropertyChanged stuff is only used for data binding in the GUI in this demo. it is not required by the serializer.
                }
            }

            public int NumMixerConsoles { get; set; }
            public string MIDIOutDeviceName { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        State machineState = new State();
        public State MachineState           // a property called 'MachineState' gets automatically saved in songs and presets
        {
            get { return machineState; }
            set
            {
                machineState = value;

                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("MachineState"));
            }
        }

        private void SetupMidiOutputDevice()
        {
            var deviceName = machineState.MIDIOutDeviceName;
            var buzz = Global.Buzz;
            var midiOut = buzz.GetMidiOuts().FirstOrDefault(mo => mo.Item2 == deviceName);
            if (midiOut != null)
            {
                int deviceNumber = midiOut.Item1;

                for (int i = 0; i < NUM_MIXERS; i++)
                {
                    var mcd = machineState.Mcd[i];
                    
                    if (mcd.MIDIChannelP1 != -1)
                    {
                        int data = MIDICreateOutputData(mcd, 0);
                        buzz.SendMIDIOutput(deviceNumber, data);
                    }
                    if (mcd.MIDIChannelP2 != -1)
                    {
                        int data = MIDICreateOutputData(mcd, 1);
                        buzz.SendMIDIOutput(deviceNumber, data);
                    }
                    if (mcd.MIDIChannelP3 != -1)
                    {
                        int data = MIDICreateOutputData(mcd, 2);
                        buzz.SendMIDIOutput(deviceNumber, data);
                    }
                    if (mcd.MIDIChannelP4 != -1)
                    {
                        int data = MIDICreateOutputData(mcd, 3);
                        buzz.SendMIDIOutput(deviceNumber, data);
                    }
                }
            }
        }

        internal int MIDICreateOutputData(MachineConnectionData mcd, int paramNum)
        {
            int midiData = -1;

            var machine = Global.Buzz.Song.Machines.FirstOrDefault(x => x.Name == mcd.paramTable[paramNum].machine);
            if (machine != null)
            {
                IParameter par = machine.GetParameter(mcd.paramTable[paramNum].param);
                if (par != null)
                {
                    double scale = (par.MaxValue - par.MinValue) / 127.0;

                    int currentVal = par.GetValue(0);
                    int currentValueToMIDI = (int)((currentVal - par.MinValue) / scale);

                    midiData = MIDI.Encode(MIDI.ControlChange | mcd.MIDIChannelP1, mcd.MIDICCP1, currentValueToMIDI);
                }
            }
            return midiData;
        }

        public IEnumerable<IMenuItem> Commands
        {
            get
            {
                yield return new MenuItemVM()
                {
                    Text = "About...",
                    Command = new SimpleCommand()
                    {
                        CanExecuteDelegate = p => true,
                        ExecuteDelegate = p => new AboutWindow(version).ShowDialog()
                    }
                };
            }
        }



        public event PropertyChangedEventHandler PropertyChanged;

        public void ResetMIDIParameterConnection(int mixreNumMixerControl, int paramNum)
        {
            MIDIHelper.ClearSoftTakeoverParamData(mixreNumMixerControl, paramNum);
        }

        internal void ResetMIDIConnection(int mixreNumMixerControl, EMIDIControlType type)
        {
            MIDIHelper.ClearMIDISoftTakeoverData(mixreNumMixerControl, type);
        }

        Dictionary<IParameter, Tuple<int, IMachineConnection>> parameterPanMixerDict = new Dictionary<IParameter, Tuple<int, IMachineConnection>>();
        Dictionary<IParameter, Tuple<int, IMachineConnection>> parameterAmpMixerDict = new Dictionary<IParameter, Tuple<int, IMachineConnection>>();
        internal void ConnectionSelected(int mixerNumber, IMachineConnection selectedConnection)
        {
            MIDIHelper.ClearMIDISoftTakeoverData(mixerNumber, EMIDIControlType.Volume);
            MIDIHelper.ClearMIDISoftTakeoverData(mixerNumber, EMIDIControlType.Pan);

            if (selectedConnection == null)
            {
                var machine = Global.Buzz.Song.Machines.FirstOrDefault(m => m.Name == machineState.Mcd[mixerNumber].destination);

                if (machine != null)
                {
                    var connection = machine.Inputs.FirstOrDefault(i => i.Source.Name == machineState.Mcd[mixerNumber].source);
                    if (connection != null)
                    {
                        var inputTrack = connection.Destination.Inputs.IndexOf(connection);
                        var parameter = connection.Destination.ParameterGroups[0].Parameters[0];
                        parameter.UnsubscribeEvents(inputTrack, InputAmpParamValueChanged, null);
                        parameterAmpMixerDict.Remove(parameter);

                        parameter = connection.Destination.ParameterGroups[0].Parameters[1];
                        parameter.UnsubscribeEvents(inputTrack, InputPanParamValueChanged, null);

                        parameterPanMixerDict.Remove(parameter);
                    }
                }

                machineState.Mcd[mixerNumber].source = "";
                machineState.Mcd[mixerNumber].destination = "";
            }
            else
            {
                machineState.Mcd[mixerNumber].source = selectedConnection.Source.Name;
                machineState.Mcd[mixerNumber].destination = selectedConnection.Destination.Name;
                UpdateVol(mixerNumber, selectedConnection);
                UpdatePan(mixerNumber, selectedConnection);

                var inputTrack = selectedConnection.Destination.Inputs.IndexOf(selectedConnection);
                var parameter = selectedConnection.Destination.ParameterGroups[0].Parameters[0];
                parameter.SubscribeEvents(inputTrack, InputAmpParamValueChanged, null);
                parameterAmpMixerDict[parameter] = new Tuple<int, IMachineConnection>(mixerNumber, selectedConnection);

                parameter = selectedConnection.Destination.ParameterGroups[0].Parameters[1];
                parameter.SubscribeEvents(inputTrack, InputPanParamValueChanged, null);

                parameterPanMixerDict[parameter] = new Tuple<int, IMachineConnection>(mixerNumber, selectedConnection);
            }
        }

        void InputAmpParamValueChanged(IParameter param, int track)
        {
            var data = parameterAmpMixerDict[param];
            UpdateVol(data.Item1, data.Item2);
        }

        void InputPanParamValueChanged(IParameter param, int track)
        {
            var data = parameterPanMixerDict[param];
            UpdatePan(data.Item1, data.Item2);
        }

        internal void SetParameter(int mixerNum, IParameter p, int paramNum)
        {
            var mcd = machineState.Mcd[mixerNum];
            var mdp = mcd.paramTable[paramNum];

            mdp.machine = p.Group.Machine.Name;
            mdp.param = p.Name;
            mdp.group = p.Group.Machine.ParameterGroups.IndexOf(p.Group);
        }

        internal void UpdatePan(int mixerC, IMachineConnection selectedConnection)
        {
            if (selectedConnection != null)
            {
                host.Machine.ParameterGroups[1].Parameters[mixerC * 2 + 1].SetValue(0, selectedConnection.Pan);
                host.Machine.SendControlChanges();
            }
        }

        internal void UpdateVol(int mixeC, IMachineConnection selectedConnection)
        {
            if (selectedConnection != null)
            {
                host.Machine.ParameterGroups[1].Parameters[mixeC * 2].SetValue(0, selectedConnection.Amp);
                host.Machine.SendControlChanges();
            }
        }

        internal int SelectedTrack(int mixerNumber, int parNum)
        {
            return machineState.Mcd[mixerNumber].paramTable[parNum].track;
        }

        internal void SetSelectedTrack(int mixerNumber, int parNum, int track)
        {
            machineState.Mcd[mixerNumber].paramTable[parNum].track = track;
        }

        internal void BindMidiVolume(int mixerNumber, int MIDIChannel, int MIDICC)
        {
            machineState.Mcd[mixerNumber].MIDIChannelVolume = MIDIChannel;
            machineState.Mcd[mixerNumber].MIDICCVolume = MIDICC;
        }

        internal void BindMidiPan(int mixerNumber, int MIDIChannel, int MIDICC)
        {
            machineState.Mcd[mixerNumber].MIDIChannelPan = MIDIChannel;
            machineState.Mcd[mixerNumber].MIDICCPan = MIDICC;
        }

        internal void BindMidi(int mixerNumber, EMIDIControlType type, int MIDIChannel, int MIDICC)
        {
            switch (type)
            {
                case EMIDIControlType.Mute:
                    machineState.Mcd[mixerNumber].MIDIChannelMute = MIDIChannel;
                    machineState.Mcd[mixerNumber].MIDICCMute = MIDICC;
                    break;
                case EMIDIControlType.Solo:
                    machineState.Mcd[mixerNumber].MIDIChannelSolo = MIDIChannel;
                    machineState.Mcd[mixerNumber].MIDICCSolo = MIDICC;
                    break;
                case EMIDIControlType.Pan:
                    machineState.Mcd[mixerNumber].MIDIChannelPan = MIDIChannel;
                    machineState.Mcd[mixerNumber].MIDICCPan = MIDICC;
                    break;
                case EMIDIControlType.P1:
                    machineState.Mcd[mixerNumber].MIDIChannelP1 = MIDIChannel;
                    machineState.Mcd[mixerNumber].MIDICCP1 = MIDICC;
                    break;
                case EMIDIControlType.P2:
                    machineState.Mcd[mixerNumber].MIDIChannelP2 = MIDIChannel;
                    machineState.Mcd[mixerNumber].MIDICCP2 = MIDICC;
                    break;
                case EMIDIControlType.P3:
                    machineState.Mcd[mixerNumber].MIDIChannelP3 = MIDIChannel;
                    machineState.Mcd[mixerNumber].MIDICCP3 = MIDICC;
                    break;
                case EMIDIControlType.P4:
                    machineState.Mcd[mixerNumber].MIDIChannelP4 = MIDIChannel;
                    machineState.Mcd[mixerNumber].MIDICCP4 = MIDICC;
                    break;
                case EMIDIControlType.Volume:
                    machineState.Mcd[mixerNumber].MIDIChannelVolume = MIDIChannel;
                    machineState.Mcd[mixerNumber].MIDICCVolume = MIDICC;
                    break;
            }
        }

        internal void UnbindMidi(int mixerNumber, EMIDIControlType type)
        {
            switch (type)
            {
                case EMIDIControlType.Mute:
                    machineState.Mcd[mixerNumber].MIDIChannelMute = -1;
                    machineState.Mcd[mixerNumber].MIDICCMute = 0;
                    break;
                case EMIDIControlType.Solo:
                    machineState.Mcd[mixerNumber].MIDIChannelSolo = -1;
                    machineState.Mcd[mixerNumber].MIDICCSolo = 0;
                    break;
                case EMIDIControlType.Pan:
                    machineState.Mcd[mixerNumber].MIDIChannelPan = -1;
                    machineState.Mcd[mixerNumber].MIDICCPan = 0;
                    break;
                case EMIDIControlType.P1:
                    machineState.Mcd[mixerNumber].MIDIChannelP1 = -1;
                    machineState.Mcd[mixerNumber].MIDICCP1 = 0;
                    break;
                case EMIDIControlType.P2:
                    machineState.Mcd[mixerNumber].MIDIChannelP2 = -1;
                    machineState.Mcd[mixerNumber].MIDICCP2 = 0;
                    break;
                case EMIDIControlType.P3:
                    machineState.Mcd[mixerNumber].MIDIChannelP3 = -1;
                    machineState.Mcd[mixerNumber].MIDICCP3 = 0;
                    break;
                case EMIDIControlType.P4:
                    machineState.Mcd[mixerNumber].MIDIChannelP4 = -1;
                    machineState.Mcd[mixerNumber].MIDICCP4 = 0;
                    break;
                case EMIDIControlType.Volume:
                    machineState.Mcd[mixerNumber].MIDIChannelVolume = -1;
                    machineState.Mcd[mixerNumber].MIDICCVolume = 0;
                    break;
            }
        }

        internal void UnbindMidi(int mixerNumber)
        {
            machineState.Mcd[mixerNumber].ResetMIDI();
        }

        internal bool IsMIDIBind(int mixerNumber, EMIDIControlType type)
        {
            bool ret = false;
            switch (type)
            {
                case EMIDIControlType.Mute:
                    ret = machineState.Mcd[mixerNumber].MIDIChannelMute != -1;
                    break;
                case EMIDIControlType.Solo:
                    ret = machineState.Mcd[mixerNumber].MIDIChannelSolo != -1;
                    break;
                case EMIDIControlType.Pan:
                    ret = machineState.Mcd[mixerNumber].MIDIChannelPan != -1;
                    break;
                case EMIDIControlType.P1:
                    ret = machineState.Mcd[mixerNumber].MIDIChannelP1 != -1;
                    break;
                case EMIDIControlType.P2:
                    ret = machineState.Mcd[mixerNumber].MIDIChannelP2 != -1;
                    break;
                case EMIDIControlType.P3:
                    ret = machineState.Mcd[mixerNumber].MIDIChannelP3 != -1;
                    break;
                case EMIDIControlType.P4:
                    ret = machineState.Mcd[mixerNumber].MIDIChannelP4 != -1;
                    break;
                case EMIDIControlType.Volume:
                    ret = machineState.Mcd[mixerNumber].MIDIChannelVolume != -1;
                    break;
            }

            return ret;
        }

        internal Tuple<int, int, string> GetMIDIData(int mixerNumber, EMIDIControlType type)
        {
            switch (type)
            {
                case EMIDIControlType.Mute:
                    return new Tuple<int, int, string>(machineState.Mcd[mixerNumber].MIDIChannelMute, machineState.Mcd[mixerNumber].MIDICCMute, "Mute");
                case EMIDIControlType.Solo:
                    return new Tuple<int, int, string>(machineState.Mcd[mixerNumber].MIDIChannelSolo, machineState.Mcd[mixerNumber].MIDICCSolo, "Solo");
                case EMIDIControlType.Pan:
                    return new Tuple<int, int, string>(machineState.Mcd[mixerNumber].MIDIChannelPan, machineState.Mcd[mixerNumber].MIDICCPan, "Pan");
                case EMIDIControlType.P1:
                    return new Tuple<int, int, string>(machineState.Mcd[mixerNumber].MIDIChannelP1, machineState.Mcd[mixerNumber].MIDICCP1, "Param 1");
                case EMIDIControlType.P2:
                    return new Tuple<int, int, string>(machineState.Mcd[mixerNumber].MIDIChannelP2, machineState.Mcd[mixerNumber].MIDICCP2, "Param 2");
                case EMIDIControlType.P3:
                    return new Tuple<int, int, string>(machineState.Mcd[mixerNumber].MIDIChannelP3, machineState.Mcd[mixerNumber].MIDICCP3, "Param 3");
                case EMIDIControlType.P4:
                    return new Tuple<int, int, string>(machineState.Mcd[mixerNumber].MIDIChannelP4, machineState.Mcd[mixerNumber].MIDICCP4, "Param 4");
                case EMIDIControlType.Volume:
                    return new Tuple<int, int, string>(machineState.Mcd[mixerNumber].MIDIChannelVolume, machineState.Mcd[mixerNumber].MIDICCVolume, "Volume");
            }

            return new Tuple<int, int, string>(-1, 0, "");
        }

        internal void LoadMIDIMapping(string fileName)
        {
            try
            {
                MIDIHelper.ClearAllSoftTakeoverData();

                IniFile ini = new IniFile(fileName);

                for (int i = 0; i < NUM_MIXERS; i++)
                {
                    var mcd = machineState.Mcd[i];

                    string section = "MixerControl" + (i + 1);

                    mcd.MIDIChannelVolume = ini.GetInt(section, "MIDIChannelVolume", -1);
                    mcd.MIDICCVolume = ini.GetInt(section, "MIDICCVolume", 0);
                    mcd.MIDIChannelPan = ini.GetInt(section, "MIDIChannelPan", -1);
                    mcd.MIDICCPan = ini.GetInt(section, "MIDICCPan", 0);
                    mcd.MIDIChannelMute = ini.GetInt(section, "MIDIChannelMute", -1);
                    mcd.MIDICCMute = ini.GetInt(section, "MIDICCMute", 0);
                    mcd.MIDIChannelSolo = ini.GetInt(section, "MIDIChannelSolo", -1);
                    mcd.MIDICCSolo = ini.GetInt(section, "MIDICCSolo", 0);
                    mcd.MIDIChannelP1 = ini.GetInt(section, "MIDIChannelP1", -1);
                    mcd.MIDICCP1 = ini.GetInt(section, "MIDICCP1", 0);
                    mcd.MIDIChannelP2 = ini.GetInt(section, "MIDIChannelP2", -1);
                    mcd.MIDICCP2 = ini.GetInt(section, "MIDICCP2", 0);
                    mcd.MIDIChannelP3 = ini.GetInt(section, "MIDIChannelP3", -1);
                    mcd.MIDICCP3 = ini.GetInt(section, "MIDICCP3", 0);
                    mcd.MIDIChannelP4 = ini.GetInt(section, "MIDIChannelP4", -1);
                    mcd.MIDICCP4 = ini.GetInt(section, "MIDICCP4", 0);
                }
            }
            catch (Exception)
            {
                return;
            }

            RegSettings.AddFileToRecentFiles(fileName);
        }

        internal void SaveMIDIMapping(string fileName)
        {
            try
            {
                File.Create(fileName).Close();

                IniFile ini = new IniFile(fileName);

                for (int i = 0; i < NUM_MIXERS; i++)
                {
                    var mcd = machineState.Mcd[i];

                    string section = "MixerControl" + (i + 1);

                    ini.SetValue(section, "MIDIChannelVolume", "" + mcd.MIDIChannelVolume);
                    ini.SetValue(section, "MIDICCVolume", "" + mcd.MIDICCVolume);
                    ini.SetValue(section, "MIDIChannelPan", "" + mcd.MIDIChannelPan);
                    ini.SetValue(section, "MIDICCPan", "" + mcd.MIDICCPan);
                    ini.SetValue(section, "MIDIChannelMute", "" + mcd.MIDIChannelMute);
                    ini.SetValue(section, "MIDICCMute", "" + mcd.MIDICCMute);
                    ini.SetValue(section, "MIDIChannelSolo", "" + mcd.MIDIChannelSolo);
                    ini.SetValue(section, "MIDICCSolo", "" + mcd.MIDICCSolo);
                    ini.SetValue(section, "MIDIChannelP1", "" + mcd.MIDIChannelP1);
                    ini.SetValue(section, "MIDICCP1", "" + mcd.MIDICCP1);
                    ini.SetValue(section, "MIDIChannelP2", "" + mcd.MIDIChannelP2);
                    ini.SetValue(section, "MIDICCP2", "" + mcd.MIDICCP2);
                    ini.SetValue(section, "MIDIChannelP3", "" + mcd.MIDIChannelP3);
                    ini.SetValue(section, "MIDICCP3", "" + mcd.MIDICCP3);
                    ini.SetValue(section, "MIDIChannelP4", "" + mcd.MIDIChannelP4);
                    ini.SetValue(section, "MIDICCP4", "" + mcd.MIDICCP4);
                }

                ini.SaveTo(fileName);
            }
            catch (Exception)
            {
                return;
            }

            RegSettings.AddFileToRecentFiles(fileName);
        }

        internal void UnbindAll()
        {
            for (int i = 0; i < NUM_MIXERS; i++)
            {
                machineState.Mcd[i].ResetMIDI();
            }
        }
    }

    public class MachineGUIFactory : IMachineGUIFactory { public IMachineGUI CreateGUI(IMachineGUIHost host) { return new ConnectionMixerGUI(); } }
    public class ConnectionMixerGUI : UserControl, IMachineGUI
    {
        public IMachine machine;
        ConnectionMixer connectionMixerMachine;
        StackPanel sp;
        DockPanel dpMenuPanel;
        Grid mainGrid;

        public ScrollViewer sw { get; set; }

        public IMachine Machine
        {
            get { return machine; }
            set
            {
                if (machine != null)
                {
                }

                machine = value;

                if (machine != null)
                {
                    connectionMixerMachine = (ConnectionMixer)machine.ManagedMachine;
                }
            }
        }

        public bool DropPeak { get; internal set; }

        public ConnectionMixerGUI()
        {
            this.Loaded += (sender, e) =>
            {
                DropPeak = true;

                mainGrid = new Grid() { HorizontalAlignment = HorizontalAlignment.Stretch };
                mainGrid.RowDefinitions.Add(new RowDefinition() { MaxHeight = 26 });
                mainGrid.RowDefinitions.Add(new RowDefinition());

                dpMenuPanel = new DockPanel() { Margin = new Thickness(6, 2, 4, 0) };

                sp = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
                sw = new ScrollViewer() { Margin = new Thickness(2), VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };

                sp.Margin = new Thickness(4);
                sw.Content = sp;

                Grid.SetRow(sw, 1);
                mainGrid.Children.Add(sw);
                machine.ParameterWindow.Content = mainGrid;
                UpdateMixerConsoleView();

                Menu menu = CreateMainMenu();
                DockPanel.SetDock(menu, Dock.Top);
                dpMenuPanel.Children.Add(menu);
                Grid.SetRow(dpMenuPanel, 0);
                mainGrid.Children.Add(dpMenuPanel);

                machine.ParameterWindow.GotKeyboardFocus += (sender2, e2) =>
                {
                    connectionMixerMachine.PropertyChangedUI("Vol");
                    connectionMixerMachine.PropertyChangedUI("Pan");
                };

                machine.ParameterWindow.PreviewKeyDown += (sender2, e2) =>
                {
                    if (e2.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        DropPeak = !DropPeak;
                        e.Handled = true;
                    }
                    if (e2.Key == Key.System || Keyboard.Modifiers == ModifierKeys.Alt)
                    {
                        menu.Visibility = menu.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;

                        if (menu.Visibility == Visibility.Visible)
                            mainGrid.RowDefinitions[0].Height = new GridLength(26);
                        else
                            mainGrid.RowDefinitions[0].Height = new GridLength(0);
                        e.Handled = true;
                    }
                };
                /*
                sp.MouseLeftButtonDown += (sender2, e2) =>
                {
                    //Global.Buzz.MIDIFocusLocked = false;
                    Global.Buzz.MIDIFocusMachine = machine;
                };
                */

                machine.ParameterWindow.SizeChanged += (sender2, e2) =>
                {
                    if (menu.Visibility == Visibility.Collapsed)
                        mainGrid.RowDefinitions[0].Height = new GridLength(0);

                    machine.ParameterWindow.SizeToContent = SizeToContent.Height;
                    e2.Handled = true;
                };

                // added after window loaded
                HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(machine.ParameterWindow).Handle);
                source.AddHook(new HwndSourceHook(WndProc));
            };
        }

        private void UpdateMixerConsoleView()
        {
            if (machine != null && machine.ParameterWindow != null)
            {
                int numMixerConsoles = connectionMixerMachine.MachineState.NumMixerConsoles;

                sp.Children.Clear();
                for (int i = 0; i < numMixerConsoles; i++)
                    sp.Children.Add(new MixerControl(connectionMixerMachine, i + 1, this) { VerticalAlignment = VerticalAlignment.Top });

                // Clear assignments
                for (int i = numMixerConsoles; i < ConnectionMixer.NUM_MIXERS; i++)
                {
                    connectionMixerMachine.ConnectionSelected(i, null);
                }

                if (numMixerConsoles >= 8)
                {
                    machine.ParameterWindow.MinWidth = 8 * 154 + 12;
                    machine.ParameterWindow.Width = machine.ParameterWindow.MinWidth;
                    machine.ParameterWindow.MaxWidth = Int32.MaxValue;
                }
                else
                {
                    machine.ParameterWindow.MinWidth = numMixerConsoles * 154 + 12;
                    machine.ParameterWindow.MaxWidth = machine.ParameterWindow.MinWidth;
                    machine.ParameterWindow.Width = machine.ParameterWindow.MinWidth;
                }
            }
        }

        private const int WM_NCLBUTTONDOWN = 0x00a1;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_NCLBUTTONDOWN:
                    Global.Buzz.MIDIFocusMachine = machine;
                    break;
            }

            return IntPtr.Zero;
        }

        private Menu CreateMainMenu()
        {
            Menu menu = new Menu() { Margin = new Thickness(2), Height = 20, IsMainMenu = true, Visibility = Visibility.Collapsed };

            MenuItem fileMenu = new MenuItem() { Header = "_File" };
            menu.Items.Add(fileMenu);

            fileMenu.Items.Add(new object());

            fileMenu.SubmenuOpened += (sender, e) =>
            {
                if (e.OriginalSource == fileMenu)
                {
                    fileMenu.Items.Clear();

                    MenuItem mi = new MenuItem() { Header = "_Load MIDI mapping..." };
                    mi.Click += Mi_Click_Load_MIDI_Mapping;
                    fileMenu.Items.Add(mi);
                    mi = new MenuItem() { Header = "_Save MIDI mapping..." };
                    mi.Click += Mi_Click_Save_MIDI_Mapping;
                    fileMenu.Items.Add(mi);
                    fileMenu.Items.Add(new Separator());

                    List<string> recentFiles = RegSettings.GetRecentFiles();

                    foreach (string file in recentFiles)
                    {
                        string header = Win32.CompactPath(file, 40);
                        mi = new MenuItem() { Header = header };
                        mi.Tag = file;
                        mi.Click += Mi_Click_Load_RecentFile;
                        fileMenu.Items.Add(mi);
                    }
                }
            };

            MenuItem otherMenu = new MenuItem() { Header = "_Other" };
            menu.Items.Add(otherMenu);

            MenuItem mi2 = new MenuItem() { Header = "_Clear All MIDI bindings..." };
            mi2.Click += Mi_Click_Reset_MIDI;
            otherMenu.Items.Add(mi2);

            mi2 = new MenuItem() { Header = "Always Soft Takeover on MIDI Focus", IsCheckable = true };
            mi2.IsChecked = RegSettings.IsAlwaysSoftTakeoverOnMIDIFocus();
            mi2.Checked += (s, e) =>
            {
                RegSettings.SetAlwaysSoftTakeoverOnMIDIFocus(true);
            };
            mi2.Unchecked += (s, e) =>
            {
                RegSettings.SetAlwaysSoftTakeoverOnMIDIFocus(false);
            };
            otherMenu.Items.Add(mi2);

            mi2 = new MenuItem() { Header = "Enable Mouse Wheel", IsCheckable = true };
            mi2.IsChecked = RegSettings.IsMouseWheelEnabled();
            mi2.Checked += (s, e) =>
            {
                RegSettings.SetMouseWheelEnabled(true);
            };
            mi2.Unchecked += (s, e) =>
            {
                RegSettings.SetMouseWheelEnabled(false);
            };
            otherMenu.Items.Add(mi2);

            mi2 = new MenuItem() { Header = "_Send values to MIDI Device on song load..." };
            mi2.Click += Mi_Click_Setup_External_MIDI_Device_On_Load;
            otherMenu.Items.Add(mi2);

            MenuItem minm = new MenuItem() { Header = "_Number of Mixer Controls" };
            minm.Icon = " # ";

            minm.Items.Add(new object());
            minm.SubmenuOpened += (sender, e) =>
            {
                if (e.OriginalSource == minm)
                {
                    minm.Items.Clear();
                    for (int i = 0; i < ConnectionMixer.NUM_MIXERS; i++)
                    {
                        mi2 = new MenuItem() { Header = i + 1 };
                        mi2.Tag = i + 1;
                        mi2.Icon = connectionMixerMachine.MachineState.NumMixerConsoles == i + 1 ? "✓" : "";
                        mi2.Click += Mi_Click_Number_of_Mixers;
                        minm.Items.Add(mi2);
                    }
                }
            };
            otherMenu.Items.Add(minm);

            return menu;
        }

        private void Mi_Click_Setup_External_MIDI_Device_On_Load(object sender, RoutedEventArgs e)
        {
            string decive = connectionMixerMachine.MachineState.MIDIOutDeviceName;
            MIDIOutWindow mow = new MIDIOutWindow(decive);
            if (mow.ShowDialog() == true)
            {
                connectionMixerMachine.MachineState.MIDIOutDeviceName = mow.GetSelectedDeviceName();
            }
        }

        private void Mi_Click_Number_of_Mixers(object sender, RoutedEventArgs e)
        {
            int num = (int)(sender as MenuItem).Tag;
            connectionMixerMachine.MachineState.NumMixerConsoles = num;
            UpdateMixerConsoleView();
        }

        private void Mi_Click_Reset_MIDI(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Are you sure?", "Clear MIDI Bindings", MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
            {
                connectionMixerMachine.UnbindAll();
            }
        }

        private void Mi_Click_Load_RecentFile(object sender, RoutedEventArgs e)
        {
            var mi = sender as MenuItem;
            string fileName = mi.Tag as string;
            connectionMixerMachine.LoadMIDIMapping(fileName);
        }

        private void Mi_Click_Save_MIDI_Mapping(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog saveFileDlg = new Microsoft.Win32.SaveFileDialog();

            saveFileDlg.DefaultExt = ".ini";
            saveFileDlg.Filter = "Text documents (.ini)|*.ini";

            // saveFileDlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            var result = saveFileDlg.ShowDialog();

            if (result == true)
            {
                connectionMixerMachine.SaveMIDIMapping(saveFileDlg.FileName);
            }
        }

        private void Mi_Click_Load_MIDI_Mapping(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog
            Microsoft.Win32.OpenFileDialog openFileDlg = new Microsoft.Win32.OpenFileDialog();

            openFileDlg.DefaultExt = ".ini";
            openFileDlg.Filter = "Text documents (.ini)|*.ini";

            openFileDlg.Multiselect = false;
            //openFileDlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Launch OpenFileDialog by calling ShowDialog method
            var result = openFileDlg.ShowDialog();

            // Get the selected file name and display in a TextBox.
            // Load content of file in a TextBlock
            if (result == true)
            {
                connectionMixerMachine.LoadMIDIMapping(openFileDlg.FileName);
            }
        }

        internal void SwitchMixers(int m1, int m2)
        {
            var mc1 = (MixerControl)sp.Children[m1];
            var mc2 = (MixerControl)sp.Children[m2];
            if (m1 < m2)
            {
                sp.Children.RemoveAt(m2);
                sp.Children.RemoveAt(m1);
                sp.Children.Insert(m1, mc2);
                sp.Children.Insert(m2, mc1);
            }
            else
            {
                sp.Children.RemoveAt(m1);
                sp.Children.RemoveAt(m2);
                sp.Children.Insert(m2, mc1);
                sp.Children.Insert(m1, mc2);
            }
            int mc1num = mc1.MixerNumber;
            mc1.MixerNumber = mc2.MixerNumber;
            mc2.MixerNumber = mc1num;
            mc1.MixerXontrolsChanged();
            mc2.MixerXontrolsChanged();
        }
    }
}
