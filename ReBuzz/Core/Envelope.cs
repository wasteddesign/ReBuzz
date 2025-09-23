using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ReBuzz.Core
{
    internal class Envelope : IEnvelope, INotifyPropertyChanged
    {
        List<Tuple<int, int>> points;
        public ReadOnlyCollection<Tuple<int, int>> Points { get => points.AsReadOnly(); }

        public List<Tuple<int, int>> PointsList { get => points; set => points = value; }

        int susteinpoint = -1;
        public int SustainPoint { get => susteinpoint; internal set { susteinpoint = value; PropertyChanged.Raise(this, "SustainPoint"); } }

        int playPosition = -1;
        public int PlayPosition { get => playPosition; set { playPosition = value; } }

        bool isEnabled;
        public bool IsEnabled { get => isEnabled; set { isEnabled = value; PropertyChanged.Raise(this, "IsEnabled"); } }

        public ushort Attack { get; internal set; }
        public ushort Decay { get; internal set; }
        public ushort Sustain { get; internal set; }
        public ushort Release { get; internal set; }
        public byte SubDivide { get; internal set; }
        public byte Flags { get; internal set; }

        public Envelope()
        {
            points = new List<Tuple<int, int>>() { new(0, 0), new(65535, 65535) };

            PropertyChanged.RaiseAll(this);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Update(IEnumerable<Tuple<int, int>> points, int sustainpoint)
        {
            this.points.Clear();
            foreach( var p in points )
            {
                this.points.Add(p);
            }

            SustainPoint = sustainpoint;
        }
    }
}
