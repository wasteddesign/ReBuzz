using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.FileBrowser;
using BuzzGUI.Interfaces;
using BuzzGUI.WaveformControl;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

namespace BuzzGUI.WavetableView
{
    public class WavetableVM : INotifyPropertyChanged
    {
        IWavetable wavetable;
        private WaveformVM waveformVm;

        public static WavetableVMViewSettings WavetableVMViewSettings { get; set; }

        public ICommand CutCommand { get; private set; }
        public ICommand CopyCommand { get; private set; }
        public ICommand PasteCommand { get; private set; }
        public ICommand UndoCommand { get; private set; }
        public ICommand RedoCommand { get; private set; }

        public IWavetable Wavetable
        {
            get { return wavetable; }
            set
            {
                if (wavetable != null)
                {
                    Global.GeneralSettings.PropertyChanged -= new PropertyChangedEventHandler(GeneralSettings_PropertyChanged);
                    wavetable.WaveChanged -= wavetable_WaveChanged;
                    wavetable.Song.MachineAdded -= Song_MachineAdded;
                    wavetable.Song.MachineRemoved -= Song_MachineRemoved;

                    Global.Buzz.PropertyChanged -= Buzz_PropertyChanged;
                }

                wavetable = value;

                if (wavetable != null)
                {
                    Global.GeneralSettings.PropertyChanged += new PropertyChangedEventHandler(GeneralSettings_PropertyChanged);
                    wavetable.WaveChanged += wavetable_WaveChanged;
                    wavetable.Song.MachineAdded += Song_MachineAdded;
                    wavetable.Song.MachineRemoved += Song_MachineRemoved;

                    Global.Buzz.PropertyChanged += Buzz_PropertyChanged;
                    waves = new ObservableCollection<WaveSlotVM>();
                    var w = wavetable.Waves;

                    for (int i = 0; i < wavetable.Waves.Count; i++)
                        waves.Add(new WaveSlotVM(this, i) { Wave = w[i], EditContext = WavetableVMViewSettings.EditContext });

                    waveformVm.Wavetable = wavetable;
                }

                PropertyChanged.RaiseAll(this);
            }
        }

        private void Buzz_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ActiveView")
            {
                if (Global.Buzz.ActiveView == BuzzView.WaveTableView)
                {
                    if (Global.Buzz.EditContext != WavetableVMViewSettings.EditContext)
                    {
                        Global.Buzz.EditContext = WavetableVMViewSettings.EditContext;
                    }
                }
            }
        }

