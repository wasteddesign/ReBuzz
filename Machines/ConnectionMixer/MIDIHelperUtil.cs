using System;

namespace WDE.ConnectionMixer
{
    class MIDISoftTakeoverData
    {
        public bool MIDIConnected = false;
        public bool MIDIFirstValueReceived = false;
        public bool machineParameterAbove = false;

        public void Clear()
        {
            MIDIConnected = false;
            MIDIFirstValueReceived = false;
            machineParameterAbove = false;
        }
    }

    public class MIDIHelperUtil
    {
        private MIDISoftTakeoverData[] PanMIDISoftTakeoverEnable;
        private MIDISoftTakeoverData[] VolMIDISoftTakeoverEnable;
        private MIDISoftTakeoverData[,] ParamMIDISoftTakeoverEnable;

        private InterpolatorExtended[] VolInterpolators;
        private InterpolatorExtended[] PanInterpolators;

        public int NumMixers { get; }
        public int NumParams { get; }
        public int ResponseTime { get; private set; }
        public int MIDIDeltaPan = 10;
        public int MIDIDeltaDefault = 5;
        public int MIDIDeltadB = 5;

        public MIDIHelperUtil(int numMixers, int numParams)
        {
            NumMixers = numMixers;
            NumParams = numParams;

            ResponseTime = 10;

            PanMIDISoftTakeoverEnable = new MIDISoftTakeoverData[numMixers];
            VolMIDISoftTakeoverEnable = new MIDISoftTakeoverData[numMixers];
            ParamMIDISoftTakeoverEnable = new MIDISoftTakeoverData[numMixers, numParams];

            VolInterpolators = new InterpolatorExtended[numMixers];
            PanInterpolators = new InterpolatorExtended[numMixers];

            for (int i = 0; i < numMixers; i++)
            {
                PanMIDISoftTakeoverEnable[i] = new MIDISoftTakeoverData();
                VolMIDISoftTakeoverEnable[i] = new MIDISoftTakeoverData();
                for (int j = 0; j < numParams; j++)
                    ParamMIDISoftTakeoverEnable[i, j] = new MIDISoftTakeoverData();

                VolInterpolators[i] = new InterpolatorExtended();
                PanInterpolators[i] = new InterpolatorExtended();
            }
        }

        public void ClearMIDISoftTakeoverData(int mixerNumber, EMIDIControlType type)
        {
            switch (type)
            {
                case EMIDIControlType.Pan:
                    PanMIDISoftTakeoverEnable[mixerNumber].Clear();
                    break;
                case EMIDIControlType.P1:
                    ParamMIDISoftTakeoverEnable[mixerNumber, 0].Clear();
                    break;
                case EMIDIControlType.P2:
                    ParamMIDISoftTakeoverEnable[mixerNumber, 1].Clear();
                    break;
                case EMIDIControlType.P3:
                    ParamMIDISoftTakeoverEnable[mixerNumber, 2].Clear();
                    break;
                case EMIDIControlType.P4:
                    ParamMIDISoftTakeoverEnable[mixerNumber, 3].Clear();
                    break;
                case EMIDIControlType.Volume:
                    VolMIDISoftTakeoverEnable[mixerNumber].Clear();
                    break;
            }
        }

        public void ClearSoftTakeoverParamData(int mixerNumber, int paramNum)
        {
            ParamMIDISoftTakeoverEnable[mixerNumber, paramNum].Clear();
        }

        public void ClearAllSoftTakeoverData()
        {
            for (int i = 0; i < NumMixers; i++)
            {
                PanMIDISoftTakeoverEnable[i].Clear();
                VolMIDISoftTakeoverEnable[i].Clear();

                for (int j = 0; j < NumParams; j++)
                {
                    ParamMIDISoftTakeoverEnable[i, j].Clear();
                }
            }
        }

