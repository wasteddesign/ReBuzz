using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BuzzGUI.MachineView
{
    /// <summary>
    /// Interaction logic for AmpControl.xaml
    /// </summary>
    public partial class AmpControl : UserControl
    {
        Connection connection;
        IMachineConnection machineConnection;
        const double MinAmp = 66;
        const int PanRight = 0x8000;
        const int PanCenter = 0x4000;
        int oldamp, oldpan;
        int newamp, newpan;

        public AmpControl(ResourceDictionary rd)
        {
            if (rd != null) this.Resources.MergedDictionaries.Add(rd);

            InitializeComponent();

            this.MouseLeave += (sender, e) =>
            {
                Deactivate();
            };

            this.PreviewMouseDoubleClick += (sender, e) => { e.Handled = true; };
            this.MouseRightButtonUp += (sender, e) => { e.Handled = true; };
            this.MouseDown += (sender, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    connection.Disconnect();
                    Visibility = Visibility.Collapsed;
                }
                else
                {
                    e.Handled = true;
                }
            };

            ampKnob.ValueChanged += (sender, e) =>
            {
                newamp = ampKnob.Value == 0 ? 0 : (int)Math.Round(Decibel.ToAmplitude(ampKnob.Value * (MinAmp + Decibel.FromAmplitude((double)0xfffe / 0x4000)) - MinAmp) * 0x4000);
                machineConnection.Amp = newamp;
                UpdateAmpText();
            };

            ampKnob.MouseRightButtonDown += (sender, e) =>
            {
                ampKnob.Value = Math.Min(1, (Decibel.FromAmplitude(0x4000 / 0x4000) + MinAmp) / (MinAmp + Decibel.FromAmplitude((double)0xfffe / 0x4000)));
                e.Handled = true;
            };

            panKnob.ValueChanged += (sender, e) =>
            {
                newpan = (int)Math.Round(panKnob.Value + PanCenter);
                machineConnection.Pan = newpan;
                UpdatePanText();
            };

            panKnob.MouseRightButtonDown += (sender, e) =>
            {
                panKnob.Value = 0;
                e.Handled = true;
            };

            disconnectButton.Click += (sender, e) =>
            {
                connection.Disconnect();
                Visibility = Visibility.Collapsed;
            };

            insertButton.Click += (sender, e) =>
            {
                connection.Insert();
                Visibility = Visibility.Collapsed;
            };

        }

        void UpdateAmpText()
        {
            int v = machineConnection.Amp;
            ampTextBlock.Text = v > 0 ? string.Format("{0:F1}dB", Decibel.FromAmplitude(v * (1.0 / 0x4000))) : "-inf.dB";
        }

        void UpdatePanText()
        {
            int v = machineConnection.Pan;
            if (v == 0) panTextBlock.Text = "L";
            else if (v == PanCenter) panTextBlock.Text = "C";
            else if (v == PanRight) panTextBlock.Text = "R";
            else if (v < PanCenter) panTextBlock.Text = string.Format("{0:F0}L", (PanCenter - v) * (100.0 / PanCenter));
            else if (v > PanCenter) panTextBlock.Text = string.Format("{0:F0}R", (v - PanCenter) * (100.0 / PanCenter));
        }

        void SetAmpKnobValue()
        {
            newamp = oldamp = machineConnection.Amp;
            double v = oldamp;
            if (v <= 0)
                ampKnob.Value = 0;
            else if (v >= 0xfffe)
                ampKnob.Value = 1.0;
            else
                ampKnob.Value = Math.Min(1, (Decibel.FromAmplitude(v / 0x4000) + MinAmp) / (MinAmp + Decibel.FromAmplitude((double)0xfffe / 0x4000)));

            UpdateAmpText();
        }

        void SetPanKnobValue()
        {
            newpan = oldpan = machineConnection.Pan;
            panKnob.Value = (double)oldpan - PanCenter;
            UpdatePanText();
        }

        public void Activate(Connection conn, Point p, MouseButtonEventArgs mbea)
        {
            if (conn == connection && Visibility == Visibility.Visible)
                return;

            connection = conn;
            machineConnection = conn.MachineConnection;

            var mv = this.GetAncestor<MachineView>();

            Canvas.SetLeft(this, p.X - 16);
            Canvas.SetTop(this, p.Y - 16);

            panKnob.Visibility = machineConnection.HasPan ? Visibility.Visible : Visibility.Collapsed;
            panViewbox.Visibility = machineConnection.HasPan ? Visibility.Visible : Visibility.Collapsed;
            grid.RowDefinitions[1].Height = new GridLength(machineConnection.HasPan ? 4.0 : 0.0);

            SetAmpKnobValue();
            if (machineConnection.HasPan) SetPanKnobValue();

            Visibility = Visibility.Visible;

            if (mbea != null)
                ampKnob.BeginDrag(mbea);
        }

        void Deactivate()
        {
            Visibility = Visibility.Collapsed;

            if (newamp != oldamp) connection.MachineGraph.SetConnectionParameter(machineConnection, 0, oldamp, newamp);
            if (newpan != oldpan) connection.MachineGraph.SetConnectionParameter(machineConnection, 1, oldpan, newpan);
        }
    }
}
