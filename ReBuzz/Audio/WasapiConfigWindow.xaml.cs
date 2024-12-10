using BuzzGUI.Common;
using NAudio.CoreAudioApi;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ReBuzz.Core;

namespace BuzzDotNet.Audio
{
    /// <summary>
    /// Interaction logic for WasapiConfigWindow.xaml
    /// </summary>
    public partial class WasapiConfigWindow : Window
    {
      private readonly IRegistryEx registryEx;

      public WasapiConfigWindow(IRegistryEx registryEx)
        {
            this.registryEx = registryEx;
            DataContext = this;
            InitializeComponent();

            // Out
            string wasapiDeviceID = this.registryEx.Read("DeviceID", "", "WASAPI");
            ComboBoxItem selectedItem = null;
            var enumerator = new MMDeviceEnumerator();
            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                ComboBoxItem comboBoxItem = new ComboBoxItem() { Content = wasapi.FriendlyName, Tag = wasapi.ID };
                cbDevices.Items.Add(comboBoxItem);
                if (wasapi.ID == wasapiDeviceID)
                    selectedItem = comboBoxItem;
            }

            if (selectedItem != null)
            {
                cbDevices.SelectedItem = selectedItem;
            }
            else
            {
                cbDevices.SelectedIndex = 0;
            }

            // In
            string wasapiDeviceIDIn = this.registryEx.Read("DeviceIDIn", "", "WASAPI");
            selectedItem = null;
            enumerator = new MMDeviceEnumerator();

            cbDevicesIn.Items.Add(new ComboBoxItem() { Content = "<None>", Tag = "" });

            foreach (var wasapi in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                ComboBoxItem comboBoxItem = new ComboBoxItem() { Content = wasapi.FriendlyName, Tag = wasapi.ID };
                cbDevicesIn.Items.Add(comboBoxItem);
                if (wasapi.ID == wasapiDeviceIDIn)
                    selectedItem = comboBoxItem;
            }

            if (selectedItem != null)
            {
                cbDevicesIn.SelectedItem = selectedItem;
            }
            else
            {
                cbDevicesIn.SelectedIndex = 0;
            }

            int wasapiDeviceSamplerate = this.registryEx.Read("SampleRate", 44100, "WASAPI");

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
                if ((int)(item as ComboBoxItem).Tag == wasapiDeviceSamplerate)
                {
                    srItem = item;
                    break;
                }
            cbSampleRate.SelectedItem = srItem;

            // Mode
            int wasapiMode = this.registryEx.Read("Mode", 0, "WASAPI");

            ComboBoxItem cbiMode = new ComboBoxItem() { Content = "Shared", Tag = 0 };
            cbMode.Items.Add(cbiMode);
            cbiMode = new ComboBoxItem() { Content = "Exclusive", Tag = 1 };
            cbMode.Items.Add(cbiMode);

            cbMode.SelectedIndex = wasapiMode;

            // Poll
            int wasapiPoll = this.registryEx.Read("Poll", 0, "WASAPI");

            ComboBoxItem cbiPoll = new ComboBoxItem() { Content = "False", Tag = 0 };
            cbPoll.Items.Add(cbiPoll);
            cbiPoll = new ComboBoxItem() { Content = "True", Tag = 1 };
            cbPoll.Items.Add(cbiPoll);

            cbPoll.SelectedIndex = wasapiPoll;

            // Latency
            int currentBufferSize = this.registryEx.Read("BufferSize", 1024, "WASAPI");
            int bufferSize = 16;
            int selectedIndex = 10;
            for (int i = 0; i < 10; i++)
            {
                double bufferLatency = 1000.0 * 2 * bufferSize / wasapiDeviceSamplerate;
                ComboBoxItem cbiLatency = new ComboBoxItem()
                {
                    Content = bufferSize + " (" + String.Format(CultureInfo.InvariantCulture,
                   "{0:0.0}", bufferLatency) + "ms)",
                    Tag = bufferSize
                };
                cbBufferSize.Items.Add(cbiLatency);

                if (currentBufferSize == bufferSize)
                {
                    selectedIndex = i;
                }
                bufferSize *= 2;
            }
            cbBufferSize.SelectedIndex = selectedIndex;

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
            registryEx.Write("DeviceID", (cbDevices.SelectedItem as ComboBoxItem).Tag, "WASAPI");
            registryEx.Write("DeviceIDIn", (cbDevicesIn.SelectedItem as ComboBoxItem).Tag, "WASAPI");
            registryEx.Write("SampleRate", (int)(cbSampleRate.SelectedItem as ComboBoxItem).Tag, "WASAPI");
            registryEx.Write("Mode", cbMode.SelectedIndex, "WASAPI");
            registryEx.Write("Poll", cbPoll.SelectedIndex, "WASAPI");

            int bufferSize = 1024;
            ComboBoxItem bsItem = (ComboBoxItem)cbBufferSize.SelectedItem;
            if (bsItem != null)
            {
                bufferSize = (int)bsItem.Tag;
            }
            registryEx.Write("BufferSize", bufferSize, "WASAPI");
        }
    }
}
