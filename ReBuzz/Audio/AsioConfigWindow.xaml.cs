using BuzzGUI.Common;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace BuzzDotNet.Audio
{
    /// <summary>
    /// Interaction logic for WasapiConfigWindow.xaml
    /// </summary>
    public partial class AsioConfigWindow : Window
    {
        public event Action OpenAsioControlPanel;
        public AsioConfigWindow(string device)
        {
            DataContext = this;
            InitializeComponent();

            tbDevice.Text = device;

            int asioDeviceSamplerate = RegistryEx.Read("SampleRate", 44100, "ASIO");

            ComboBoxItem cbiSampleRate = new ComboBoxItem() { Content = "44100", Tag = 44100 };
            cbSampleRate.Items.Add(cbiSampleRate);
            cbiSampleRate = new ComboBoxItem() { Content = "48000", Tag = 48000 };
            cbSampleRate.Items.Add(cbiSampleRate);
            cbiSampleRate = new ComboBoxItem() { Content = "88200", Tag = 88200 };
            cbSampleRate.Items.Add(cbiSampleRate);
            cbiSampleRate = new ComboBoxItem() { Content = "96000", Tag = 96000 };
            cbSampleRate.Items.Add(cbiSampleRate);
            cbiSampleRate = new ComboBoxItem() { Content = "132300", Tag = 132300 };
            cbSampleRate.Items.Add(cbiSampleRate);
            cbiSampleRate = new ComboBoxItem() { Content = "176400", Tag = 176400 };
            cbSampleRate.Items.Add(cbiSampleRate);
            cbiSampleRate = new ComboBoxItem() { Content = "192000", Tag = 192000 };
            cbSampleRate.Items.Add(cbiSampleRate);

            var srItem = cbSampleRate.Items[0];
            foreach (var item in cbSampleRate.Items)
                if ((int)(item as ComboBoxItem).Tag == asioDeviceSamplerate)
                {
                    srItem = item;
                    break;
                }
            cbSampleRate.SelectedItem = srItem;

            // Latency
            int currentBufferSize = RegistryEx.Read("BufferSize", 0, "ASIO");
            int bufferSize = 16;
            int increment = 8;
            int selectedIndex = 10;
            for (int i = 0; i < 40; i++)
            {
                if (bufferSize > 1000)
                    bufferSize = 1024;
                double bufferLatency = 1000.0 * 2 * bufferSize / asioDeviceSamplerate;
                ComboBoxItem cbiLatency = new ComboBoxItem()
                {
                    Content = bufferSize + " (" + String.Format(CultureInfo.InvariantCulture,
                   "{0:0.0}", bufferLatency) + "ms)",
                    Tag = bufferSize
                };
                cbLatency.Items.Add(cbiLatency);
                if (currentBufferSize == bufferSize)
                    selectedIndex = i;

                if (bufferSize == 1024)
                    break;

                if (i % 16 == 0)
                {
                    increment *= 2;
                }
                bufferSize += increment;
            }
            cbLatency.SelectedIndex = selectedIndex;

            btAsioControlPanel.Click += (s, e) =>
            {
                if (OpenAsioControlPanel != null)
                {
                    OpenAsioControlPanel.Invoke();
                }
            };

            btCancel.Click += (sender, e) =>
            {
                this.Close();
            };

            btOk.Click += (sender, e) =>
            {
                DialogResult = true;
                this.Close();
            };
        }

        public void SaveSelection()
        {
            RegistryEx.Write("SampleRate", (int)(cbSampleRate.SelectedItem as ComboBoxItem).Tag, "ASIO");

            int buffer = 1024;
            ComboBoxItem latencyItem = (ComboBoxItem)cbLatency.SelectedItem;
            if (latencyItem != null)
            {
                buffer = (int)latencyItem.Tag;
            }
            RegistryEx.Write("BufferSize", buffer, "ASIO");
        }
    }
}