        void GeneralSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "WPFIdealFontMetrics":
                    PropertyChanged.Raise(this, "TextFormattingMode");
                    break;
            }
        }

        private int selectedWaveIndex = 0;
        public int SelectedWaveIndex
        {
            get { return selectedWaveIndex; }
            set
            {
                selectedWaveIndex = value;
            }
        }
        private WaveSlotVM selectedItem;
        public WaveSlotVM SelectedItem
        {
            get { return selectedItem; }
            set
            {
                //unsubscribe the event of the previous item (if there was one)
                if (selectedItem != null)
                {
                    selectedItem.PropertyChanged -= selectedItem_PropertyChanged;
                }

                //change the item
                selectedItem = value;

                if (selectedItem != null)
                {
                    WaveformVm.SelectedWave = value.Wave;

                    if (value.SelectedLayer != null)
                    {
                        WaveformVm.Waveform = value.SelectedLayer.Layer;
                    }
                    else
                    {
                        WaveformVm.Waveform = null;
                    }

                    //subscribe to the event again
                    selectedItem.PropertyChanged += selectedItem_PropertyChanged;
                }

                PropertyChanged.RaiseAll(this);
            }
        }

        void selectedItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("SelectedLayer"))
            {
                if (selectedItem.SelectedLayer != null)
                {
                    if (waveformVm.SelectedWave == null)
                    {
                        //fixes problem with no wave selected after loading a bmx resulting in exception when using editor
                        waveformVm.SelectedWave = selectedItem.Wave;
                    }
                    WaveformVm.Waveform = selectedItem.SelectedLayer.Layer;
                }
                else
                {
                    WaveformVm.Waveform = null;
                }
            }
        }

        private IWaveLayer SelectedWaveform()
        {
            if (waves[selectedWaveIndex].Wave == null) return null;
            return waves[selectedWaveIndex].Wave.Layers.FirstOrDefault();
        }

        public ICommand PlayFileCommand { get; private set; }
        public ICommand FileKeyDownCommand { get; private set; }

        public WaveformVM WaveformVm
        {
            get { return waveformVm; }
            private set
            {
                waveformVm = value;
                waveformVm.EditedWaveChanged += WaveformVm_EditedWaveChanged;
                PropertyChanged.RaiseAll(this);
            }
        }

        private void WaveformVm_EditedWaveChanged(object sender, EventArgs e)
        {
            SelectedItem = Waves[waveformVm.SelectedSlotIndex];
            SelectedItem.SelectedLayer = SelectedItem.Layers[waveformVm.SelectedLayerIndex];
        }

        public WavetableVM()
        {
            PlayFileCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { wavetable.PlayWave((x as FSItemVM).FullPath); }
            };

            FileKeyDownCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    var p = x as Tuple<FSItemVM, KeyEventArgs>;
                    if (p.Item2.Key == Key.Space || p.Item2.Key == Key.Right)
                    {
                        if (p.Item1.IsFile)
                            wavetable.PlayWave(p.Item1.FullPath);
                    }
                    else if (p.Item2.Key == Key.Back)
                    {
                        if (p.Item1.IsFile)
                            LoadWaves(SelectedWaveIndex, new FSItemVM[] { p.Item1 }, false);
                    }
                }
            };

            CutCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => false,
                ExecuteDelegate = x =>
                {
                }
            };

            CopyCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => false,
                ExecuteDelegate = x =>
                {
                }
            };

            PasteCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => false,
                ExecuteDelegate = p =>
                {
                }
            };

            UndoCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => WavetableVMViewSettings.EditContext.ActionStack.CanUndo,
                ExecuteDelegate = p =>
                {
                    WavetableVMViewSettings.EditContext.ActionStack.Undo();
                }
            };

            RedoCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => WavetableVMViewSettings.EditContext.ActionStack.CanRedo,
                ExecuteDelegate = p =>
                {
                    WavetableVMViewSettings.EditContext.ActionStack.Redo();
                }
            };

            WavetableVMViewSettings = new WavetableVMViewSettings(this);
            wavePlayerMachines.Add(new MachineVM(null));
            WaveformVm = new WaveformVM();
            WaveformVm.EditContext = WavetableVMViewSettings.EditContext;

            StickyFocus = true;
        }

        ObservableCollection<WaveSlotVM> waves;
        public ObservableCollection<WaveSlotVM> Waves
        {
            get
            {
                return waves;
            }
        }

        void wavetable_WaveChanged(int i)
        {
            waves[i].Wave = wavetable.Waves[i];
            if (i == selectedWaveIndex && waveformVm.Waveform == null)
            {
                waveformVm.Waveform = SelectedWaveform();
                //OnPropertyChanged("WaveformVm"); //doesn't work, probably checks if its the same, we should new the waveformVM really
            }

            // Hack to force wave element update
            var tmp = selectedItem.SelectedLayer;
            selectedItem.SelectedLayer = null;
            selectedItem.SelectedLayer = tmp;
            PropertyChanged.Raise(this, "WaveformVm");
        }

        public class MachineVM
        {
            public IMachine Machine { get; private set; }
            public override string ToString() { return Machine != null ? Machine.Name : "<select machine>"; }
            public MachineVM(IMachine m) { Machine = m; }
        }

        readonly ObservableCollection<MachineVM> wavePlayerMachines = new ObservableCollection<MachineVM>();
        public ObservableCollection<MachineVM> WavePlayerMachines { get { return wavePlayerMachines; } }


        void Song_MachineAdded(IMachine m)
        {
            if ((m.DLL.Info.Flags & MachineInfoFlags.PLAYS_WAVES) != 0)
                wavePlayerMachines.Add(new MachineVM(m));
        }

        void Song_MachineRemoved(IMachine m)
        {
            var mi = wavePlayerMachines.FirstOrDefault(x => x.Machine == m);
            if (mi != null) wavePlayerMachines.Remove(mi);
        }

        public void LoadWaves(int index, IEnumerable waves, bool add)
        {
            if (waves != null)
            {
                foreach (var item in waves)
                {
                    if (item is FSItemVM)
                    {
                        var fsi = (FSItemVM)item;

                        if (fsi.IsFile)
                        {
                            wavetable.LoadWaveEx(index, fsi.FullPath, System.IO.Path.GetFileNameWithoutExtension(fsi.Name), add);
                            if (++index >= 200) break;
                        }
                    }
                    else if (item is string)
                    {
                        string path = (string)item;
                        wavetable.LoadWaveEx(index, path, System.IO.Path.GetFileNameWithoutExtension(path), add);
                        if (++index >= 200) break;
                    }
                }
                SelectedItem = Waves[SelectedWaveIndex];
            }
            else
            {
                wavetable.LoadWave(index, null, null, false);
            }

            if (!StickyFocus)
            {
                SelectedWaveIndex = FindNextAvailableIndex(SelectedWaveIndex, wavetable.Waves);
                OnPropertyChanged("SelectedWaveIndex");
            }
        }

        public struct LoadWaveRef
        {
            public int Index;
            public string FullPath;
            public string Name;
            public bool Add;
        }

        public IEnumerable<LoadWaveRef> PrepareLoadWaves(int index, IEnumerable waves, bool add)
        {
            List<LoadWaveRef> filesToLoad = new List<LoadWaveRef>();

            if (waves != null)
            {
                foreach (var item in waves)
                {
                    if (item is FSItemVM)
                    {
                        var fsi = (FSItemVM)item;

                        if (fsi.IsFile)
                        {
                            filesToLoad.Add(new LoadWaveRef() { Index = index, FullPath = fsi.FullPath, Name = System.IO.Path.GetFileNameWithoutExtension(fsi.Name), Add = add });
                            if (++index >= 200) break;
                        }
                    }
                    else if (item is string)
                    {
                        string path = (string)item;
                        filesToLoad.Add(new LoadWaveRef() { Index = index, FullPath = path, Name = System.IO.Path.GetFileNameWithoutExtension(path), Add = add });
                        if (++index >= 200) break;
                    }
                }
            }

            return filesToLoad;
        }

        internal void UpdateFocus()
        {
            if (!StickyFocus)
            {
                SelectedWaveIndex = FindNextAvailableIndex(SelectedWaveIndex, wavetable.Waves);
                OnPropertyChanged("SelectedWaveIndex");
            }
        }

        public bool StickyFocus { get; set; }

        private int FindNextAvailableIndex(int begin, ReadOnlyCollection<IWave> waves)
        {
            for (int i = begin; i < waves.Count; i++)
            {
                if (waves[i] == null) return i;
            }
            return 0;
        }

        protected void OnPropertyChanged(string field)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(field));
        }

        private void Undo()
        {
            var ac = WaveformVm.EditContext.ActionStack;
            if (ac != null && ac.CanUndo)
                ac.Undo();
        }

        private void Redo()
        {
            var ac = WaveformVm.EditContext.ActionStack;
            if (ac != null && ac.CanRedo)
                ac.Redo();
        }

        public IList<string> ExtensionFilter { get { return wavetable.GetSupportedFileTypeExtensions(); } }

        public TextFormattingMode TextFormattingMode { get { return Global.GeneralSettings.WPFIdealFontMetrics ? TextFormattingMode.Ideal : TextFormattingMode.Display; } }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
