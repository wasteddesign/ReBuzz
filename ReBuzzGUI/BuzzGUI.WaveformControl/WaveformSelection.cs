using System;
using System.ComponentModel;

namespace BuzzGUI.WaveformControl
{
    public class WaveformSelection : INotifyPropertyChanged
    {
        readonly WaveformElement element;
        int originSample;
        int startSample;
        int endSample;
        AdjustmentTargetValue adjustmentTarget = AdjustmentTargetValue.None;

        public WaveformElement Element { get { return element; } }
        public int OriginSample { get { return originSample; } set { originSample = value; } }
        public int StartSample { get { return startSample; } set { startSample = Math.Max(0, value); OnPropertyChanged("StartSample"); } }
        public int EndSample { get { return endSample; } set { endSample = Math.Min(value, element.Waveform.SampleCount); OnPropertyChanged("EndSample"); } }
        public int LengthInSamples { get { return Math.Max(0, (EndSample - StartSample)); } }

        public AdjustmentTargetValue AdjustmentTarget { get { return adjustmentTarget; } set { adjustmentTarget = value; OnPropertyChanged("AdjustmentTarget"); } }

        public double Start { get { return element.SampleToPosition(StartSample); } }
        public double End { get { return element.SampleToPosition(EndSample); } }
        public double Width { get { return Math.Max(0, (End - Start)); } }

        public WaveformSelection(WaveformElement element)
        {
            this.element = element;
        }

        public bool IsActive()
        {
            return !StartSample.Equals(EndSample);
        }

        public void Reset(int cursorSamplePosition)
        {
            StartSample = EndSample = cursorSamplePosition;
        }

        public bool IsNearStart(double x)
        {
            if (x < Start + 3 && x > Start - 3) return true;
            return false;
        }
        public bool IsNearEnd(double x)
        {
            if (x < End + 3 && x > End - 3) return true;
            return false;
        }

        public bool IsValid(Interfaces.IWaveformBase waveform)
        {
            //todo there's certainly more conditions when a selection isn't valid
            //for example start < end etc
            if (waveform.SampleCount < EndSample) return false;
            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }
    }

    public enum AdjustmentTargetValue
    {
        None,
        Start,
        End
    }
}

