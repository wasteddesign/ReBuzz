using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace WDE.ModernSequenceEditorHorizontal
{
    public class ViewSettings : INotifyPropertyChanged
    {
        public ViewSettings(SequenceEditor e)
        {
            EditContext = new EditContext(e);
        }

        double tickWidth = 2;
        public double TickWidth { get { return tickWidth; } set { tickWidth = value; } }
        public int SongEnd { get; set; }
        public int TrackCount { get; set; }

        //public int LastCellTime { get { return TimeSignatureList.Snap(int.MaxValue, SongEnd); } }

        public double Width { get { return TickWidth * SongEnd; } }

        public double TrackHeight { get { return (int)SequenceEditor.Settings.TrackHeight; } }

        //TimeSignatureList timeSignatureList = new TimeSignatureList();
        //public TimeSignatureList TimeSignatureList { get { return timeSignatureList; } set { timeSignatureList = value; } }

        public IEditContext EditContext { get; set; }

        //public Dictionary<IPattern, PatternEx> PatternAssociations = new Dictionary<IPattern, PatternEx>();
        //public event Action PatternAssociationsChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        //public void PatternAssociationsChangedEvent()
        //{
        //	PropertyChanged?.Invoke(null, new PropertyChangedEventArgs("PatternAssociations"));
        //}

        public void VUMeterMachineConnectionEvent()
        {
            PropertyChanged?.Invoke(null, new PropertyChangedEventArgs("VUMeterTarget"));
        }

        public Dictionary<ISequence, IMachineConnection> VUMeterMachineConnection = new Dictionary<ISequence, IMachineConnection>();

        public int NonPlayPattenSpan { get { return 10; } }
    }
}
