using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ReBuzz.Audio;
using ReBuzz.Common;
using ReBuzz.Core;
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ReBuzz.NativeMachine
{
    public class MessageEventArgs : EventArgs
    {
        public int MessageId { get; set; }
        public object Data { get; set; }
    }

    public class MessageContent
    {
        public List<byte> ReceaveMessageData { get; }
        public MachineCore Machine { get; internal set; }

        public MessageContent()
        {
            ReceaveMessageData = new List<byte>();
        }
    }

    internal unsafe abstract class NativeMessage
    {
        ChannelListener channelListener;
        public ChannelListener ChannelListener
        {
            get
            {
                return channelListener;
            }
            set
            {
                channelListener = value;
            }
        }

        public ChannelType Channel { get; set; }
        public MemoryMappedViewAccessor Accessor { get; }
        public NativeMachineHost NativeHost { get; }

        internal int stateOffset;
        internal int dataRootOffset;
        private readonly int dataSizeOffset;
        private readonly int callbackOffset;
        internal int offset;

        internal byte* statePointer;
        internal byte* dataRootPointer;
        private readonly byte* dataSizePointer;
        private readonly byte* callbackPointer;

        // 1. First read/write message to lists
        // 2. When data fully read/written, parse data from lists

        readonly List<byte> sendMessageData = new List<byte>();
        byte[] receaveMessageTable;

        readonly MessageContent messageContent = new MessageContent();

        public abstract event EventHandler<EventArgs> MessageEvent;

        public unsafe NativeMessage(ChannelType channel, MemoryMappedViewAccessor accessor, NativeMachineHost nativeMachineHost)
        {
            Channel = channel;
            Accessor = accessor;
            NativeHost = nativeMachineHost;
            stateOffset = IPC.GetChannelSharedMemStateOffset(channel);
            dataRootOffset = IPC.GetChannelSharedMemDataOffset(channel);
            dataSizeOffset = IPC.GetChannelSharedMemSizeOffset(channel);
            callbackOffset = IPC.GetChannelSharedMemCallbackOffset(channel);

            byte* basePointer = null;
            Accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref basePointer);
            dataRootPointer = basePointer + dataRootOffset;
            callbackPointer = basePointer + callbackOffset;
            dataSizePointer = basePointer + dataSizeOffset;
            statePointer = basePointer + stateOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DoReveiveIncomingMessage()
        {
            if (IsCallback())
            {
                // Callback message has been sent so read it and act
                var messageContent = ReadMessage();
                if (messageContent.ReceaveMessageData.Count > 0)
                    HandleIncomingMessage(messageContent);
            }
            else
            {
                var messageContent = ReadMessage();
                if (messageContent.ReceaveMessageData.Count > 0)
                    HandleIncomingMessage(messageContent);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetMessageId()
        {
            return *(int*)dataSizePointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ChannelState GetChannelState()
        {
            return (ChannelState)(*(int*)statePointer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetChannelState(ChannelState state)
        {
            *(int*)statePointer = (int)state;
        }

        public bool CopyMessageToSendBuffer()
        {
            bool done = false;
            int size = sendMessageData.Count();

            if (size > MessageBuffer.MaxSize)
            {
                fixed (byte* source = sendMessageData.ToArray())
                {
                    Unsafe.CopyBlock(dataRootPointer, source, (uint)MessageBuffer.MaxSize);
                }

                sendMessageData.RemoveRange(0, MessageBuffer.MaxSize);
                size = MessageBuffer.MaxSize;

                SetChannelState(ChannelState.sendbuffer);
            }
            else
            {
                fixed (byte* source = sendMessageData.ToArray())
                {
                    Unsafe.CopyBlock(dataRootPointer, source, (uint)size);
                }

                done = true;
                SetChannelState(ChannelState.sendlastbuffer);
            }

            SetMessageSize(size);
            return done;
        }

        public MessageContent DoSendMessage(MachineCore machine)
        {
            return DoSendMessage();
            /*
            MessageContent msg;
            while (true)
            {
                // Done
                if (CopyMessageToSendBuffer())
                {
                    ChannelListener.WaitHandlePing.Set();

                    if (!ChannelListener.WaitHandlePongWaitOne(machine, 2000)) // Wait reply. If nothing happens in 2 seconds, consider crashed
                    {
                        NativeHost.Dispose();
                        return null;
                    }

                    msg = DoReceiveReply(); // Reply read
                    msg.Machine = machine;
                    if (IsCallback())
                    {
                        HandleIncomingMessage(msg);
                        msg = DoReceiveReply(); // Read 
                    }
                    break;
                }
                // Not done
                else
                {
                    ChannelListener.WaitHandlePing.Set();
                    if (!ChannelListener.WaitHandlePongWaitOne(machine, 2000)) // Wait reply
                    {
                        NativeHost.Dispose();
                        return null;
                    }
                }
            }

            receaveMessageTable = msg.ReceaveMessageData.ToArray();
            return msg;
            */
        }

        public MessageContent DoSendMessage()
        {
            MessageContent msg;
            while (true)
            {
                // Done
                if (CopyMessageToSendBuffer())
                {
                    ChannelListener.WaitHandlePing.Set();
                    ChannelListener.WaitHandlePongWaitOne();

                    msg = DoReceiveReply(); // Reply read
                    if (IsCallback())
                    {
                        HandleIncomingMessage(msg);
                        msg = DoReceiveReply(); // Read 
                    }
                    break;
                }
                // Not done
                else
                {
                    ChannelListener.WaitHandlePing.Set();
                    ChannelListener.WaitHandlePongWaitOne(); // Wait reply
                }
            }

            receaveMessageTable = msg.ReceaveMessageData.ToArray();
            return msg;
        }

        public MessageContent DoReceiveReply()
        {
            var receaveMessageData = messageContent.ReceaveMessageData;
            receaveMessageData.Clear();
            offset = 0;

            while (true)
            {
                var state = GetChannelState();
                if (state == ChannelState.replylastbuffer)
                {
                    int size = GetMessageSize();
                    byte[] bytes = ReadMessageBytes(size);
                    receaveMessageData.AddRange(bytes);
                    break;
                }
                else if (state == ChannelState.replybuffer)
                {
                    int size = GetMessageSize();
                    byte[] bytes = ReadMessageBytes(size);
                    receaveMessageData.AddRange(bytes);

                    // Request more
                    SetChannelState(ChannelState.sendbuffer);
                    SetMessageSize(0);
                    ChannelListener.WaitHandlePing.Set();
                    ChannelListener.WaitHandlePong.WaitOne();
                }
                else if (state == ChannelState.sendbuffer)
                {
                    int size = GetMessageSize();
                    byte[] bytes = ReadMessageBytes(size);
                    receaveMessageData.AddRange(bytes);

                    // Request more
                    SetChannelState(ChannelState.replybuffer);
                    ChannelListener.WaitHandlePong.Set();
                    ChannelListener.WaitHandlePing.WaitOne();
                }
                else if (state == ChannelState.sendlastbuffer)
                {
                    int size = GetMessageSize();
                    byte[] bytes = ReadMessageBytes(size);
                    receaveMessageData.AddRange(bytes);
                    break;
                }
            }
            return messageContent;
        }
        public bool CopyReplyMessageToSendBuffer()
        {
            int size = sendMessageData.Count();
            bool done = false;

            if (size > MessageBuffer.MaxSize)
            {
                fixed (byte* source = sendMessageData.ToArray())
                {
                    Unsafe.CopyBlock(dataRootPointer, source, (uint)MessageBuffer.MaxSize);
                }

                sendMessageData.RemoveRange(0, MessageBuffer.MaxSize);
                size = MessageBuffer.MaxSize;

                if (IsCallback())
                {
                    SetChannelState(ChannelState.sendbuffer);
                }
                else
                {
                    SetChannelState(ChannelState.replybuffer);
                }
            }
            else
            {
                fixed (byte* source = sendMessageData.ToArray())
                {
                    Unsafe.CopyBlock(dataRootPointer, source, (uint)size);
                }

                done = true;
                if (IsCallback())
                {
                    SetChannelState(ChannelState.sendlastbuffer);
                }
                else
                {
                    SetChannelState(ChannelState.replylastbuffer);
                }
            }

            SetMessageSize(size);
            return done;
        }

        public void DoReplyMessage()
        {
            if (IsCallback())
            {
                while (true)
                {
                    // Send signal for BE to handle callback response
                    bool done = CopyReplyMessageToSendBuffer();

                    ChannelListener.WaitHandlePing.Set();
                    ChannelListener.WaitHandlePong.WaitOne();

                    if (IsCallback())
                    {
                        var messageContent = ReadMessage();
                        if (messageContent.ReceaveMessageData.Count > 0)
                            HandleIncomingMessage(messageContent);
                    }
                    if (done)
                        break;
                }
            }
            else
            {
                while (true)
                {
                    bool done = CopyReplyMessageToSendBuffer();

                    ChannelListener.WaitHandlePong.Set();
                    ChannelListener.WaitHandlePong.Reset();

                    if (done)
                        break;
                }
            }
        }

        public MessageContent ReadMessage()
        {
            var receaveMessageData = messageContent.ReceaveMessageData;
            // 1. Ping signaled, fetch messageData
            receaveMessageData.Clear();
            offset = 0;

            while (true)
            {
                var state = GetChannelState();
                if (state == ChannelState.replylastbuffer)
                {
                    int size = GetMessageSize();
                    byte[] bytes = ReadMessageBytes(size);
                    if (bytes.Length > 0)
                        receaveMessageData.AddRange(bytes);
                    break;
                }
                else if (state == ChannelState.replybuffer)
                {
                    int size = GetMessageSize();
                    byte[] bytes = ReadMessageBytes(size);
                    receaveMessageData.AddRange(bytes);

                    SetChannelState(ChannelState.sendbuffer);

                    ChannelListener.WaitHandlePong.Set();
                    ChannelListener.WaitHandlePing.WaitOne();
                }
                // Message from buzzengine
                else if (state == ChannelState.sendbuffer)
                {
                    int size = GetMessageSize();
                    byte[] bytes = ReadMessageBytes(size);
                    receaveMessageData.AddRange(bytes);

                    SetChannelState(ChannelState.replybuffer);

                    ChannelListener.WaitHandlePong.Set();
                    ChannelListener.WaitHandlePing.WaitOne();
                }
                else if (state == ChannelState.sendlastbuffer)
                {
                    // Last buffer sent, so copy last part and exit loop
                    int size = GetMessageSize();
                    byte[] bytes = ReadMessageBytes(size);
                    receaveMessageData.AddRange(bytes);
                    break;
                }
            }

            return messageContent;
        }

        readonly object hostMessageLock = new object();
        // Messages from buzzengine are HostMessages.
        public void HandleIncomingMessage(MessageContent msg)
        {
            //lock (hostMessageLock)
            {
                this.receaveMessageTable = msg.ReceaveMessageData.ToArray();
                offset = 0;
                HostMessages uiMessageID = (HostMessages)GetMessageData<int>();
                switch (uiMessageID)
                {
                    case HostMessages.HostDCWriteLine:
                        {
                            string text = GetMessageString();
                            //Global.Buzz.DCWriteLine(text);
                            Reset();
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostInitMIEx:
                        {
                            long hostId = GetMessageData<long>();

                            Reset();
                            SetMessageData((int)HostMessages.HostInitMIEx);
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostInitMDK:
                        {
                            Reset();
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostSetnumOutputChannels:
                        {
                            // if n=1 Work(), n=2 WorkMonoToStereo()
                            long hostId = GetMessageData<long>();
                            int n = GetMessageData<int>();
                            Reset();
                            DoReplyMessage();

                            var machine = channelListener.buzz.GetMachineFromHostID(hostId);
                            if (machine != null)
                            {
                                if (n == 2)
                                {
                                    machine.HasStereoOutput = true;
                                }
                                else
                                {
                                    machine.HasStereoOutput = false;
                                }
                            }
                        }
                        break;
                    case HostMessages.HostSetInputChannelCount:
                        {
                            // MULTI_IO
                            long hostId = GetMessageData<long>();
                            int count = GetMessageData<int>();
                            Reset();
                            DoReplyMessage();
                            var machine = channelListener.buzz.GetMachineFromHostID(hostId);
                            if (machine != null)
                            {
                                machine.InputChannelCount = count;
                            }
                        }
                        break;
                    case HostMessages.HostSetOutputChannelCount:
                        {
                            // MULTI_IO
                            long hostId = GetMessageData<long>();
                            int count = GetMessageData<int>();
                            var machine = channelListener.buzz.GetMachineFromHostID(hostId);
                            Reset();
                            DoReplyMessage();

                            if (machine != null)
                            {
                                machine.OutputChannelCount = count;
                            }
                        }
                        break;
                    case HostMessages.HostGetMachineName:
                        {
                            long hostId = GetMessageData<long>();
                            var machine = channelListener.buzz.GetMachineFromHostID(hostId);

                            string name = "";
                            if (machine != null)
                            {
                                name = machine.Name;
                            }
                            Reset();
                            SetMessageData(name);
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostRenameMachine:
                        {
                            long hostId = GetMessageData<long>();
                            var machine = channelListener.buzz.GetMachineFromHostID(hostId);
                            string name = GetMessageString();

                            bool success = false;
                            if (machine != null)
                            {
                                success = channelListener.buzz.RenameMachine(machine, name);
                            }
                            Reset();
                            SetMessageData(success);
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostSetEventHandler:
                        {
                            long hostMachineId = GetMessageData<long>();
                            var machine = ChannelListener.buzz.GetMachineFromHostID(hostMachineId);
                            // TODO: Can machine register to other machine events?
                            CMachineEvent cMachineEvent = new CMachineEvent
                            {
                                Type = (BEventType)GetMessageData<int>(),
                                Event_Handler = GetMessageIntPtr(machine),
                                Param_Addr = GetMessageIntPtr(machine)
                            };
                            Reset();

                            if (machine != null)
                            {
                                machine.CMachineEventType.Add(cMachineEvent);
                            }
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostSetModifyFlag:
                        {
                            long hostMachineId = GetMessageData<long>();
                            Reset();

                            var machine = ChannelListener.buzz.GetMachineFromHostID(hostMachineId);
                            if (machine != null)
                            {
                                ChannelListener.buzz.SetModifyFlag(machine);
                            }
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostControlChange:
                        {
                            long hostMachineId = GetMessageData<long>();
                            var machine = ChannelListener.buzz.GetMachineFromHostID(hostMachineId);

                            int group = GetMessageData<int>();
                            int track = GetMessageData<int>();
                            int param = GetMessageData<int>();
                            int value = GetMessageData<int>();

                            if (machine != null)
                            {
                                try
                                {
                                    machine.ParameterGroups[group].Parameters[param].SetValue(track, value);
                                }
                                catch
                                {
                                }
                            }

                            Reset();
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostControlChangeImmediate:
                        {
                            // Global.Buzz.DCWriteLine("HostMessages.HostControlChangeImmediate");

                            // Call tick immediately after?
                            long hostMachineId = GetMessageData<long>();
                            var machine = ChannelListener.buzz.GetMachineFromHostID(hostMachineId);
                            int group = GetMessageData<int>();
                            int track = GetMessageData<int>();
                            int param = GetMessageData<int>();
                            int value = GetMessageData<int>();

                            if (machine != null)
                            {
                                try
                                {
                                    //Application.Current.Dispatcher.Invoke(() =>
                                    //{
                                    machine.ParameterGroups[group].Parameters[param].SetValue(track, value);
                                    //});

                                }
                                catch
                                {

                                }
                            }

                            Reset();
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostSetMidiFocus:
                        {
                            long hostMachineId = GetMessageData<long>();
                            Reset();

                            var machine = ChannelListener.buzz.GetMachineFromHostID(hostMachineId);
                            Global.Buzz.MIDIFocusMachine = machine;
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostGetMachineNames:
                        {
                            long hostMachineId = GetMessageData<long>();
                            Reset();

                            foreach (var machine in Global.Buzz.Song.Machines.Where(m => (m as MachineCore).Ready))
                            {
                                SetMessageData(machine.Name);
                            }

                            SetMessageData((byte)0);
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostCreateRefMachine:
                        {
                            string machineName = GetMessageString();
                            Reset();

                            MachineCore machine = Global.Buzz.Song.Machines.FirstOrDefault(m => m.Name == machineName && !m.DLL.IsMissing) as MachineCore;

                            if (machine != null)
                            {
                                SetMessageData(true); // Machine found

                                var buzz = Global.Buzz as ReBuzzCore;
                                bool sameHost = false;
                                if (!machine.DLL.IsManaged && buzz.MachineManager.NativeMachines.ContainsKey(machine))
                                {
                                    NativeMachineHost nmh = buzz.MachineManager.NativeMachines[machine];
                                    if (nmh == NativeHost)
                                        sameHost = true;
                                }
                                if (sameHost)
                                {
                                    // Machine exists in the target process
                                    SetMessageDataPtr(machine.CMachinePtr);
                                }
                                else
                                {
                                    SetMessageDataPtr(IntPtr.Zero);
                                    // Host id
                                    SetMessageData(machine.CMachineHost);
                                    // Write machine info
                                    WriteMachineInfo(machine);
                                }
                            }
                            else
                            {
                                SetMessageData(false);
                            }
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostSendMidiNote:
                        {
                            long hostMachineId = GetMessageData<long>();
                            var machine = ChannelListener.buzz.GetMachineFromHostID(hostMachineId);
                            int channel = GetMessageData<int>();
                            int value = GetMessageData<int>();
                            int note = GetMessageData<int>();

                            if (machine != null)
                            {
                                machine.SendMIDINote(channel, value, note);
                            }

                            Reset();
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostSendMidiControlChange:
                        {
                            long hostMachineId = GetMessageData<long>();
                            var machine = ChannelListener.buzz.GetMachineFromHostID(hostMachineId);
                            int control = GetMessageData<int>();
                            int channel = GetMessageData<int>();
                            int value = GetMessageData<int>();

                            if (machine != null)
                            {
                                machine.SendMIDIControlChange(control, channel, value);
                            }

                            Reset();
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostGetOption:
                        {
                            long hostId = GetMessageData<long>();
                            var machine = channelListener.buzz.GetMachineFromHostID(hostId);
                            string option = GetMessageString();
                            bool result = false;

                            /*
                            var allSettings = Global.MIDISettings.List.Concat(Global.EngineSettings.List); //.FirstOrDefault(s => s.Name == option.Replace(" ", ""));
                           
                            Setting item = allSettings.FirstOrDefault(s => s.Name == option.Replace(" ", ""));
                            if (item != null)
                            {
                                result = item.Value == "True";
                            }
                            */

                            // There are like only two options pvst is checking
                            if (option == "Master Keyboard Mode")
                            {
                                result = Global.MIDISettings.MasterKeyboardMode;
                            }
                            else if (option == "Accurate BPM")
                            {
                                result = Global.EngineSettings.AccurateBPM;
                            }
                            else if (option == "SubTick Timing")
                            {
                                result = Global.EngineSettings.SubTickTiming;
                            }
                            Reset();
                            SetMessageData(result);
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostGetNumTracks:
                        {
                            long hostMachineId = GetMessageData<long>();
                            Reset();

                            var buzz = Global.Buzz as ReBuzzCore;
                            MachineCore machine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.CMachineHost == hostMachineId);

                            if (machine != null)
                            {
                                SetMessageData(machine.TrackCount);
                            }
                            else
                            {
                                SetMessageData(0);
                            }

                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostGetParameterState:
                        {
                            long hostMachineId = GetMessageData<long>();
                            int group = GetMessageData<int>();
                            int track = GetMessageData<int>();
                            int param = GetMessageData<int>();
                            Reset();

                            var buzz = Global.Buzz as ReBuzzCore;
                            MachineCore machine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.CMachineHost == hostMachineId);

                            if (machine != null)
                            {
                                try
                                {
                                    int val = machine.ParameterGroupsList[group].ParametersList[param].GetPValue(track);
                                    SetMessageData(val);
                                }
                                catch
                                {
                                    SetMessageData(0);
                                }
                            }
                            else
                            {
                                SetMessageData(0);
                            }

                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostAttributesChanged:
                        {
                            long hostMachineId = GetMessageData<long>();
                            Reset();

                            var buzz = Global.Buzz as ReBuzzCore;
                            MachineCore machine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.CMachineHost == hostMachineId);

                            if (machine != null)
                            {
                                buzz.MachineManager.AttributesChanged(machine);
                            }

                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostGetAttribute:
                        {
                            int value = 0;
                            long hostMachineId = GetMessageData<long>();
                            int index = GetMessageData<int>();
                            Reset();

                            var buzz = Global.Buzz as ReBuzzCore;
                            MachineCore machine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.CMachineHost == hostMachineId);

                            if (machine != null)
                            {
                                if (index >= 0 && index < machine.Attributes.Count)
                                {
                                    value = machine.Attributes[index].Value;
                                }

                            }
                            SetMessageData(value);
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostSetAttribute:
                        {
                            long hostMachineId = GetMessageData<long>();
                            int index = GetMessageData<int>();
                            int value = GetMessageData<int>();
                            Reset();

                            var buzz = Global.Buzz as ReBuzzCore;
                            MachineCore machine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.CMachineHost == hostMachineId);

                            if (machine != null)
                            {
                                if (index >= 0 && index < machine.Attributes.Count)
                                {
                                    machine.Attributes[index].Value = value;
                                }

                            }
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostGetPlayingSequence:
                        {
                            long hostMachineId = GetMessageData<long>();
                            Reset();

                            var buzz = Global.Buzz as ReBuzzCore;
                            MachineCore machine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.CMachineHost == hostMachineId);

                            int ret = 0;
                            if (machine != null)
                            {
                                foreach (var seq in buzz.SongCore.SequencesList.Where(s => s.Machine == machine))
                                {
                                    var se = seq.Events.FirstOrDefault(e => (e.Key >= buzz.Song.PlayPosition) && (buzz.Song.PlayPosition < e.Value.Span));
                                    if (se.Value != null)
                                    {
                                        ret = (int)seq.CSequence;
                                    }
                                }
                            }
                            SetMessageData(ret);
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostSendControlChanges:
                        {
                            long hostMachineId = GetMessageData<long>();
                            Reset();

                            var buzz = Global.Buzz as ReBuzzCore;
                            MachineCore machine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.CMachineHost == hostMachineId);

                            if (machine != null)
                            {
                                machine.SendControlChanges();
                            }
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostBeginWriteToPlayingPattern:
                        {
                            long hostMachineId = GetMessageData<long>();
                            int quantization = GetMessageData<int>();
                            Reset();

                            var buzz = Global.Buzz as ReBuzzCore;
                            MachineCore machine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.CMachineHost == hostMachineId);

                            int row = 0;
                            float tickPos = 0;
                            GetPlayingRowAndTickPos(machine, quantization, ref row, ref tickPos);
                            SetMessageData(row);
                            SetMessageData(tickPos);
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostWriteToPlayingPattern:
                        {
                            long hostMachineId = GetMessageData<long>();
                            int group = GetMessageData<int>();
                            int track = GetMessageData<int>();
                            int param = GetMessageData<int>();
                            int value = GetMessageData<int>();

                            var buzz = Global.Buzz as ReBuzzCore;
                            MachineCore machine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.CMachineHost == hostMachineId);

                            if (machine != null)
                            {
                                var p = machine.ParameterGroupsList[group].ParametersList[param];
                                buzz.RecordControlChange(p, track, value);
                            }
                            Reset();
                            DoReplyMessage();
                        }
                        break;
                    case HostMessages.HostEndWriteToPlayingPattern:
                        {
                            long hostMachineId = GetMessageData<long>();
                            DoReplyMessage();
                            /*
                            long hostMachineId = GetMessageLong();
                            int count = GetMessageInt<int>();

                            var buzz = Global.Buzz as ReBuzzCore;
                            MachineCore machine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.CMachineHost == hostMachineId);

                            if (machine != null)
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    long mId = GetMessageLong();
                                    int group = GetMessageInt<int>();
                                    int param = GetMessageInt<int>();
                                    int track = GetMessageInt<int>();
                                    int value = GetMessageInt<int>();

                                    MachineCore targetMachine = buzz.SongCore.MachinesList.FirstOrDefault(m => m.CMachineHost == hostMachineId);
                                    if (targetMachine != null)
                                    {
                                        buzz.RecordControlChange(targetMachine.ParameterGroupsList[group].ParametersList[param], track, value);
                                    }
                                }
                            }
                            DoReplyMessage();
                            */
                        }
                        break;
                    case HostMessages.HostMidiOut:
                        {
                            int device = GetMessageData<int>();
                            int data = GetMessageData<int>();
                            var buzz = Global.Buzz as ReBuzzCore;
                            buzz.MidiInOutEngine.SendMidiOut(device, data);
                            DoReplyMessage();
                        }
                        break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCallback()
        {
            return *(int*)callbackPointer == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte[] ReadMessageBytes(int size)
        {
            byte[] res = new byte[size];
            fixed (byte* dest = res)
            {
                Unsafe.CopyBlock(dest, dataRootPointer, (uint)size);
            }
            return res;
        }

        public void Reset()
        {
            offset = 0;
            sendMessageData.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetMessageSize()
        {
            return *(int*)dataSizePointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMessageSize(int size)
        {
            *(int*)dataSizePointer = size;
        }

        readonly byte[] byteArray = new byte[1];
        readonly byte[] shortArray = new byte[2];
        readonly byte[] intArray = new byte[4];
        readonly byte[] longArray = new byte[8];
        readonly byte[] longlongArray = new byte[16];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        byte[] GetArray(int size)
        {
            if (size == 1)
                return byteArray;
            else if (size == 2)
                return shortArray;
            else if (size == 4)
                return intArray;
            else if (size == 8)
                return longArray;
            else
                return longlongArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetMessageData<T>(T data)
        {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            byte[] array = GetArray(sizeof(T)); //new byte[sizeof(T)];
            fixed (byte* ptr = array)
            {
                *(T*)ptr = data;
                sendMessageData.AddRange(array);
            }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte[] GetBytes<T>(T data)
        {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            byte[] array = GetArray(sizeof(T)); //new byte[sizeof(T)];
            fixed (byte* ptr = array)
            {
                *(T*)ptr = data;
            }
            return array;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMessageDataPtr(IntPtr data)
        {
            if (NativeHost.Host64)
                sendMessageData.AddRange(GetBytes(data.ToInt64()));
            else
                sendMessageData.AddRange(GetBytes(data.ToInt32()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMessageDataPtr(IntPtr data, bool is64Bit)
        {
            if (is64Bit)
                sendMessageData.AddRange(GetBytes(data.ToInt64()));
            else
                sendMessageData.AddRange(GetBytes(data.ToInt32()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMessageData(byte[] data)
        {
            sendMessageData.AddRange(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetMessageData(Sample[] data, int nSamples, bool stereo)
        {
            byte[] array = intArray;
            fixed (byte* ptr = array)
            {
                if (stereo)
                {
                    for (int i = 0; i < nSamples; i++)
                    {
                        *(float*)ptr = data[i].L;
                        sendMessageData.AddRange(array);
                        *(float*)ptr = data[i].R;
                        sendMessageData.AddRange(array);
                    }
                }
                else
                {
                    for (int i = 0; i < nSamples; i++)
                    {
                        *(float*)ptr = (data[i].L + data[i].R);
                        sendMessageData.AddRange(array);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMessageData(string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                sendMessageData.Add((byte)str[i]);
            }
            sendMessageData.Add(0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe T GetMessageData<T>()
        {
            fixed (byte* ptr = receaveMessageTable)
            {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
                T res = *(T*)&ptr[offset];
                offset += sizeof(T);
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
                return res;
            }
        }

        // Reads 64 or 32 bit pointer based on native message process
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IntPtr GetMessageIntPtr(MachineCore machine)
        {
            return machine.MachineDLL.Is64Bit ? new IntPtr(GetMessageData<long>()) : new IntPtr(GetMessageData<uint>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void GetMessageSamples(Sample[] samples, int numSamples, bool stereo)
        {
            fixed (byte* ptr = receaveMessageTable)
            {
                if (stereo)
                {
                    for (int i = 0; i < numSamples; i++)
                    {
                        samples[i].L = *(float*)&ptr[offset];
                        offset += 4;
                        samples[i].R = *(float*)&ptr[offset];
                        offset += 4;
                    }
                }
                else
                {
                    for (int i = 0; i < numSamples; i++)
                    {
                        samples[i].L = samples[i].R = *(float*)&ptr[offset];
                        offset += 4;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetMessageByte()
        {
            byte res = receaveMessageTable[offset];
            offset += 1;
            return res;
        }

        public bool GetMessageBool()
        {
            byte res = receaveMessageTable[offset];
            offset += 1;
            return res == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetMessageString()
        {
            string ret = "";
            while (true)
            {
                byte c = receaveMessageTable[offset];

                if (c == 0)
                    break;
                ret += (char)c;
                offset++;
            }
            offset++;
            return ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] GetMessageBytes(int size)
        {
            byte[] res = new byte[size];
            fixed (byte* dest = res)
            {
                Unsafe.CopyBlock(dest, dataRootPointer + offset, (uint)size);
            }
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] GetData()
        {
            return receaveMessageTable.ToArray();
        }

        public void WriteMachineInfo(MachineCore machine)
        {
            var info = machine.DLL.Info;
            SetMessageData((int)info.Type);
            SetMessageData(info.Version);
            SetMessageData((int)info.Flags);
            SetMessageData(info.MinTracks);
            SetMessageData(info.MaxTracks);
            int numGlobalParameters = machine.ParameterGroups[1].Parameters.Count;
            SetMessageData(numGlobalParameters);
            int numTrackParameters = machine.ParameterGroups[2].Parameters.Count;
            SetMessageData(numTrackParameters);

            // Global
            var pgp = machine.ParameterGroups[1];
            for (int i = 0; i < numGlobalParameters; i++)
            {
                WriteParameter(pgp.Parameters[i]);
            }

            // Track
            var tpg = machine.ParameterGroups[2];
            for (int i = 0; i < numTrackParameters; i++)
            {
                WriteParameter(tpg.Parameters[i]);
            }

            // Attributes
            int numAttributes = machine.Attributes.Count;
            SetMessageData(numAttributes);
            for (int i = 0; i < numAttributes; i++)
            {
                WriteAttribute(machine.AttributesList[i]);
            }

            SetMessageData(info.Name);
            SetMessageData(info.ShortName);
            SetMessageData(info.Author);
            SetMessageData("");
            //SetMessageData(false);
        }

        void WriteParameter(IParameter parameter)
        {
            SetMessageData((int)parameter.Type);
            SetMessageData(parameter.Name);
            SetMessageData(parameter.Description);
            SetMessageData(parameter.MinValue);
            SetMessageData(parameter.MaxValue);
            SetMessageData(parameter.NoValue);
            SetMessageData((int)parameter.Flags);
            SetMessageData(parameter.DefValue);
        }

        void WriteAttribute(IAttribute attribute)
        {
            SetMessageData(attribute.Name);
            SetMessageData(attribute.MinValue);
            SetMessageData(attribute.MaxValue);
            SetMessageData(attribute.DefValue);
        }

        public abstract void ReceaveMessage();

        //public abstract void SendMessage();

        internal abstract void Notify();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteMasterInfo()
        {
            SetMessageData(WorkManager.MasterInfoData);
            return;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteGlobalState()
        {
            SetMessageData(Utils.SerializeValueType(ReBuzzCore.GlobalState));
        }

        internal static void GetPlayingRowAndTickPos(MachineCore machine, int quantization, ref int row, ref float tickPos)
        {
            row = 0;
            tickPos = 0;
            var buzz = Global.Buzz as ReBuzzCore;
            if (machine != null)
            {
                var seq = buzz.SongCore.SequencesList.FirstOrDefault(s => s.Machine == machine);
                if (seq != null)
                {
                    var se = seq.Events.FirstOrDefault(e => (e.Key <= buzz.Song.PlayPosition) && (buzz.Song.PlayPosition < e.Key + e.Value.Span));
                    if (se.Value != null)
                    {
                        int pp = se.Value.Pattern.PlayPosition / PatternEvent.TimeBase;
                        pp = pp < 0 ? 0 : pp;
                        if (quantization > 0)
                        {
                            int rowsPerBeat = buzz.MachineManager.GetTicksPerBeat(machine, se.Value.Pattern, pp);
                            float pos = (float)Math.Floor(pp * quantization + 0.5f) / quantization;
                            tickPos = (pos - (int)pos) * 4.0f / 4.0f; //rowsPerBeat;
                            row = (int)pos;
                        }
                        else
                        {
                            row = pp;
                            tickPos = 0;
                        }
                        //int playposition = se.Value.Pattern.PlayPosition;
                        tickPos = 0;// ReBuzzCore.masterInfo.PosInTick;
                    }
                }
            }
        }
    }
}
