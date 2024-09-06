using System;

namespace ReBuzz.NativeMachine
{
    internal interface INativeMessage
    {
        event EventHandler<EventArgs> MessageEvent;
        void ReceaveMessage();
        void SendMessage();

        bool IsSending { get; set; }
    }
}
