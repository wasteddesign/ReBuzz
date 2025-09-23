using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzz.MachineManagement;
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReBuzz.NativeMachine
{
    internal class UIMessage : NativeMessage
    {
        private readonly Lock UIMessageLock = new();
        private readonly IUiDispatcher dispatcher;

        public UIMessage(ChannelType channel, MemoryMappedViewAccessor accessor, NativeMachineHost nativeMachineHost, IUiDispatcher dispatcher) : base(channel, accessor, nativeMachineHost)
        {
            this.dispatcher = dispatcher;
        }

        public override event EventHandler<EventArgs> MessageEvent;

        public override void ReceaveMessage()
        {
            DoReveiveIncomingMessage();
        }

        internal override void Notify()
        {
            MessageEvent?.Invoke(this, new MessageEventArgs() { MessageId = GetMessageId() });
        }

        public void SendMessageBuzzInitSync(IntPtr buzzHwnd, bool is64Bit)
        {
            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UIBuzzInit);
                SetMessageDataPtr(buzzHwnd);
                DoSendMessage();
            }
        }

        internal void UIDSPInitSync(int sampleRate)
        {
            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UIDSPInit);
                SetMessageData(sampleRate);
                DoSendMessage();
            }
        }

        internal bool UILoadLibrarySync(ReBuzzCore buzz, MachineCore machine, string libname, string path)
        {
            if (machine.DLL.IsCrashed)
            {
                return false;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UILoadLibrary);
                    SetMessageData(libname);
                    SetMessageData(path);
                    DoSendMessage();

                    IntPtr handle = GetMessageIntPtr(machine);

                    if (handle.ToInt64() == 0)
                    {
                        // Failed
                        buzz.DCWriteErrorLine(GetMessageString());
                        return false;
                    }
                    else
                    {
                        MachineDLL machineDLL = machine.MachineDLL;
                        machineDLL.ModuleHandle = handle;

                        var info = machineDLL.MachineInfo;
                        info.Type = (MachineType)GetMessageData<int>();

                        // Try to figure out the type some other way if machine returns weird number?
                        if ((int)info.Type > 2)
                        {   
                            buzz.DCWriteErrorLine("Load library failed: " + libname + " type is not valid: " + (int)info.Type);
                            return false;
                        }

                        info.Version = GetMessageData<int>();
                        info.Flags = (MachineInfoFlags)GetMessageData<int>();
                        info.MinTracks = GetMessageData<int>();
                        info.MaxTracks = GetMessageData<int>();
                        int numGlobalParameters = GetMessageData<int>();
                        int numTrackParameters = GetMessageData<int>();

                        // Global
                        ParameterGroup gpg = new ParameterGroup(machine, ParameterGroupType.Global);
                        machine.AddParameterGroup(gpg);
                        for (int i = 0; i < numGlobalParameters; i++)
                        {
                            gpg.AddParameter(ReadParameter(i));
                        }
                        if (gpg.Parameters.Count > 0)
                        {
                            gpg.TrackCount = 1;
                        }

                        // Track
                        ParameterGroup tpg = new ParameterGroup(machine, ParameterGroupType.Track);
                        machine.AddParameterGroup(tpg);
                        for (int i = 0; i < numTrackParameters; i++)
                        {
                            var p = ReadParameter(i);
                            tpg.AddParameter(p);
                        }

                        // Attributes
                        int numAttributes = GetMessageData<int>();
                        for (int i = 0; i < numAttributes; i++)
                        {
                            machine.AttributesList.Add(ReadAttribute(machine));
                        }

                        info.Name = GetMessageString();
                        info.ShortName = GetMessageString();
                        info.Author = GetMessageString();
                        string commands = GetMessageString();

                        bool cLibInreface = GetMessageByte() != 0;

                        machineDLL.Path = path;
                        machineDLL.Name = libname;
                        machineDLL.IsLoaded = true;
                        machine.TrackCount = info.MinTracks;

                        machine.SetParametersToDefaulValue();
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
                return false;
            }
        }
        internal void UINewMISync(MachineCore machine, string libname)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UINewMI);
                    SetMessageData(libname);
                    DoSendMessage();

                    machine.CMachinePtr = GetMessageIntPtr(machine);
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal void UIDeleteMI(MachineCore machine)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UIDeleteMI);
                    SetMessageDataPtr(machine.CMachinePtr);
                    DoSendMessage();
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal void UIInit(MachineCore machine, byte[] data)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UIInit);
                    WriteMasterInfo(machine);
                    SetMessageDataPtr(machine.CMachinePtr);
                    SetMessageData(machine.CMachineHost);
                    SetMessageData(data.Length);
                    SetMessageData(data);
                    DoSendMessage();
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal void UILoad(MachineCore machine, byte[] data)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (ReBuzzCore.AudioLock)
                {
                    lock (UIMessageLock)
                    {
                        Reset();
                        SetMessageData((int)UIMessages.UILoad);
                        SetMessageDataPtr(machine.CMachinePtr);
                        SetMessageData(data.Length);
                        SetMessageData(data);
                        DoSendMessage();
                    }
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal byte[] UISave(MachineCore machine)
        {
            if (machine.DLL.IsCrashed)
            {
                return null;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UISave);
                    SetMessageDataPtr(machine.CMachinePtr);
                    var msg = DoSendMessage();
                    byte[] retData = msg != null ? GetData() : null;
                    return retData;
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
                return null;
            }
        }

        internal void UIAttributesChanged(MachineCore machine)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UIAttributesChanged);
                    SetMessageDataPtr(machine.CMachinePtr);
                    DoSendMessage();
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal void UIStop(MachineCore machine)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UIStop);
                    SetMessageDataPtr(machine.CMachinePtr);
                    DoSendMessage();
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal void UICommand(MachineCore machine, int command)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UICommand);
                    SetMessageDataPtr(machine.CMachinePtr);
                    SetMessageData(command);
                    DoSendMessage();
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal void UIAddInput(MachineCore machine, string name, bool stereo)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UIAddInput);
                    SetMessageDataPtr(machine.CMachinePtr);
                    SetMessageData(name);
                    SetMessageData(stereo);
                    DoSendMessage();
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal void UIDeleteInput(MachineCore machine, string name)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UIDeleteInput);
                    SetMessageDataPtr(machine.CMachinePtr);
                    SetMessageData(name);
                    DoSendMessage();
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal void UIRenameInput(MachineCore machine, string oldName, string newName)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UIRenameInput);
                    SetMessageDataPtr(machine.CMachinePtr);
                    SetMessageData(oldName);
                    SetMessageData(newName);
                    DoSendMessage();
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal void UISetInputChannels(MachineCore machine, string name, bool stereo)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UISetInputChannels);
                    SetMessageDataPtr(machine.CMachinePtr);
                    SetMessageData(name);
                    SetMessageData(stereo ? (byte)1 : (byte)0);
                    DoSendMessage();
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal string UIDescribeValue(MachineCore machine, int param, int value)
        {
            if (machine.DLL.IsCrashed)
            {
                return null;
            }

            try
            {
                lock (UIMessageLock)
                {
                    string str = null;
                    Reset();
                    SetMessageData((int)UIMessages.UIDescribeValue);
                    SetMessageDataPtr(machine.CMachinePtr);
                    SetMessageData(param);
                    SetMessageData(value);
                    DoSendMessage();
                    bool success = GetMessageBool();
                    if (success)
                    {
                        str = GetMessageString();
                    }

                    return str;
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
                return null;
            }
        }

        internal byte[] UIHandleGUIMessage(MachineCore machine, byte[] msg)
        {
            if (machine.DLL.IsCrashed)
            {
                return null;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UIHandleGUIMessage);
                    SetMessageDataPtr(machine.CMachinePtr);
                    SetMessageData(msg);
                    DoSendMessage();

                    byte[] allData = GetData();
                    bool msgOut = allData[allData.Length - 1] == 1;
                    byte[] dataOut = null;
                    if (msgOut)
                    {
                        dataOut = allData.Take(allData.Length - 1).ToArray();
                    }
                    return dataOut;
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
                return null;
            }
        }

        internal void UIGetEnvelopeInfos(MachineCore machine)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UIGetEnvelopeInfos);
                    SetMessageDataPtr(machine.CMachinePtr);
                    DoSendMessage();
                    bool success = GetMessageByte() != 0;
                    if (success)
                    {
                        int count = GetMessageData<int>();
                        for (int i = 0; i < count; i++)
                        {
                            string name = GetMessageString();
                            int flags = GetMessageData<int>();
                            machine.envelopes.Add(name, new Envelope() { Flags = (byte)flags });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal IntPtr UIGetDLLPtr(MachineCore machine, string libname)
        {
            if (machine.DLL.IsCrashed)
            {
                return IntPtr.Zero;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UIGetDLLPtr);
                    SetMessageData(libname);
                    DoSendMessage();
                    var dllPtr = GetMessageIntPtr(machine);
                    return dllPtr;
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
                return IntPtr.Zero;
            }
        }

        internal List<string> UIGetInstrumentList(MachineCore machine, IntPtr dllPtr)
        {
            if (machine.DLL.IsCrashed)
            {
                return new List<string>();
            }

            try
            {
                lock (UIMessageLock)
                {
                    Dictionary<string, int> instrumentList = new Dictionary<string, int>();
                    Reset();
                    SetMessageData((int)UIMessages.UIGetInstrumentList);
                    SetMessageDataPtr(dllPtr);
                    DoSendMessage();

                    byte[] data = GetData();

                    string instrument = "";
                    for (int i = 0; i < data.Length; i++)
                    {
                        if (data[i] == 0 && instrument != "")
                        {
                            instrumentList[instrument] = 0;
                            instrument = "";
                        }
                        else
                        {
                            instrument += (char)data[i];
                        }
                    }

                    return instrumentList.Keys.ToList();
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
                return new List<string>();
            }
        }

        internal string UIGetInstrumentPath(MachineCore machine, IntPtr dllPtr, string instrumentName)
        {
            if (machine.DLL.IsCrashed || machine.DLL.Info.Version < MachineManager.BUZZ_MACHINE_INTERFACE_VERSION_42)
            {
                return null;
            }

            try
            {
                lock (UIMessageLock)
                {
                    string ret = null;
                    Reset();
                    SetMessageData((int)UIMessages.UIGetInstrumentPath);
                    SetMessageDataPtr(dllPtr);
                    SetMessageData(instrumentName);
                    DoSendMessage();
                    if (GetMessageBool())
                    {
                        ret = GetMessageString();
                    }

                    string s1 = "UIGetInstrumentPath for " + instrumentName.Trim() + ":";
                    int spaceCount = Math.Max(60 - s1.Length, 1);
                    s1 = s1.PadRight(s1.Length + spaceCount);
                    Global.Buzz.DCWriteLine(s1 + ret);
                    return ret;
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
                return null;
            }
        }

        internal bool UISetInstrument(MachineCore machine, string instrumentName)
        {
            if (machine.DLL.IsCrashed)
            {
                return false;
            }

            try
            {
                lock (UIMessageLock)
                {
                    Reset();
                    SetMessageData((int)UIMessages.UISetInstrument);
                    SetMessageDataPtr(machine.CMachinePtr);
                    SetMessageData(instrumentName);
                    DoSendMessage();
                    bool ret = GetMessageBool();

                    return ret;
                }
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
                return false;
            }
        }

        internal bool UISendEvent(MachineCore machine, CMachineEvent e, int data)
        {
            if (machine.DLL.IsCrashed)
            {
                return false;
            }

            try
            {
                lock (UIMessageLock)
                {
                    bool ret = false;
                    var bEvent = machine.CMachineEventType.FirstOrDefault(t => t.Type == e.Type);
                    if (bEvent.Event_Handler != null)
                    {
                        Reset();
                        SetMessageData((int)UIMessages.UIEvent);
                        SetMessageDataPtr(machine.CMachinePtr);
                        SetMessageDataPtr(e.Event_Handler);
                        SetMessageDataPtr(e.Param_Addr);
                        DoSendMessage();
                        ret = GetMessageBool(); // Handled?
                    }
                    return ret;
                }
            }
            catch (Exception ex)
            {
                MachineCrashed(machine, ex);
                return false;
            }
        }

        internal ParameterCore ReadParameter(int index)
        {
            lock (UIMessageLock)
            {
                ParameterCore parameter = new ParameterCore(dispatcher);
                parameter.Type = (ParameterType)GetMessageData<int>();
                parameter.Name = GetMessageString();
                parameter.Description = GetMessageString();
                parameter.MinValue = GetMessageData<int>();
                parameter.MaxValue = GetMessageData<int>();

                if (parameter.Type == ParameterType.Switch)
                {
                    parameter.MinValue = 0;
                    parameter.MaxValue = 1;
                }

                parameter.NoValue = GetMessageData<int>();
                parameter.Flags = (ParameterFlags)GetMessageData<int>();
                parameter.DefValue = GetMessageData<int>();
                parameter.IndexInGroup = index;
                parameter.SetValue(0, parameter.NoValue);
                return parameter;
            }
        }

        internal AttributeCore ReadAttribute(MachineCore machine)
        {
            lock (UIMessageLock)
            {
                AttributeCore attribute = new AttributeCore(machine);
                attribute.Name = GetMessageString();
                attribute.MinValue = GetMessageData<int>();
                attribute.MaxValue = GetMessageData<int>();
                attribute.DefValue = GetMessageData<int>();
                attribute.Value = attribute.DefValue;
                return attribute;
            }
        }

        internal void UIGetResources(MachineCore machine, out BitmapSource skinImage, out BitmapSource ledImage, out Point ledPosition)
        {
            skinImage = null; ledImage = null; ledPosition = (default);
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            try
            {
                Reset();
                SetMessageData((int)UIMessages.UIGetResources);
                SetMessageDataPtr(machine.CMachinePtr);
                DoSendMessage();

                skinImage = null;
                ledImage = null;

                // Skin
                if (GetMessageBool())
                {
                    int width = GetMessageData<int>();
                    int height = GetMessageData<int>();
                    int size = width * height * 4;
                    byte[] data = new byte[size];

                    // Decide between black or white text color based on skin image
                    int colorLightness = 0;

                    for (int i = 0; i < size; i++)
                    {
                        data[i] = GetMessageByte();
                        if ((i + 1) % 4 != 0)
                        {
                            colorLightness += data[i];
                        }
                    }

                    colorLightness /= (width * height * 3);

                    if (colorLightness < 128)
                    {
                        machine.MachineDLL.SkinTextColor = Colors.White;
                    }
                    else
                    {
                        machine.MachineDLL.SkinTextColor = Colors.Black;
                    }

                    skinImage = GetImageSource(data, size, width, height);
                }

                // SkinLed
                if (GetMessageBool())
                {
                    int width = GetMessageData<int>();
                    int height = GetMessageData<int>();
                    int size = width * height * 4;
                    byte[] data = new byte[size];
                    for (int i = 0; i < size; i++)
                    {
                        data[i] = GetMessageByte();
                    }

                    ledImage = GetImageSource(data, size, width, height);
                }

                int x = GetMessageByte();
                int y = GetMessageByte();
                ledPosition = new Point(x, y);
            }
            catch (Exception e)
            {
                MachineCrashed(machine, e);
            }
        }

        internal BitmapSource GetImageSource(byte[] data, int size, int width, int height)
        {
            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            System.Drawing.Imaging.BitmapData bmpData =
                        bmp.LockBits(new System.Drawing.Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadWrite,
            bmp.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Copy data to bitmap
            Marshal.Copy(data, 0, ptr, size);

            // Unlock the bits.
            bmp.UnlockBits(bmpData);
            var bmpHandle = bmp.GetHbitmap();
            BitmapSource s = Imaging.CreateBitmapSourceFromHBitmap(bmpHandle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DeleteObject(bmpHandle);
            return s;
        }

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);


        // MIF_PATTERN_EDITOR
        internal void UICreatePattern(MachineCore machine, PatternCore cPattern, int numrows)
        {
            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UICreatePattern);
                SetMessageDataPtr(machine.CMachinePtr);
                SetMessageDataPtr(cPattern.CPattern);
                SetMessageData(numrows);
                DoSendMessage();
            }
        }

        internal void UICreatePatternCopy(MachineCore machine, PatternCore cPatternNew, PatternCore cPatternOld)
        {
            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UICreatePatternCopy);
                SetMessageDataPtr(machine.CMachinePtr);
                SetMessageDataPtr(cPatternNew.CPattern);
                SetMessageDataPtr(cPatternOld.CPattern);
                DoSendMessage();
            }
        }

        internal void UIDeletePattern(MachineCore machine, PatternCore cPattern)
        {
            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UIDeletePattern);
                SetMessageDataPtr(machine.CMachinePtr);
                SetMessageDataPtr(cPattern.CPattern);
                DoSendMessage();
            }
        }

        internal void UIRenamePattern(MachineCore machine, PatternCore cPattern, string name)
        {
            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UIRenamePattern);
                SetMessageDataPtr(machine.CMachinePtr);
                SetMessageDataPtr(cPattern.CPattern);
                SetMessageData(name);
                DoSendMessage();
            }
        }
        internal void UISetPatternLength(MachineCore machine, PatternCore cPattern, int len)
        {
            Reset();
            SetMessageData((int)UIMessages.UISetPatternLength);
            SetMessageDataPtr(machine.CMachinePtr);
            SetMessageDataPtr(cPattern.CPattern);
            SetMessageData(len);
            DoSendMessage();
        }
        internal void UIPlayPattern(MachineCore machine, PatternCore cPattern, SequenceCore seqence, int playOffset)
        {
            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UIPlayPattern);
                SetMessageDataPtr(machine.CMachinePtr);
                SetMessageDataPtr(cPattern.CPattern);
                SetMessageDataPtr(seqence.CSequence);
                SetMessageData(playOffset);
                DoSendMessage();
            }
        }
        internal IntPtr UICreatePatternEditor(MachineCore machine, IntPtr parentHwnd)
        {
            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UICreatePatternEditor);
                SetMessageDataPtr(machine.CMachinePtr);
                SetMessageDataPtr(parentHwnd);
                DoSendMessage();
                IntPtr hwnd = GetMessageIntPtr(machine);
                return hwnd;
            }
        }
        internal void UISetEditorPattern(MachineCore machine, PatternCore cPattern)
        {
            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UISetEditorPattern);
                SetMessageDataPtr(machine.CMachinePtr);
                SetMessageDataPtr(cPattern.CPattern);
                DoSendMessage();
            }
        }
        internal void UIAddTrack(MachineCore machine)
        {
            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UIAddTrack);
                SetMessageDataPtr(machine.CMachinePtr);
                DoSendMessage();
            }
        }

        internal void UIDeleteLastTrack(MachineCore machine)
        {
            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UIDeleteLastTrack);
                SetMessageDataPtr(machine.CMachinePtr);
                DoSendMessage();
            }
        }

        internal bool UIEnableCommandUI(MachineCore machine, int id)
        {
            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UIEnableCommandUI);
                SetMessageDataPtr(machine.CMachinePtr);
                SetMessageData(id);
                DoSendMessage();
                bool ret = GetMessageBool();
                return ret;
            }
        }

        internal void UIDrawPatternBox(MachineCore m)
        {
            // Not supported
        }

        internal void UISetPatternTargetMachine(MachineCore machine, PatternCore cPattern, MachineCore targetMachine)
        {
            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UISetPatternTargetMachine);
                SetMessageDataPtr(machine.CMachinePtr);
                SetMessageDataPtr(cPattern.CPattern);
                SetMessageDataPtr(targetMachine.CMachinePtr);
                DoSendMessage();
            }
        }

        internal string UIGetChannelName(MachineCore machine, bool input, int index)
        {
            if (machine.DLL.IsCrashed)
            {
                return null;
            }

            lock (UIMessageLock)
            {
                string ret = null;
                Reset();
                SetMessageData((int)UIMessages.UIGetChannelName);
                SetMessageDataPtr(machine.CMachinePtr);
                SetMessageData(input);
                SetMessageData(index);
                DoSendMessage();
                bool nameFound = GetMessageBool();
                if (nameFound)
                {
                    ret = GetMessageString();
                }
                return ret;
            }
        }

        internal void UIGotMidiFocus(MachineCore machine)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UIGotMidiFocus);
                SetMessageDataPtr(machine.CMachinePtr);
                DoSendMessage();
            }
        }

        internal void UILostMidiFocus(MachineCore machine)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UILostMidiFocus);
                SetMessageDataPtr(machine.CMachinePtr);
                DoSendMessage();
            }
        }

        internal string[] GetSubMenu(MachineCore machine, int index)
        {
            if (machine.DLL.IsCrashed)
            {
                return Array.Empty<string>();
            }

            lock (UIMessageLock)
            {
                string[] ret;
                Reset();
                SetMessageData((int)UIMessages.UIGetSubMenu);
                SetMessageDataPtr(machine.CMachinePtr);
                SetMessageData(index);
                DoSendMessage();

                byte[] allData = GetData();
                var str = System.Text.Encoding.Default.GetString(allData);
                ret = str.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < ret.Length; i++)
                {
                    ret[i] = ret[i].Trim();
                }

                return ret;
            }
        }

        internal void ImportFinished(MachineCore machine)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UIImportFinished);
                SetMessageDataPtr(machine.CMachinePtr);
                DoSendMessage();
            }
        }

        internal bool ImplementsFunction(MachineCore machine, string str)
        {
            if (machine.DLL.IsCrashed)
            {
                return false;
            }

            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UIImplementsFunction);
                SetMessageDataPtr(machine.CMachinePtr);
                SetMessageData(str);
                DoSendMessage();
                return GetMessageBool();
            }
        }

        internal IEnumerable<IMenuItem> GetCommands(MachineCore machine)
        {
            if (machine.DLL.IsCrashed)
            {
                return null;
            }

            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UIGetCommands);
                SetMessageDataPtr(machine.CMachinePtr);
                DoSendMessage();
                string commands = GetMessageString();
                machine.SetCommands(commands);
                return null;
            }
        }

        internal void UIRemapMachineNames(MachineCore machine, IDictionary<string, string> dict)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            lock (UIMessageLock)
            {
                Reset();
                SetMessageData((int)UIMessages.UIRemapMachineNames);
                SetMessageDataPtr(machine.CMachinePtr);
                int count = dict.Count;
                SetMessageData(count);
                foreach (var dk in dict.Keys)
                {
                    SetMessageData(dk);
                    SetMessageData(dict[dk]);
                }
                DoSendMessage();
            }
        }

        internal void UpdateWaveReferences(MachineCore machine, MachineCore editorTargetMachine, Dictionary<int, int> remappedWaveReferences)
        {
        }
    }
}
