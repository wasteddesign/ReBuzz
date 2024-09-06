namespace ReBuzz.Audio
{
    internal interface IReBuzzAudioProvider
    {
        int ReadOverride(float[] buffer, int offset, int count);
        void ClearBuffer();
        CommonAudioProvider AudioSampleProvider { get; }
    }
}
