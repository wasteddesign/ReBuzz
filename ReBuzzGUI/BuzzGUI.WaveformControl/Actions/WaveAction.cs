using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
using System;

namespace BuzzGUI.WaveformControl.Actions
{
    internal abstract class WaveAction : BuzzAction
    {
        private IWaveformBase waveform;
        public IWaveformBase Waveform
        {
            get { return waveform; }
            set { waveform = value; }
        }

        private WaveformVM waveformVm;
        public WaveformVM WaveformVm
        {
            get { return waveformVm; }
            set
            {
                waveformVm = value;
                wavetable = waveformVm.Wavetable;
            }
        }

        private IWavetable wavetable;
        public IWavetable Wavetable
        {
            get { return wavetable ?? waveformVm.Wavetable; }
            set { wavetable = value; }
        }

        private WaveformSelection selection;
        private readonly IWaveformBase oldWaveform;

        public WaveformSelection Selection
        {
            get { return selection; }
            set { selection = value; }
        }

        public WaveAction(WaveformVM inWVM)
        {
            waveformVm = inWVM;
        }

        private TemporaryWave backupWave;
        internal int selectedSlotIndex;
        internal int selectedLayerIndex;
        internal int oldSelectionStartSample;
        internal int oldSelectionEndSample;

        protected bool SaveState(object parameter, bool backupWaveLayer)
        {
            var param = parameter as Tuple<IWaveformBase, WaveformSelection>;
            if (param == null) return false;

            selection = param.Item2;
            waveform = param.Item1;

            selectedSlotIndex = waveformVm.SelectedWave.Index;
            selectedLayerIndex = WaveCommandHelpers.GetLayerIndex(Waveform);
            oldSelectionStartSample = param.Item2.StartSample;
            oldSelectionEndSample = param.Item2.EndSample;
            Wavetable = waveformVm.Wavetable;

            if (backupWaveLayer)
            {
                backupWave = new TemporaryWave(waveformVm.Waveform);
            }

            return true;
        }

        protected void RestoreWave()
        {
            bool playing = Global.Buzz.Playing;
            Global.Buzz.Playing = false;

            WaveCommandHelpers.ReplaceLayer(Wavetable, selectedSlotIndex, selectedLayerIndex, backupWave);

            Global.Buzz.Playing = playing;
        }

        protected void RestoreSelection()
        {
            if (Selection != null)
            {
                Selection.StartSample = oldSelectionStartSample;
                Selection.EndSample = oldSelectionEndSample;
            }
        }

        protected bool UpdateFromState(bool forceSelection = true)
        {
            if (selection == null || waveform == null)
            {
                return false;
            }
            else
            {
                waveformVm.Waveform = Wavetable.Waves[selectedSlotIndex].Layers[selectedLayerIndex];

                bool waveChanged = waveformVm.SelectedSlotIndex != selectedSlotIndex | waveformVm.SelectedLayerIndex != selectedLayerIndex;
                waveformVm.SelectedSlotIndex = selectedSlotIndex;
                waveformVm.SelectedLayerIndex = selectedLayerIndex;
                if (waveChanged)
                {
                    waveformVm.RaiseEditedWaveChanged();
                }

                Selection.StartSample = oldSelectionStartSample;
                Selection.EndSample = oldSelectionEndSample;
                //we need to save the layer index that is beeing edited because the whole slot will be re-written
            }
            if (forceSelection)
            {
                if (!selection.IsActive()) return false;
            }
            return true;
        }

        protected bool UpdateFromParam(object parameter)
        {
            var param = parameter as Tuple<IWaveformBase, WaveformSelection>;
            if (param == null) return false;
            selection = param.Item2;
            waveform = param.Item1;
            if (selection == null || waveform == null)
            {
                return false;
            }
            else
            {
                waveformVm.SelectedSlotIndex = waveformVm.SelectedWave.Index;
                waveformVm.SelectedLayerIndex = WaveCommandHelpers.GetLayerIndex(Waveform);
                //we need to save the layer index that is beeing edited because the whole slot will be re-written
            }
            if (!selection.IsActive()) return false;
            return true;
        }

