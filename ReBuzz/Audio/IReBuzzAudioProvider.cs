namespace ReBuzz.Audio
{
    internal interface IReBuzzAudioProvider
    {
        int ReadOverride(float[] buffer, int offset, int count, bool multiChannel);
        void ClearBuffer();
        void ClearChannels();
        CommonAudioProvider AudioSampleProvider { get; }
    }
}
