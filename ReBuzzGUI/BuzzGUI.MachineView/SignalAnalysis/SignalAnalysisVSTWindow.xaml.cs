using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using Jacobi.Vst.Core;
using Jacobi.Vst.Host.Interop;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace BuzzGUI.MachineView.SignalAnalysis
{
    public class InstrumentVM
    {
        public IInstrument Instrument { get; set; }
        public InstrumentVM(IInstrument instrument)
        {
            Instrument = instrument;
        }

        public string DisplayName
        {
            get
            {
                return System.IO.Path.GetFileName(Instrument.Name);
            }
        }
    }

    /// <summary>
    /// Interaction logic for SignalAnalysisVSTWindow.xaml
    /// </summary>
    public partial class SignalAnalysisVSTWindow : Window, INotifyPropertyChanged
    {
        public IMachine Machine { get; private set; }

        Thread threadVST;
        DispatcherTimer dtPluginUI;

        InstrumentVM selectedVSTInstrument;
        public InstrumentVM SelectedVSTInstrument
        {
            get
            {
                return selectedVSTInstrument;
            }
            set
            {
                if (selectedVSTInstrument != null)
                {
                    if (savst != null)
                    {
                        vstReady = false;
                        closingVST = true;
                        if (threadVST.ThreadState == ThreadState.Running)
                            threadVST.Join();

                        CloseVST();

                        if (controlHost != null)
                        {
                            controlHost.PropertyChanged -= ControlHost_PropertyChanged;
                            //controlHost.Release();
                        }
                    }
                }
                selectedVSTInstrument = value;
                if (selectedVSTInstrument != null)
                {
                    try
                    {
                        // Double check bitness
                        if (UnmanagedDllIs64Bit(selectedVSTInstrument.Instrument.Path) == Environment.Is64BitProcess)
                        {
                            LoadVST(selectedVSTInstrument.Instrument.Path);
                        }
                    }
                    catch (Exception e)
                    {
                        selectedVSTInstrument = null;
                        Global.Buzz.DCWriteLine("Error loading plugin: " + e);
                    }
                    UpdateTitle();
                    PropertyChanged.Raise(this, "SelectedVSTInstrument");
                }
            }
        }

        readonly string REGISTRY_DEFAULT_KEY = "DefaultSignalAnalysisVST";
        private void SetDefaultVST()
        {
            var t = GetType();
            string registryLocation = Global.RegistryRoot + "BuzzGUI\\" + t.Name;

            RegistryKey key = Registry.CurrentUser.OpenSubKey(registryLocation, true);
            if (key == null) key = Registry.CurrentUser.CreateSubKey(registryLocation);

            if (savst != null)
            {
                key.SetValue(REGISTRY_DEFAULT_KEY, SelectedVSTInstrument.Instrument.Path);
            }
            else
            {
                Registry.CurrentUser.DeleteSubKey(REGISTRY_DEFAULT_KEY);
            }
        }

        private string GetDefaultVST()
        {
            string ret = null;
            var t = GetType();
            string registryLocation = Global.RegistryRoot + "BuzzGUI\\" + t.Name;

            RegistryKey key = Registry.CurrentUser.OpenSubKey(registryLocation, true);
            if (key != null)
            {
                ret = (string)key.GetValue(REGISTRY_DEFAULT_KEY, null);
            }
            return ret;
        }

        private void UpdateTitle()
        {
            string t = "Signal Analysis: " + Machine.Name;
            if (SelectedConnection != null)
                t += " -> " + SelectedConnection.Destination.Name;
            Title = t;
        }

        private IMachineConnection selectedConnection;
        private bool masterTap;

        public IMachineConnection SelectedConnection
        {
            get { return selectedConnection; }
            set
            {
                if (selectedConnection != null)
                {
                    selectedConnection.Tap -= SelectedConnection_Tap;
                }
                else if (masterTap)
                {
                    Global.Buzz.MasterTap -= SelectedConnection_Tap;
                    selectedConnection = value;
                    masterTap = false;
                    return;
                }

                selectedConnection = value;
                if (selectedConnection != null)
                {
                    selectedConnection.Tap += SelectedConnection_Tap;
                    UpdateTitle();
                }
                else if (Machine.Name == "Master")
                {
                    masterTap = true;
                    Global.Buzz.MasterTap += SelectedConnection_Tap;
                    UpdateTitle();
                }
            }
        }

        private void SelectedConnection_Tap(float[] samples, bool stereo, SongTime songTime)
        {
            lock (syncLock)
            {
                if (vstReady)
                {
                    taskList.Add(samples);
                }
            }

            /*
            if (savst != null && savst.Ctx != null)
            {
                var PluginContext = savst.Ctx;
                if ((PluginContext.PluginInfo.Flags & VstPluginFlags.CanReplacing) == 0)
                {   
                    return;
                }

                int inputCount = 2;// PluginContext.PluginInfo.AudioInputCount;
                int outputCount = 2;// PluginContext.PluginInfo.AudioOutputCount;

                int bufferSize = samples.Length / 2;

                // wrap these in using statements to automatically call Dispose and cleanup the unmanaged memory.
                using (VstAudioBufferManager inputMgr = new VstAudioBufferManager(inputCount, bufferSize))
                {
                    using (VstAudioBufferManager outputMgr = new VstAudioBufferManager(outputCount, bufferSize))
                    {
                        int channel = 0;
                        foreach (VstAudioBuffer buffer in inputMgr)
                        {
                            float scale = (1.0f / 32768.0f);
                            for (int i = 0; i < samples.Length / 2; i++)
                            {   
                                buffer[i] = samples[i * 2 + channel] * scale;
                            }
                            channel++;
                        }

                        VstAudioBuffer[] inputBuffers = inputMgr.ToArray<VstAudioBuffer>();
                        VstAudioBuffer[] outputBuffers = outputMgr.ToArray<VstAudioBuffer>();

                        PluginContext.PluginCommandStub.ProcessReplacing(inputBuffers, outputBuffers);
                    }
                }
            }
            */
        }

        SignalAnalysisVST savst;

        List<InstrumentVM> vstInstruments = new List<InstrumentVM>();
        public List<InstrumentVM> VSTInstruments { get => vstInstruments; set { vstInstruments = value; PropertyChanged.Raise(this, "VSTInstruments"); } }

        public event PropertyChangedEventHandler PropertyChanged;

        ControlHost controlHost;
        private bool ready;

        public SignalAnalysisVSTWindow(IMachine machine)
        {
            this.Machine = machine;
            InitializeComponent();
            DataContext = this;

            Machine.Graph.ConnectionAdded += (obj) =>
            {
                PropertyChanged.Raise(this, "Machine");
            };

            Machine.Graph.ConnectionRemoved += (obj) =>
            {
                if (!masterTap)
                {
                    if (Machine.Outputs.FirstOrDefault(x => x == SelectedConnection) == null)
                        SelectedConnection = Machine.Outputs.FirstOrDefault();
                    PropertyChanged.Raise(this, "Machine");
                }
            };

            SelectedConnection = Machine.Outputs.FirstOrDefault();
            PropertyChanged.Raise(this, "Machine");

            Global.Buzz.Song.MachineRemoved += (obj) =>
            {
                if (Machine == obj)
                {
                    this.Close();
                }
            };

            this.Closed += (sender, e) =>
            {
                SelectedVSTInstrument = null;
            };

            string exclude = "(x86)";
            if (!Environment.Is64BitProcess)
                exclude = "(x64)";

            foreach (var inst in Global.Buzz.Instruments)
            {
                if (inst.Name.Length > 0 &&
                    inst.Type == InstrumentType.Effect &&
                    !inst.Name.Contains(exclude))
                {
                    vstInstruments.Add(new InstrumentVM(inst));
                }
            }


            VSTInstruments = vstInstruments.OrderBy(x => x.DisplayName).ToList();

            string defaultVST = GetDefaultVST();
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                SelectedVSTInstrument = null;
            }
            else if (defaultVST != null)
            {
                SelectedVSTInstrument = VSTInstruments.FirstOrDefault(x => x.Instrument.Path == defaultVST);
            }

            this.Closed += (sender, e) =>
            {
                //controlHost.Release();
                SelectedVSTInstrument = null;
            };

            btDefaultVST.Click += (sender, e) =>
            {
                SetDefaultVST();
            };

            cbvst.PreviewKeyDown += (sender, e) =>
            {
                e.Handled = true;
            };

            /*
            btDefault.Click += (sender, e) =>
            {
                if (savst != null && savst.Ctx != null)
                {
                    var PluginContext = savst.Ctx;
                }
            };
            */
        }

        internal void LoadVST(string path)
        {
            closingVST = false;
            savst = new SignalAnalysisVST(selectedVSTInstrument.Instrument.Path);
            controlHost = new ControlHost(400, 400, savst);
            controlHost.PropertyChanged += ControlHost_PropertyChanged;
            controlHost.Loaded += (sender, args) =>
            {
                ready = true;
                SetSize();
            };
            controlHost.SizeChanged += (sender, args) =>
            {
                // Hack for editors that can change theirAeady)
                if (!ready)
                    return;
                SetSize();
            };
            borderControlHost.Child = controlHost;

            threadVST = new Thread(ThreadVSTCallback);
            threadVST.Priority = ThreadPriority.Normal;
            taskList.Clear();
            threadVST.Start();
        }

        public enum MachineType : ushort
        {
            IMAGE_FILE_MACHINE_UNKNOWN = 0x0,
            IMAGE_FILE_MACHINE_AMD64 = 0x8664,
            IMAGE_FILE_MACHINE_I386 = 0x14c,
            IMAGE_FILE_MACHINE_IA64 = 0x200
        }

        public static bool UnmanagedDllIs64Bit(string dllPath)
        {
            switch (GetDllMachineType(dllPath))
            {
                case MachineType.IMAGE_FILE_MACHINE_AMD64:
                case MachineType.IMAGE_FILE_MACHINE_IA64:
                    return true;
                default:
                    return false;
            }
        }

        public static MachineType GetDllMachineType(string dllPath)
        {
            // See http://www.microsoft.com/whdc/system/platform/firmware/PECOFF.mspx
            // Offset to PE header is always at 0x3C.
            // The PE header starts with "PE\0\0" =  0x50 0x45 0x00 0x00,
            // followed by a 2-byte machine type field (see the document above for the enum).
            //
            using (var fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                fs.Seek(0x3c, SeekOrigin.Begin);
                Int32 peOffset = br.ReadInt32();

                fs.Seek(peOffset, SeekOrigin.Begin);
                UInt32 peHead = br.ReadUInt32();

                if (peHead != 0x00004550) // "PE\0\0", little-endian
                    throw new Exception("Can't find PE header");

                return (MachineType)br.ReadUInt16();
            }
        }

        private void CloseVST()
        {
            dtPluginUI.Stop();
            StopAudioStreamProcess();
            var PluginContext = savst.Ctx;
            if (PluginContext != null)
            {
                ((HostCommandStub)PluginContext.HostCommandStub).PluginCalled -= SignalAnalysisVSTWindow_PluginCalled;
                PluginContext.PluginCommandStub.Commands.EditorClose();
                PluginContext.PluginCommandStub.Commands.Close();
            }

            savst = null;
        }

        readonly List<float[]> taskList = new List<float[]>();
        bool closingVST;
        private bool vstReady;
        private readonly object syncLock = new object();
        private void ThreadVSTCallback()
        {
            while (!closingVST)
            {
                float[] samples = null;
                lock (syncLock)
                {
                    if (taskList.Count > 0)
                    {
                        samples = taskList.First();
                        taskList.RemoveAt(0);
                    }
                }

                if (samples != null && vstReady)
                {
                    if (savst != null && savst.Ctx != null)
                    {
                        var PluginContext = savst.Ctx;
                        if ((PluginContext.PluginInfo.Flags & VstPluginFlags.CanReplacing) == 0)
                        {
                            vstReady = false;
                        }

                        int inputCount = PluginContext.PluginInfo.AudioInputCount;
                        int outputCount = PluginContext.PluginInfo.AudioOutputCount;

                        int bufferSize = samples.Length / 2;
                        PluginContext.PluginCommandStub.Commands.SetBlockSize(bufferSize);

                        // wrap these in using statements to automatically call Dispose and cleanup the unmanaged memory.
                        using (VstAudioBufferManager inputMgr = new VstAudioBufferManager(inputCount, bufferSize))
                        {
                            using (VstAudioBufferManager outputMgr = new VstAudioBufferManager(outputCount, bufferSize))
                            {
                                int channel = 0;
                                foreach (VstAudioBuffer buffer in inputMgr.Buffers)
                                {
                                    if (channel < 2)
                                    {
                                        float scale = (1.0f / 32768.0f);
                                        for (int i = 0; i < bufferSize; i++)
                                        {
                                            buffer[i] = samples[i * 2 + channel] * scale;
                                        }
                                        channel++;
                                    }
                                    else
                                    {
                                        for (int i = 0; i < bufferSize; i++)
                                        {
                                            buffer[i] = 0;
                                        }
                                    }
                                }

                                VstAudioBuffer[] inputBuffers = inputMgr.Buffers.ToArray<VstAudioBuffer>();
                                VstAudioBuffer[] outputBuffers = outputMgr.Buffers.ToArray<VstAudioBuffer>();
                                PluginContext.PluginCommandStub.Commands.ProcessReplacing(inputBuffers, outputBuffers);
                            }
                        }
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }

        private void SetSize()
        {
            borderControlHost.Width = controlHost.Width;
            borderControlHost.Height = controlHost.Height;
            this.SizeToContent = SizeToContent.WidthAndHeight;
        }

        private void ControlHost_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "WndHostReady")
            {
                var context = savst.OpenPlugin(controlHost.GetHostHwnd());
                if (context != null)
                {
                    ((HostCommandStub)context.HostCommandStub).PluginCalled += SignalAnalysisVSTWindow_PluginCalled;
                    CheckSize();
                    StartAudioStreamProcess();

                    // Hack to resize machines that don't send resize event
                    dtPluginUI = new DispatcherTimer();
                    dtPluginUI.Interval = TimeSpan.FromMilliseconds(1000 / 30);
                    dtPluginUI.Tick += (se, ev) =>
                    {
                        if (savst != null && !closingVST && savst.Ctx != null)
                        {
                            //dt.Stop();
                            CheckSize();
                            context.PluginCommandStub.Commands.EditorIdle(); // update UI
                        }
                    };
                    dtPluginUI.Start();
                }
                else
                {
                    closingVST = true;
                    vstReady = false;
                    if (threadVST != null && threadVST.ThreadState == ThreadState.Running)
                        threadVST.Join();

                    if (controlHost != null)
                    {
                        controlHost.PropertyChanged -= ControlHost_PropertyChanged;
                        //controlHost.Release();
                    }

                    //CloseVST();
                    borderControlHost.Child = new Label() { Content = "'" + selectedVSTInstrument.DisplayName.Trim() + "' could not be loaded." };
                    selectedVSTInstrument = null;
                }
            }
        }

        private void SignalAnalysisVSTWindow_PluginCalled(object sender, PluginCalledEventArgs e)
        {
            if (e.Message == "SizeWindow")
            {
                CheckSize();
            }
        }


        private void CheckSize()
        {
            var wndRect = new System.Drawing.Rectangle();
            if (((HostCommandStub)savst.Ctx.HostCommandStub).Status >= 0 && savst.Ctx.PluginCommandStub.Commands.EditorGetRect(out wndRect))
            {
                controlHost.Width = wndRect.Width / WPFExtensions.PixelsPerDip;
                controlHost.Height = wndRect.Height / WPFExtensions.PixelsPerDip;
                wpmain.Width = controlHost.Width;
            }
        }

        internal void StartAudioStreamProcess()
        {
            var PluginContext = savst.Ctx;
            if (PluginContext != null)
            {
                //PluginContext.PluginCommandStub.SetBlockSize(256);
                PluginContext.PluginCommandStub.Commands.SetSampleRate(Global.Buzz.SelectedAudioDriverSampleRate);
                PluginContext.PluginCommandStub.Commands.MainsChanged(true); // Turn plugin on
                PluginContext.PluginCommandStub.Commands.StartProcess();
                //PluginContext.PluginCommandStub.SetBlockSize(blockSize);
                lock (syncLock)
                {
                    taskList.Clear();
                    vstReady = true;
                }
            }
        }

        internal void StopAudioStreamProcess()
        {
            vstReady = false;
            var PluginContext = savst.Ctx;
            if (PluginContext != null)
            {
                PluginContext.PluginCommandStub.Commands.StopProcess();
                PluginContext.PluginCommandStub.Commands.MainsChanged(false); // Plugin off
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    }
}
