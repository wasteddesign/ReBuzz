using System;
using System.IO.MemoryMappedFiles;
using System.Threading.Tasks;

namespace ReBuzz.NativeMachine
{
    internal class HostMessage : NativeMessage
    {
        public HostMessage(ChannelType channel, MemoryMappedViewAccessor accessor, NativeMachineHost nativeMachineHost) : base(channel, accessor, nativeMachineHost)
        {
        }

        public override event EventHandler<EventArgs> MessageEvent;

        public override void ReceaveMessage()
        {
            DoReceiveIncomingMessage();
        }

        internal override void Notify()
        {
            // Use async not to block host listener
            //MessageEvent?.BeginInvoke(this, new MessageEventArgs() { MessageId = GetMessageId() }, null, null);
            Task task = Task.Run(() => MessageEvent?.Invoke(this, new MessageEventArgs() { MessageId = GetMessageId() }));
        }
    }
}
