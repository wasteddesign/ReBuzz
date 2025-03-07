using ReBuzz.Core;
using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace ReBuzz.NativeMachine
{
    internal class MidiMessage : NativeMessage
    {
        internal readonly Lock MidiLock = new();

        public MidiMessage(ChannelType channel, MemoryMappedViewAccessor accessor, NativeMachineHost nativeMachineHost) : base(channel, accessor, nativeMachineHost)
        {
        }

        public override event EventHandler<EventArgs> MessageEvent;

        public override void ReceaveMessage()
        {
        }

        internal override void Notify()
        {
            MessageEvent?.Invoke(this, new MessageEventArgs() { MessageId = GetMessageId() });
        }

        public void MidiNote(MachineCore machine, int channel, int note, int velocity)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            lock (MidiLock)
            {
                Reset();
                SetMessageData((int)MIDIMessages.MIDINote);
                SetMessageDataPtr(machine.CMachinePtr);
                SetMessageData(channel);
                SetMessageData(note);
                SetMessageData(velocity);
                DoSendMessage();
            }
        }

        public void MidiControlChange(MachineCore machine, int ctrl, int channel, int value)
        {
            if (machine.DLL.IsCrashed)
            {
                return;
            }

            lock (MidiLock)
            {
                Reset();
                SetMessageData((int)MIDIMessages.MIDIControlChange);
                SetMessageDataPtr(machine.CMachinePtr);
                SetMessageData(ctrl);
                SetMessageData(channel);
                SetMessageData(value);
                DoSendMessage();
            }
        }
    }
}
