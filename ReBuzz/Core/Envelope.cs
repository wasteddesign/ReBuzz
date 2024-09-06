using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ReBuzz.Core
{
    internal class Envelope : IEnvelope
    {
        List<Tuple<int, int>> points;
        public ReadOnlyCollection<Tuple<int, int>> Points { get => points.AsReadOnly(); }

        public List<Tuple<int, int>> PointsList { get => points; set => points = value; }

        public int SustainPoint { get; }

        public int PlayPosition { get; }

        public bool IsEnabled { get; set; }
        public ushort Attack { get; internal set; }
        public ushort Decay { get; internal set; }
        public ushort Sustain { get; internal set; }
        public ushort Release { get; internal set; }
        public byte SubDivide { get; internal set; }
        public byte Flags { get; internal set; }

        public Envelope()
        {
            points = new List<Tuple<int, int>>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Update(IEnumerable<Tuple<int, int>> points, int sustainpoint)
        {

        }
    }
}