        internal bool IsMIDIConnected(int mixerNumber, EMIDIControlType type)
        {
            bool ret = false;
            switch (type)
            {
                case EMIDIControlType.Pan:
                    ret = PanMIDISoftTakeoverEnable[mixerNumber].MIDIConnected;
                    break;
                case EMIDIControlType.P1:
                    ret = ParamMIDISoftTakeoverEnable[mixerNumber, 0].MIDIConnected;
                    break;
                case EMIDIControlType.P2:
                    ret = ParamMIDISoftTakeoverEnable[mixerNumber, 1].MIDIConnected;
                    break;
                case EMIDIControlType.P3:
                    ret = ParamMIDISoftTakeoverEnable[mixerNumber, 2].MIDIConnected;
                    break;
                case EMIDIControlType.P4:
                    ret = ParamMIDISoftTakeoverEnable[mixerNumber, 3].MIDIConnected;
                    break;
                case EMIDIControlType.Volume:
                    ret = VolMIDISoftTakeoverEnable[mixerNumber].MIDIConnected;
                    break;
            }
            return ret;
        }

        internal bool IsInterpolatorActive(int i, EMIDIControlType type)
        {
            bool ret = false;
            switch (type)
            {
                case EMIDIControlType.Pan:
                    ret = PanInterpolators[i].IsRunning();
                    break;
                case EMIDIControlType.P1:

                    break;
                case EMIDIControlType.P2:

                    break;
                case EMIDIControlType.P3:

                    break;
                case EMIDIControlType.P4:

                    break;
                case EMIDIControlType.Volume:
                    ret = VolInterpolators[i].IsRunning();
                    break;
            }

            return ret;
        }

        internal int GetInterpolatorValue(int i, EMIDIControlType type)
        {
            int ret = 0;
            switch (type)
            {
                case EMIDIControlType.Pan:
                    ret = (int)PanInterpolators[i].Tick();
                    break;
                case EMIDIControlType.P1:

                    break;
                case EMIDIControlType.P2:

                    break;
                case EMIDIControlType.P3:

                    break;
                case EMIDIControlType.P4:

                    break;
                case EMIDIControlType.Volume:
                    ret = (int)VolInterpolators[i].Tick();
                    break;
            }

            return ret;
        }

        internal bool IsMIDIConnected(int mixerNumber, int paramNumber)
        {
            return ParamMIDISoftTakeoverEnable[mixerNumber, paramNumber].MIDIConnected;
        }

        internal void UpdateMidiConnectionStatus(int mixerNumber, double machineValue, double midiValue, EMIDIControlType type)
        {
            bool match = machineValue == midiValue;
            switch (type)
            {
                case EMIDIControlType.Pan:

                    PanMIDISoftTakeoverEnable[mixerNumber].MIDIConnected = match;

                    if (PanMIDISoftTakeoverEnable[mixerNumber].MIDIFirstValueReceived == false)
                    {
                        PanMIDISoftTakeoverEnable[mixerNumber].MIDIFirstValueReceived = true;
                        PanMIDISoftTakeoverEnable[mixerNumber].machineParameterAbove = machineValue > midiValue;

                        if (Math.Abs(machineValue - midiValue) <= MIDIDeltaPan)
                            PanMIDISoftTakeoverEnable[mixerNumber].MIDIConnected = true;
                    }
                    else
                    {
                        if (PanMIDISoftTakeoverEnable[mixerNumber].machineParameterAbove && machineValue < midiValue)
                            PanMIDISoftTakeoverEnable[mixerNumber].MIDIConnected = true;
                        else if (!PanMIDISoftTakeoverEnable[mixerNumber].machineParameterAbove && machineValue > midiValue)
                            PanMIDISoftTakeoverEnable[mixerNumber].MIDIConnected = true;
                    }
                    break;
                case EMIDIControlType.Volume:
                    VolMIDISoftTakeoverEnable[mixerNumber].MIDIConnected = match;

                    if (VolMIDISoftTakeoverEnable[mixerNumber].MIDIFirstValueReceived == false)
                    {
                        VolMIDISoftTakeoverEnable[mixerNumber].MIDIFirstValueReceived = true;
                        VolMIDISoftTakeoverEnable[mixerNumber].machineParameterAbove = machineValue > midiValue;

                        if (Math.Abs(machineValue - midiValue) <= MIDIDeltadB)
                            VolMIDISoftTakeoverEnable[mixerNumber].MIDIConnected = true;
                    }
                    else
                    {
                        if (VolMIDISoftTakeoverEnable[mixerNumber].machineParameterAbove && machineValue < midiValue)
                            VolMIDISoftTakeoverEnable[mixerNumber].MIDIConnected = true;
                        else if (!VolMIDISoftTakeoverEnable[mixerNumber].machineParameterAbove && machineValue > midiValue)
                            VolMIDISoftTakeoverEnable[mixerNumber].MIDIConnected = true;
                    }
                    break;
            }
        }

