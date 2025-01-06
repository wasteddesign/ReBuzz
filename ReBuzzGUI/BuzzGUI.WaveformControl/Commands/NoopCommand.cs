using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using BuzzGUI.Interfaces;
using System.Reflection;
using BuzzGUI;
using BuzzGUI.Common;
using BuzzGUI.WaveformControl.Commands;

namespace BuzzGUI.WaveformControl
{
    public class NoopCommand : ICommand
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
        public WaveformSelection Selection
        {
            get { return selection; }
            set { selection = value; }
        }

        public NoopCommand(WaveformVM inWVM)
        {
            waveformVm = inWVM;
        }

        protected bool UpdateFromParam(object parameter)
        {
            var param = parameter as Tuple<IWaveformBase, WaveformSelection>;
            if (param == null) return false;
            selection = param.Item2 as WaveformSelection;
            waveform = param.Item1 as IWaveformBase;
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

        public virtual bool CanExecute(object parameter)
        {
            return canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public virtual void Execute(object parameter)
        {
            return;//noop
        }

        protected bool canExecute = false;
        public void UpdateCanExecute(bool canExecute)
        {
            this.canExecute = canExecute;
            if (CanExecuteChanged != null)
                CanExecuteChanged(this, null);
        }

        // mono process
		protected void Process(object parameter, Func<float[], int, float> f)
		{
			var param = parameter as Tuple<IWaveformBase, WaveformSelection>;
			if (param == null) return;
			var Selection = param.Item2 as WaveformSelection;
			var wave = param.Item1 as IWaveformBase;
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
            var Selection = param.Item2 as WaveformSelection;
            var wave = param.Item1 as IWaveformBase;
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
			var Selection = param.Item2 as WaveformSelection;
			var wave = param.Item1 as IWaveformBase;
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