        // mono process
        protected void Process(object parameter, Func<float[], int, float> f)
        {
            var param = parameter as Tuple<IWaveformBase, WaveformSelection>;
            if (param == null) return;
            var Selection = param.Item2;
            var wave = param.Item1;
            if (Selection == null || wave == null) return;

            var src = new float[Selection.LengthInSamples];
            var dst = new float[Selection.LengthInSamples];

            wave.GetDataAsFloat(src, 0, 1, 0, Selection.StartSample, Selection.LengthInSamples);
            for (int i = 0; i < src.Length; i++) dst[i] = f(src, i);
            wave.SetDataAsFloat(dst, 0, 1, 0, Selection.StartSample, Selection.LengthInSamples);

            if (wave.ChannelCount == 2)
            {
                wave.GetDataAsFloat(src, 0, 1, 1, Selection.StartSample, Selection.LengthInSamples);
                for (int i = 0; i < src.Length; i++) dst[i] = f(src, i);
                wave.SetDataAsFloat(dst, 0, 1, 1, Selection.StartSample, Selection.LengthInSamples);
            }

            wave.InvalidateData();
        }

        // stereo process
        protected void Process(object parameter, Func<float[], int, float> f, Func<float[], int, float> g)
        {
            var param = parameter as Tuple<IWaveformBase, WaveformSelection>;
            if (param == null) return;
            var Selection = param.Item2;
            var wave = param.Item1;
            if (Selection == null || wave == null) return;

            var src = new float[Selection.LengthInSamples];
            var dst = new float[Selection.LengthInSamples];

            wave.GetDataAsFloat(src, 0, 1, 0, Selection.StartSample, Selection.LengthInSamples);
            for (int i = 0; i < src.Length; i++) dst[i] = f(src, i);
            wave.SetDataAsFloat(dst, 0, 1, 0, Selection.StartSample, Selection.LengthInSamples);

            if (wave.ChannelCount == 2)
            {
                wave.GetDataAsFloat(src, 0, 1, 1, Selection.StartSample, Selection.LengthInSamples);
                for (int i = 0; i < src.Length; i++) dst[i] = g(src, i);
                wave.SetDataAsFloat(dst, 0, 1, 1, Selection.StartSample, Selection.LengthInSamples);
            }

            wave.InvalidateData();
        }

        // NOTE: this is stereo aggregate followed by mono process originally written for NormalizeCommand. you can create variations of this as needed.
        protected void Process<T>(object parameter, T state, Func<T, float[], int, T> aggregate, Func<T, float[], int, float> f)
        {
            var param = parameter as Tuple<IWaveformBase, WaveformSelection>;
            if (param == null) return;
            var Selection = param.Item2;
            var wave = param.Item1;
            if (Selection == null || wave == null) return;

            var srcl = new float[Selection.LengthInSamples];
            float[] srcr = null;

            wave.GetDataAsFloat(srcl, 0, 1, 0, Selection.StartSample, Selection.LengthInSamples);
            for (int i = 0; i < srcl.Length; i++) state = aggregate(state, srcl, i);

            if (wave.ChannelCount == 2)
            {
                srcr = new float[Selection.LengthInSamples];
                wave.GetDataAsFloat(srcr, 0, 1, 1, Selection.StartSample, Selection.LengthInSamples);
                for (int i = 0; i < srcr.Length; i++) state = aggregate(state, srcr, i);
            }

            var dst = new float[Selection.LengthInSamples];

            wave.GetDataAsFloat(srcl, 0, 1, 0, Selection.StartSample, Selection.LengthInSamples);
            for (int i = 0; i < srcl.Length; i++) dst[i] = f(state, srcl, i);
            wave.SetDataAsFloat(dst, 0, 1, 0, Selection.StartSample, Selection.LengthInSamples);

            if (wave.ChannelCount == 2)
            {
                wave.GetDataAsFloat(srcr, 0, 1, 1, Selection.StartSample, Selection.LengthInSamples);
                for (int i = 0; i < srcr.Length; i++) dst[i] = f(state, srcr, i);
                wave.SetDataAsFloat(dst, 0, 1, 1, Selection.StartSample, Selection.LengthInSamples);
            }

            wave.InvalidateData();

        }
    }
}
