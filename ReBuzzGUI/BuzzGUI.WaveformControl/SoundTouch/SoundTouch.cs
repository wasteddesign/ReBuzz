using System;
using System.Text;

namespace DSPLib.SoundTouch
{
    class SoundTouch : IDisposable
    {
        private IntPtr handle;
        private string versionString;
        private readonly bool is64Bit;
        public SoundTouch()
        {
            is64Bit = IntPtr.Size == 8;

            handle = is64Bit ? SoundTouchInterop64.soundtouch_createInstance() :
                SoundTouchInterop32.soundtouch_createInstance();
        }

        public string VersionString
        {
            get
            {
                if (versionString == null)
                {
                    var s = new StringBuilder(100);
                    if (is64Bit)
                        SoundTouchInterop64.soundtouch_getVersionString2(s, s.Capacity);
                    else
                        SoundTouchInterop32.soundtouch_getVersionString2(s, s.Capacity);
                    versionString = s.ToString();
                }
                return versionString;
            }
        }

        public void SetPitchOctaves(float pitchOctaves)
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_setPitchOctaves(handle, pitchOctaves);
            else
                SoundTouchInterop32.soundtouch_setPitchOctaves(handle, pitchOctaves);
        }

        public void SetPitchSemitones(float pitchSemitones)
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_setPitchSemiTones(handle, pitchSemitones);
            else
                SoundTouchInterop32.soundtouch_setPitchSemiTones(handle, pitchSemitones);
        }
        public void SetTempoChange(float newTempo)
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_setTempoChange(handle, newTempo);
            else
                SoundTouchInterop32.soundtouch_setTempoChange(handle, newTempo);
        }

        public void SetSampleRate(int sampleRate)
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_setSampleRate(handle, (uint)sampleRate);
            else
                SoundTouchInterop32.soundtouch_setSampleRate(handle, (uint)sampleRate);
        }

        public void SetChannels(int channels)
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_setChannels(handle, (uint)channels);
            else
                SoundTouchInterop32.soundtouch_setChannels(handle, (uint)channels);
        }

        private void DestroyInstance()
        {
            if (handle != IntPtr.Zero)
            {
                if (is64Bit)
                    SoundTouchInterop64.soundtouch_destroyInstance(handle);
                else
                    SoundTouchInterop32.soundtouch_destroyInstance(handle);
                handle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            DestroyInstance();
            GC.SuppressFinalize(this);
        }

        ~SoundTouch()
        {
            DestroyInstance();
        }

        public void PutSamples(float[] samples, int numSamples)
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_putSamples(handle, samples, numSamples);
            else
                SoundTouchInterop32.soundtouch_putSamples(handle, samples, numSamples);
        }

        public int ReceiveSamples(float[] outBuffer, int maxSamples)
        {
            if (is64Bit)
                return (int)SoundTouchInterop64.soundtouch_receiveSamples(handle, outBuffer, (uint)maxSamples);
            return (int)SoundTouchInterop32.soundtouch_receiveSamples(handle, outBuffer, (uint)maxSamples);
        }

        public bool IsEmpty
        {
            get
            {
                if (is64Bit)
                    return SoundTouchInterop64.soundtouch_isEmpty(handle) != 0;
                return SoundTouchInterop32.soundtouch_isEmpty(handle) != 0;
            }
        }

        public int NumberOfSamplesAvailable
        {
            get
            {
                if (is64Bit)
                    return (int)SoundTouchInterop64.soundtouch_numSamples(handle);
                return (int)SoundTouchInterop32.soundtouch_numSamples(handle);
            }
        }

        public int NumberOfUnprocessedSamples
        {
            get
            {
                if (is64Bit)
                    return SoundTouchInterop64.soundtouch_numUnprocessedSamples(handle);
                return SoundTouchInterop32.soundtouch_numUnprocessedSamples(handle);
            }
        }

        public void Flush()
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_flush(handle);
            else
                SoundTouchInterop32.soundtouch_flush(handle);
        }

        public void Clear()
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_clear(handle);
            else
                SoundTouchInterop32.soundtouch_clear(handle);
        }

        public void SetRate(float newRate)
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_setRate(handle, newRate);
            else
                SoundTouchInterop32.soundtouch_setRate(handle, newRate);
        }

        public void SetRateChange(float newRate)
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_setRateChange(handle, newRate);
            else
                SoundTouchInterop32.soundtouch_setRateChange(handle, newRate);
        }

        public void SetTempo(float newTempo)
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_setTempo(handle, newTempo);
            else
                SoundTouchInterop32.soundtouch_setTempo(handle, newTempo);
        }

        public int GetUseAntiAliasing()
        {
            if (is64Bit)
                return SoundTouchInterop64.soundtouch_getSetting(handle, SoundTouchSettings.UseAaFilter);
            return SoundTouchInterop32.soundtouch_getSetting(handle, SoundTouchSettings.UseAaFilter);
        }

        public void SetUseAntiAliasing(bool useAntiAliasing)
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_setSetting(handle, SoundTouchSettings.UseAaFilter, useAntiAliasing ? 1 : 0);
            else
                SoundTouchInterop32.soundtouch_setSetting(handle, SoundTouchSettings.UseAaFilter, useAntiAliasing ? 1 : 0);
        }

        public void SetUseQuickSeek(bool useQuickSeek)
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_setSetting(handle, SoundTouchSettings.UseQuickSeek, useQuickSeek ? 1 : 0);
            else
                SoundTouchInterop32.soundtouch_setSetting(handle, SoundTouchSettings.UseQuickSeek, useQuickSeek ? 1 : 0);
        }

        public int GetUseQuickSeek()
        {
            if (is64Bit)
                return SoundTouchInterop64.soundtouch_getSetting(handle, SoundTouchSettings.UseQuickSeek);
            return SoundTouchInterop32.soundtouch_getSetting(handle, SoundTouchSettings.UseQuickSeek);
        }

        public int AaFilterLength()
        {
            if (is64Bit)
                return SoundTouchInterop64.soundtouch_getSetting(handle, SoundTouchSettings.AaFilterLength);
            return SoundTouchInterop32.soundtouch_getSetting(handle, SoundTouchSettings.AaFilterLength);
        }

        public void SetAaFilterLength(int lenght)
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_setSetting(handle, SoundTouchSettings.AaFilterLength, lenght);
            else
                SoundTouchInterop32.soundtouch_setSetting(handle, SoundTouchSettings.AaFilterLength, lenght);
        }

        public int GetOverlapMs()
        {
            if (is64Bit)
                return SoundTouchInterop64.soundtouch_getSetting(handle, SoundTouchSettings.OverlapMs);
            return SoundTouchInterop32.soundtouch_getSetting(handle, SoundTouchSettings.OverlapMs);
        }

        public void SetOverlapMs(int value)
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_setSetting(handle, SoundTouchSettings.OverlapMs, value);
            else
                SoundTouchInterop32.soundtouch_setSetting(handle, SoundTouchSettings.OverlapMs, value);
        }

        public int GetSeekWindowMs()
        {
            if (is64Bit)
                return SoundTouchInterop64.soundtouch_getSetting(handle, SoundTouchSettings.SeekWindowMs);
            return SoundTouchInterop32.soundtouch_getSetting(handle, SoundTouchSettings.SeekWindowMs);
        }

        public void SetSeekWindowMs(int value)
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_setSetting(handle, SoundTouchSettings.SeekWindowMs, value);
            else
                SoundTouchInterop32.soundtouch_setSetting(handle, SoundTouchSettings.SeekWindowMs, value);
        }

        public int GetSequenceMs()
        {
            if (is64Bit)
                return SoundTouchInterop64.soundtouch_getSetting(handle, SoundTouchSettings.SequenceMs);
            return SoundTouchInterop32.soundtouch_getSetting(handle, SoundTouchSettings.SequenceMs);
        }

        public void SetSequenceMs(int value)
        {
            if (is64Bit)
                SoundTouchInterop64.soundtouch_setSetting(handle, SoundTouchSettings.SequenceMs, value);
            else
                SoundTouchInterop32.soundtouch_setSetting(handle, SoundTouchSettings.SequenceMs, value);
        }
    }

    public sealed class BPMDetect : IDisposable
    {
        #region Private Members

        private readonly object SyncRoot = new object();
        private bool IsDisposed = false;
        private IntPtr handle;
        private readonly bool is64Bit;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="BPMDetect"/> class.
        /// </summary>
        public BPMDetect(int numChannels, int sampleRate)
        {
            is64Bit = IntPtr.Size == 8;

            handle = is64Bit ? SoundTouchInterop64.bpm_createInstance(numChannels, sampleRate) :
                SoundTouchInterop32.bpm_createInstance(numChannels, sampleRate);
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="BPMDetect"/> class.
        /// </summary>
        ~BPMDetect()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the analysed BPM rate.
        /// </summary>
        public float Bpm
        {
            get { lock (SyncRoot) { return is64Bit ? SoundTouchInterop64.bpm_getBpm(handle) : SoundTouchInterop32.bpm_getBpm(handle); } }
        }

        #endregion

        #region Sample Stream Methods

        /// <summary>
        /// Feed 'numSamples' sample into the BPM detector
        /// </summary>
        /// <param name="samples">Sample buffer to input</param>
        /// <param name="numSamples">Number of sample frames in buffer. Notice
        /// that in case of multi-channel sound a single sample frame contains 
        /// data for all channels</param>
        public void PutSamples(float[] samples, uint numSamples)
        {
            lock (SyncRoot)
            {
                if (is64Bit)
                    SoundTouchInterop64.bpm_putSamples(handle, samples, numSamples);
                else
                    SoundTouchInterop32.bpm_putSamples(handle, samples, numSamples);
            }
        }


        #endregion

        #region IDisposable Support

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (!IsDisposed)
            {
                if (alsoManaged)
                {
                    // NOTE: Placeholder, dispose managed state (managed objects).
                    // At this point, nothing managed to dispose
                }

                if (is64Bit)
                    SoundTouchInterop64.bpm_destroyInstance(handle);
                else
                    SoundTouchInterop32.bpm_destroyInstance(handle);

                handle = IntPtr.Zero;

                IsDisposed = true;
            }
        }

        #endregion

    }
}
