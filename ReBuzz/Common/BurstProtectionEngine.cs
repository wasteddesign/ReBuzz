using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using ReBuzz.Core;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ReBuzz.Audio.BurstProtection
{
    public sealed class BurstProtectionEngine
    {
        private readonly DcBlockerStereo _dc = new DcBlockerStereo();
        private readonly SlewLimiterStereo _slew;
        private EnvelopeFollower _env;

        private ReBuzzCore reBuzz;
        private bool applyToAudioOutput;
        private float scaleMul = 1.0f;
        bool enabled = false;

        public BurstProtectionEngine(ReBuzzCore reBuzz, bool applyToAudioOutput)
        {
            this.reBuzz = reBuzz;
            this.applyToAudioOutput = applyToAudioOutput;

            UpdateScaleMul();

            reBuzz.PropertyChanged += ReBuzz_PropertyChanged;
            Global.EngineSettings.PropertyChanged += ReBuzz_PropertyChanged;

            _slew = new SlewLimiterStereo(0.01f);
            _env = new EnvelopeFollower(5f, 20f, reBuzz.SelectedAudioDriverSampleRate);
        }

        private void ReBuzz_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SelectedAudioDriverSampleRate")
            {
                _env = new EnvelopeFollower(5f, 20f, reBuzz.SelectedAudioDriverSampleRate);
            }
            else if (e.PropertyName == "BurstProtectionAudioOutput")
            {
                UpdateScaleMul();
            }
            else if (e.PropertyName == "BurstProtectionConnections")
            {
                UpdateScaleMul();
            }
        }

        public void Release()
        {
            reBuzz.PropertyChanged -= ReBuzz_PropertyChanged;
            Global.EngineSettings.PropertyChanged -= ReBuzz_PropertyChanged;
        }

        void UpdateEnabled()
        {
            enabled = applyToAudioOutput && Global.EngineSettings.BurstProtectionAudioOutput != BurstProtectionType.Off
                || !applyToAudioOutput && Global.EngineSettings.BurstProtectionConnections != BurstProtectionType.Off;
        }

        void UpdateScaleMul()
        {
            if (applyToAudioOutput)
            {
                UpdateEnabled();
                scaleMul = GetAmplitudeForType(Global.EngineSettings.BurstProtectionAudioOutput);
            }
            else
            {
                UpdateEnabled();
                scaleMul = GetAmplitudeForType(Global.EngineSettings.BurstProtectionConnections);
            }
        }

        // ======================================================================
        // PROCESS: FLOAT[]
        // ======================================================================
        public void Process(float[] buffer, int offset, int count, bool active, bool buzzScale)
        {
            if (!enabled)
                return;

            float scale = buzzScale ? 32768.0f : 1.0f;

            scale *= scaleMul;

            ProcessFloat(buffer, offset, count, active, scale);
        }

        // ======================================================================
        // PROCESS: SAMPLE[]
        // ======================================================================
        public unsafe void Process(Sample[] samples, int offsetSamples, int sampleCount, bool active, bool buzzScale)
        {
            if (!enabled)
                return;

            float scale = buzzScale ? 32768.0f : 1.0f;

            scale *= scaleMul;

            fixed (Sample* p = samples)
            {
                float* f = (float*)p; // reinterpret Sample[] as float*
                int floatOffset = offsetSamples * 2;
                int floatCount = sampleCount * 2;

                ProcessFloatPtr(f, floatOffset, floatCount, active, scale);
            }
        }

        float GetAmplitudeForType(BurstProtectionType type)
        {
            return type switch
            {
                BurstProtectionType.LimitTo0dB => (float)Decibel.ToAmplitude(0.0f),
                BurstProtectionType.LimitTo3dB => (float)Decibel.ToAmplitude(3.0f),
                BurstProtectionType.LimitTo6dB => (float)Decibel.ToAmplitude(6.0f),
                BurstProtectionType.LimitTo9dB => (float)Decibel.ToAmplitude(9.0f),
                BurstProtectionType.LimitTo12dB => (float)Decibel.ToAmplitude(12.0f),
                _ => 1.0f,
            };
        }

        // ======================================================================
        // INTERNAL FLOAT[] PROCESSOR (SIMD + DSP chain, normalized domain)
        // ======================================================================
        private void ProcessFloat(float[] buffer, int offset, int count, bool active, float scale)
        {
            int end = offset + count;
            int vecSize = Vector<float>.Count;

            int i = offset;

            var vInvScale = new Vector<float>(1.0f / scale);
            var vScale = new Vector<float>(scale);

            for (; i + vecSize <= end; i += vecSize)
            {
                float gain = _env.Process(active);
                var gVec = new Vector<float>(gain);

                var xVec = new Vector<float>(buffer, i);

                // normalize to [-1,1] domain
                xVec *= vInvScale;

                // DC + slew in normalized domain
                for (int lane = 0; lane < vecSize; lane++)
                {
                    int channel = ((i + lane) & 1);

                    float x = xVec[lane];
                    x = _dc.ProcessLane(x, channel);
                    x = _slew.ProcessLane(x, channel);

                    xVec = xVec.WithElement(lane, x);
                }

                // envelope
                xVec *= gVec;

                // soft-clip in normalized domain
                xVec = SoftClipVector(xVec);

                // rescale back
                xVec *= vScale;

                xVec.CopyTo(buffer, i);
            }

            // scalar tail
            for (; i < end; i += 2)
            {
                float gain = _env.Process(active);

                float l = buffer[i] / scale;
                float r = buffer[i + 1] / scale;

                l = _dc.ProcessL(l);
                r = _dc.ProcessR(r);

                l = _slew.ProcessL(l);
                r = _slew.ProcessR(r);

                l *= gain;
                r *= gain;

                l = SoftClipScalar(l);
                r = SoftClipScalar(r);

                buffer[i] = l * scale;
                buffer[i + 1] = r * scale;
            }
        }

        // ======================================================================
        // INTERNAL FLOAT* PROCESSOR (SIMD + DSP chain, normalized domain)
        // ======================================================================
        private unsafe void ProcessFloatPtr(float* ptr, int offset, int count, bool active, float scale)
        {
            int end = offset + count;
            int vecSize = Vector<float>.Count;

            int i = offset;

            var vInvScale = new Vector<float>(1.0f / scale);
            var vScale = new Vector<float>(scale);

            for (; i + vecSize <= end; i += vecSize)
            {
                float gain = _env.Process(active);
                var gVec = new Vector<float>(gain);

                Vector<float> xVec = UnsafeRead(ptr + i);

                // normalize
                xVec *= vInvScale;

                // DC + slew
                for (int lane = 0; lane < vecSize; lane++)
                {
                    int channel = ((i + lane) & 1);

                    float x = xVec[lane];
                    x = _dc.ProcessLane(x, channel);
                    x = _slew.ProcessLane(x, channel);

                    xVec = xVec.WithElement(lane, x);
                }

                // envelope
                xVec *= gVec;

                // soft-clip
                xVec = SoftClipVector(xVec);

                // rescale
                xVec *= vScale;

                UnsafeWrite(ptr + i, xVec);
            }

            // scalar tail
            for (; i < end; i += 2)
            {
                float gain = _env.Process(active);

                float l = ptr[i] / scale;
                float r = ptr[i + 1] / scale;

                l = _dc.ProcessL(l);
                r = _dc.ProcessR(r);

                l = _slew.ProcessL(l);
                r = _slew.ProcessR(r);

                l *= gain;
                r *= gain;

                l = SoftClipScalar(l);
                r = SoftClipScalar(r);

                ptr[i] = l * scale;
                ptr[i + 1] = r * scale;
            }
        }

        // ======================================================================
        // DC BLOCKER
        // ======================================================================
        private sealed class DcBlockerStereo
        {
            private float _xm1L, _ym1L;
            private float _xm1R, _ym1R;
            private const float R = 0.995f;

            public float ProcessL(float x)
            {
                float y = x - _xm1L + R * _ym1L;
                _xm1L = x;
                _ym1L = y;
                return y;
            }

            public float ProcessR(float x)
            {
                float y = x - _xm1R + R * _ym1R;
                _xm1R = x;
                _ym1R = y;
                return y;
            }

            public float ProcessLane(float x, int ch) => ch == 0 ? ProcessL(x) : ProcessR(x);
        }

        // ======================================================================
        // SLEW LIMITER
        // ======================================================================
        private sealed class SlewLimiterStereo
        {
            private float _prevL, _prevR;
            private readonly float _maxDelta;

            public SlewLimiterStereo(float maxDelta)
            {
                _maxDelta = maxDelta;
            }

            public float ProcessL(float x)
            {
                float delta = x - _prevL;
                if (delta > _maxDelta) delta = _maxDelta;
                if (delta < -_maxDelta) delta = -_maxDelta;
                float y = _prevL + delta;
                _prevL = y;
                return y;
            }

            public float ProcessR(float x)
            {
                float delta = x - _prevR;
                if (delta > _maxDelta) delta = _maxDelta;
                if (delta < -_maxDelta) delta = -_maxDelta;
                float y = _prevR + delta;
                _prevR = y;
                return y;
            }

            public float ProcessLane(float x, int ch) => ch == 0 ? ProcessL(x) : ProcessR(x);
        }

        // ======================================================================
        // ENVELOPE FOLLOWER
        // ======================================================================
        private sealed class EnvelopeFollower
        {
            private float _gain;
            private readonly float _attack;
            private readonly float _release;

            public EnvelopeFollower(float attackMs, float releaseMs, int sampleRate)
            {
                _attack = 1f / (attackMs * 0.001f * sampleRate);
                _release = 1f / (releaseMs * 0.001f * sampleRate);
            }

            public float Process(bool active)
            {
                float target = active ? 1f : 0f;
                float g = _gain;

                float step = g < target ? _attack : _release;
                g += (target - g) * step;

                _gain = Math.Clamp(g, 0f, 1f);
                return _gain;
            }
        }

        // ======================================================================
        // SOFT CLIPPER (normalized domain)
        // ======================================================================
        private static Vector<float> SoftClipVector(Vector<float> x)
        {
            var one = Vector<float>.One;
            var neg1 = -one;

            x = Vector.Min(one, Vector.Max(neg1, x));

            var x2 = x * x;
            var x3 = x2 * x;
            var k = new Vector<float>(0.33f);

            return x - x3 * k;
        }

        private static float SoftClipScalar(float x)
        {
            if (x > 1f) x = 1f;
            if (x < -1f) x = -1f;
            return x - (x * x * x) * 0.33f;
        }

        // ======================================================================
        // SIMD LOAD/STORE HELPERS
        // ======================================================================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static Vector<float> UnsafeRead(float* src)
            => Unsafe.Read<Vector<float>>(src);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static void UnsafeWrite(float* dst, Vector<float> v)
            => Unsafe.Write(dst, v);
    }
}