        internal void UpdateMidiConnectionParameterStatus(int mixerNumber, int paramNum, int machineValue, int midiValue)
        {
            bool match = machineValue == midiValue;

            ParamMIDISoftTakeoverEnable[mixerNumber, paramNum].MIDIConnected = match;

            if (ParamMIDISoftTakeoverEnable[mixerNumber, paramNum].MIDIFirstValueReceived == false)
            {
                ParamMIDISoftTakeoverEnable[mixerNumber, paramNum].MIDIFirstValueReceived = true;
                ParamMIDISoftTakeoverEnable[mixerNumber, paramNum].machineParameterAbove = machineValue > midiValue;

                if (Math.Abs(machineValue - midiValue) <= MIDIDeltaDefault)
                    ParamMIDISoftTakeoverEnable[mixerNumber, paramNum].MIDIConnected = true;
            }
            else
            {
                if (ParamMIDISoftTakeoverEnable[mixerNumber, paramNum].machineParameterAbove && machineValue < midiValue)
                    ParamMIDISoftTakeoverEnable[mixerNumber, paramNum].MIDIConnected = true;
                else if (!ParamMIDISoftTakeoverEnable[mixerNumber, paramNum].machineParameterAbove && machineValue > midiValue)
                    ParamMIDISoftTakeoverEnable[mixerNumber, paramNum].MIDIConnected = true;
            }
        }

        internal void SetInterpolator(int i, int currentVal, int newamp, EMIDIControlType type)
        {
            switch (type)
            {
                case EMIDIControlType.Pan:
                    PanInterpolators[i].Value = currentVal;
                    PanInterpolators[i].SetTarget(newamp, ResponseTime);
                    break;
                case EMIDIControlType.P1:

                    break;
                case EMIDIControlType.P2:

                    break;
                case EMIDIControlType.P3:

                    break;
                case EMIDIControlType.P4:

                    break;
                case EMIDIControlType.Volume:
                    VolInterpolators[i].Value = currentVal;
                    VolInterpolators[i].SetTarget(newamp, ResponseTime);
                    break;
            }


        }
    }

    public class InterpolatorExtended
    {
        float value;
        float target;
        float delta;

        public InterpolatorExtended() { }
        public InterpolatorExtended(float v) { value = target = v; }

        public float Value
        {
            set
            {
                this.value = value;
            }
            get
            {
                return value;
            }
        }

        public bool IsRunning()
        {
            return value != target;
        }

        public void SetTarget(float t, int time)
        {
            target = t;

            if (time > 0)
            {
                delta = (target - value) / time;
            }
            else
            {
                delta = 0;
                value = target;
            }
        }

        public float Tick()
        {
            if (delta != 0.0f)
            {
                value += delta;

                if (delta > 0)
                {
                    if (value >= target)
                    {
                        value = target;
                        delta = 0;
                    }
                }
                else
                {
                    if (value <= target)
                    {
                        value = target;
                        delta = 0;
                    }
                }
            }

            return value;
        }
    }
}
