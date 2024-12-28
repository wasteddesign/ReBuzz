using System;

namespace Sanford.Multimedia.Midi
{
    public class SysCommonMessageEventArgs : EventArgs
    {
        private readonly SysCommonMessage message;

        public SysCommonMessageEventArgs(SysCommonMessage message)
        {
            this.message = message;
        }

        public SysCommonMessage Message
        {
            get
            {
                return message;
            }
        }
    }
}
