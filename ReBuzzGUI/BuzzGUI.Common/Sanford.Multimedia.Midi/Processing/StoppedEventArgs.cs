using System;
using System.Collections;

namespace Sanford.Multimedia.Midi
{
    public class StoppedEventArgs : EventArgs
    {
        private readonly ICollection messages;

        public StoppedEventArgs(ICollection messages)
        {
            this.messages = messages;
        }

        public ICollection Messages
        {
            get
            {
                return messages;
            }
        }
    }
}
