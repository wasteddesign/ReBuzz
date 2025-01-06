using System;

namespace Sanford.Multimedia.Midi
{
    public class SysExMessageEventArgs : EventArgs
    {
        private readonly SysExMessage message;

        public SysExMessageEventArgs(SysExMessage message)
        {
            this.message = message;
        }

        public SysExMessage Message
        {
            get
            {
                return message;
            }
        }
    }
}
