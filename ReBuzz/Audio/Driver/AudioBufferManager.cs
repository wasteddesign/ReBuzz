using ReBuzz.Audio.Engine;
using ReBuzz.Common;

namespace ReBuzz.Audio
{
    internal sealed class AudioBufferManager
    {
        public void Clear(IReBuzzAudioProvider provider)
        {
            provider?.ClearBuffer();
        }

        public void ClearChannels(IReBuzzAudioProvider provider)
        {
            provider?.ClearChannels();
        }
    }
}