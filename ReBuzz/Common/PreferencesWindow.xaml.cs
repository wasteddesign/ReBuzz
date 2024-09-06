using BuzzGUI.Common;
using NAudio.Midi;
using ReBuzz.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ReBuzz.Common
{
    /// <summary>
    /// Interaction logic for PreferencesWindow.xaml
    /// </summary>
    public partial class PreferencesWindow : Window, INotifyPropertyChanged
    {
        public class ControllerVM : INotifyPropertyChanged
        {
            public string Name { get; set; }
            public int Channel { get; set; }
            public int Controller { get; set; }
            public int Value { get; internal set; }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public class ControllerCheckboxVM
        {
            public int Id { get; set; }
            public string Label { get; set; }
            public bool Checked { get; set; }
        }

        public IList<ControllerCheckboxVM> MidiInControllerCheckboxes { get; set; } = new List<ControllerCheckboxVM>();
        public IList<ControllerCheckboxVM> MidiOutControllerCheckboxes { get; set; } = new List<ControllerCheckboxVM>();

        public PreferencesWindow(ReBuzzCore buzz)
        {
            DataContext = this;
            InitializeComponent();

            var midiInDevices = buzz.MidiInOutEngine.GetMidiInputDevices();
            for (int device = 0; device < MidiIn.NumberOfDevices; device++)
            {
                MidiInControllerCheckboxes.Add(new ControllerCheckboxVM { Id = device, Label = MidiIn.DeviceInfo(device).ProductName, Checked = midiInDevices.Contains(device) });
            }
            lbMidiInputs.InvalidateVisual();

            var midiOutDevices = buzz.MidiInOutEngine.GetMidiOutputDevices();
            for (int device = 0; device < MidiOut.NumberOfDevices; device++)
            {
                MidiOutControllerCheckboxes.Add(new ControllerCheckboxVM { Id = device, Label = MidiOut.DeviceInfo(device).ProductName, Checked = midiOutDevices.Contains(device) });
            }
            lbMidiOutputs.InvalidateVisual();

            btOk.Click += (sender, e) =>
            {
                DialogResult = true;
                this.Close();
            };

            this.Closed += (sender, e) =>
            {
                buzz = null;
            };

            int numWaveDirs = RegistryEx.Read("numWaveDirs", 0, "Settings");
            for (int i = 0; i < numWaveDirs; i++)
            {
                string dir = RegistryEx.Read("WaveDir" + i, "", "Settings");
                if (dir != "")
                {
                    lbWaveDirectories.Items.Add(dir);
                }
            }

            btAddWaveDirectory.Click += (sender, e) =>
            {
                var selectFolderDlg = new FolderPicker();
                {
                    if (selectFolderDlg.ShowDialog(this) == true)
                    {
                        var path = selectFolderDlg.ResultPath;
                        lbWaveDirectories.Items.Add(path);
                        WritePathsToRegistry();
                    }
                }
            };

            btRemoveWaveDirectory.Click += (sender, e) =>
            {
                List<string> removeItemsList = new List<string>();
                foreach (string item in lbWaveDirectories.SelectedItems)
                {
                    removeItemsList.Add(item);
                }
                foreach (string item in removeItemsList)
                {
                    lbWaveDirectories.Items.Remove(item);
                }

                WritePathsToRegistry();
            };

            long processorAffinityMask = RegistryEx.Read("ProcessorAffinity", 0xFFFFFFFF, "Settings");
            int processorCount = Environment.ProcessorCount;// >= 32 ? 31 : Environment.ProcessorCount;

            for (int i = 0; i < processorCount; i++)
            {
                bool cpuEnabled = (processorAffinityMask & (1L << i)) != 0;
                CheckBox cbCpu = new CheckBox() { Name = "CPU" + i, IsChecked = cpuEnabled, ToolTip = "CPU " + i };
                wpCPU.Children.Add(cbCpu);
            }

            int threadType = RegistryEx.Read("AudioThreadType", 0, "Settings");
            {
                ComboBoxItem cbi = new ComboBoxItem();
                cbi.Content = "Dedicated Scheduler";
                cbAudioThreadType.Items.Add(cbi);

                cbi = new ComboBoxItem();
                cbi.Content = "Thread";
                cbAudioThreadType.Items.Add(cbi);

                cbi = new ComboBoxItem();
                cbi.Content = "None";
                cbAudioThreadType.Items.Add(cbi);
            }

            cbAudioThreadType.SelectedIndex = threadType;

            int numThreads = RegistryEx.Read("AudioThreads", 2, "Settings");
            for (int i = 1; i <= 8; i++)
            {
                ComboBoxItem cbi = new ComboBoxItem();
                cbi.Content = "" + i;
                cbi.Tag = i;
                cbAudioThreads.Items.Add(cbi);
            }

            cbAudioThreads.SelectedIndex = numThreads - 1;

            int algorithm = RegistryEx.Read("WorkAlgorithm", 1, "Settings");
            {
                ComboBoxItem cbi = new ComboBoxItem();
                cbi.Content = "Recursive Task Groups";
                cbi.Tag = 0;
                cbAlgorithms.Items.Add(cbi);

                cbi = new ComboBoxItem();
                cbi.Content = "Recursive Tasks";
                cbi.Tag = 1;
                cbAlgorithms.Items.Add(cbi);

                cbi = new ComboBoxItem();
                cbi.Content = "Threads";
                cbi.Tag = 2;
                cbAlgorithms.Items.Add(cbi);
            }

            cbAlgorithms.SelectedIndex = algorithm;

            btAddController.Click += (sender, e) =>
            {
                MidiControllerAssignWindow mcaw = new MidiControllerAssignWindow();
                mcaw.Resources.MergedDictionaries.Add(this.Resources);
                if (mcaw.ShowDialog() == true)
                {
                    ControllerVM cVM = new ControllerVM();
                    try
                    {
                        cVM.Name = mcaw.Name.Text;
                        cVM.Channel = int.Parse(mcaw.Channel.Text);
                        cVM.Value = int.Parse(mcaw.Value.Text);
                        cVM.Controller = int.Parse(mcaw.Controller.Text);
                        lvControllers.Items.Add(cVM);
                    }
                    catch { }
                }
            };

            btModifyController.Click += (sender, e) =>
            {
                ControllerVM cVM = (ControllerVM)lvControllers.SelectedItem;

                if (cVM != null)
                {
                    MidiControllerAssignWindow mcaw = new MidiControllerAssignWindow();
                    mcaw.SelectedController(cVM);
                    mcaw.Resources.MergedDictionaries.Add(this.Resources);
                    if (mcaw.ShowDialog() == true)
                    {
                        try
                        {
                            cVM.Name = mcaw.Name.Text;
                            cVM.Channel = int.Parse(mcaw.Channel.Text);
                            cVM.Value = int.Parse(mcaw.Value.Text);
                            cVM.Controller = int.Parse(mcaw.Controller.Text);
                        }
                        catch { }
                        ICollectionView view = CollectionViewSource.GetDefaultView(lvControllers.Items.SourceCollection);
                        view.Refresh();
                    }
                }
            };

            btRemoveController.Click += (sender, e) =>
            {
                ControllerVM cVM = (ControllerVM)lvControllers.SelectedItem;

                if (cVM != null)
                {
                    lvControllers.Items.Remove(cVM);
                }
            };

            lvControllers.SelectionChanged += (sender, e) =>
            {
                IsControllerSelected = lvControllers.SelectedIndex != -1;
                PropertyChanged.Raise(this, "IsControllerSelected");
            };

            foreach (var controller in buzz.MidiControllerAssignments.MIDIControllers)
            {
                ControllerVM cVM = new ControllerVM();
                cVM.Name = controller.Name;
                cVM.Channel = controller.Channel + 1;
                cVM.Controller = controller.Contoller;
                cVM.Value = controller.Value;

                lvControllers.Items.Add(cVM);
            }
        }

        public bool IsControllerSelected { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public long GetProcessorAffinity()
        {
            long processorAffinityMask = 0;
            int processorCount = Environment.ProcessorCount;

            for (int i = 0; i < processorCount; i++)
            {
                CheckBox cbCpu = (CheckBox)wpCPU.Children[i];
                if (cbCpu.IsChecked == true)
                    processorAffinityMask |= (1L << i);
            }

            return processorAffinityMask;
        }

        void WritePathsToRegistry()
        {
            int numWaveDirs = lbWaveDirectories.Items.Count;
            RegistryEx.Write("numWaveDirs", numWaveDirs, "Settings");
            for (int i = 0; i < numWaveDirs; i++)
            {
                RegistryEx.Write("WaveDir" + i, lbWaveDirectories.Items[i], "Settings");
            }
        }
    }
}
