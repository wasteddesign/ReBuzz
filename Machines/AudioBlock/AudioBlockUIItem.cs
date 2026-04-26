using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WDE.AudioBlock
{
    /// <summary>
    /// Big class to handle all UI functionalities in a wave row.
    /// </summary>
    public class AudioBlockUIItem : StackPanel
    {
        private int audioBlockIndex;
        public ComboBox pattern;
        public ComboBox audioSample;
        int[] wtIndexes;
        public NumericUpDown numBoxOffsetMs;
        public NumericUpDown numBoxOffsetSeconds;
        private ContextMenu cmPat;

        private Knob knob;

        AudioBlock audioBlockMachine;
        MenuItem miColor;
        public AudioBlockGUI audioBlockMachineGUI { get; private set; }

        public static double KNOB_1DB_MIDPOINT = 200;
        public static double KNOB_MAXIMUM = 900;
        public AudioBlockUIItem(int index)
        {
            this.audioBlockIndex = index;
            this.Orientation = Orientation.Horizontal;

            pattern = new ComboBox() { BorderThickness = new Thickness(0, 0, 0, 0), Width = 100, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 4, 4, 4) };
            audioSample = new ComboBox() { BorderThickness = new Thickness(0, 0, 0, 0), Width = 300, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 4, 4, 4), AllowDrop = true, IsReadOnly = false };

            numBoxOffsetMs = new NumericUpDown() { Width = 80, Height = 20, DecimalPlaces = 1, Change = (decimal)0.1, Minimum = (decimal)-1000.0, Maximum = (decimal)1000.0, Margin = new Thickness(4, 4, 4, 4) };
            numBoxOffsetSeconds = new NumericUpDown() { Width = 80, Height = 20, DecimalPlaces = 0, Change = (decimal)1, Minimum = (decimal)-1000, Maximum = (decimal)1000, Margin = new Thickness(4, 4, 4, 4) };

            cmPat = new ContextMenu() { Margin = new Thickness(4, 4, 4, 4) };
            var mi = new MenuItem();
            mi.Header = "Add New Pattern";
            mi.Click += Mi_Click_AddNew;
            cmPat.Items.Add(mi);
            mi = new MenuItem();
            mi.Header = "Delete Pattern";
            mi.Click += Mi_Click_Delete;
            cmPat.Items.Add(mi);
            cmPat.MaxHeight = 200;

            miColor = new MenuItem();
            miColor.Header = "Color";
            cmPat.Items.Add(miColor);

            object dummySub = new object();

            miColor.Items.Add(dummySub);
            miColor.SubmenuOpened += delegate
            {
                if (miColor.Items[0].GetType() == typeof(object))
                {
                    miColor.Items.Clear();

                    Array values = typeof(Brushes).GetProperties().
                        Select(p => new { Name = p.Name, Brush = p.GetValue(null) as Brush }).
                        ToArray();

                    Type colorsType = typeof(System.Windows.Media.Colors);
                    PropertyInfo[] colorsTypePropertyInfos = colorsType.GetProperties(BindingFlags.Public | BindingFlags.Static);

                    foreach (PropertyInfo colorsTypePropertyInfo in colorsTypePropertyInfos)
                    {
                        mi = new MenuItem();
                        mi.Header = colorsTypePropertyInfo.Name;
                        mi.Icon = new Rectangle() { Height = 16, Width = 30, Stroke = Brushes.Black, Fill = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(colorsTypePropertyInfo.Name)) };
                        miColor.Items.Add(mi);
                        mi.Click += Mi_Click_Change_Color;
                    }
                }
            };

            pattern.ContextMenu = cmPat;

            knob = new Knob() { Minimum = 0, Maximum = 900, Value = KNOB_1DB_MIDPOINT, Margin = new Thickness(6, 2, 2, 2), Height = 20 };
            ToolTip knobToolTip = new ToolTip();
            knob.ToolTip = knobToolTip;
            knobToolTip.Content = string.Format("{0:0.0} dB", Decibel.FromAmplitude(knob.Value / KNOB_1DB_MIDPOINT));
            knob.MouseLeave += Knob_MouseLeave;

            this.Children.Add(pattern);
            this.Children.Add(audioSample);
            this.Children.Add(numBoxOffsetSeconds);
            this.Children.Add(numBoxOffsetMs);
            this.Children.Add(knob);

            ContextMenu cmResample = new ContextMenu();
            mi = new MenuItem();
            mi.Header = "Resample to Buzz Sample Rate";
            mi.Click += Mi_Click;
            cmResample.Items.Add(mi);

            mi = new MenuItem();
            mi.Header = "Change Pitch...";
            mi.Click += Mi_ClickPitch;
            cmResample.Items.Add(mi);

            mi = new MenuItem();
            mi.Header = "Change Tempo...";
            mi.Click += Mi_ClickTempo;
            cmResample.Items.Add(mi);

            mi = new MenuItem();
            mi.Header = "Change Rate...";
            mi.Click += Mi_ClickRate; ;
            cmResample.Items.Add(mi);

            mi = new MenuItem();
            mi.Header = "Detect BPM...";
            mi.Click += Mi_DetectBpm;
            cmResample.Items.Add(mi);

            mi = new MenuItem();
            mi.Header = "AI Noise Suppression...";
            mi.Click += Mi_NoiseRemoval;
            cmResample.Items.Add(mi);

            audioSample.ContextMenu = cmResample;
            audioSample.ContextMenuOpening += ContextMenu_ContextMenuOpening;

            Loaded += AudioBlockUIItem_Loaded;
            Unloaded += AudioBlockUIItem_Unloaded;
            
        }

        private void AudioBlockUIItem_Unloaded(object sender, RoutedEventArgs e)
        {
            //DisableEvents();
        }

        private void AudioBlockUIItem_Loaded(object sender, RoutedEventArgs e)
        {   
        }

        private void Mi_NoiseRemoval(object sender, RoutedEventArgs e)
        {
            NoiseSuppressionWindow wnd = new NoiseSuppressionWindow();
            wnd.Resources.MergedDictionaries.Add(audioBlockMachine.host.Machine.ParameterWindow.Resources);
            wnd.ShowDialog();

            if (wnd.DialogResult.HasValue && wnd.DialogResult.Value)
            {

                int wtIndex = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex;
                if (wtIndex >= 0)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    audioBlockMachine.WaveUndo.SaveData(wtIndex);
                    Effects.RNNoiseRemoval(audioBlockMachine, wtIndex, wnd.SelectedModel.FilePath);
                    Mouse.OverrideCursor = null;

                    audioBlockMachine.UpdateEnvData(audioBlockIndex);
                    audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex);
                    audioBlockMachine.NotifyBuzzDataChanged();
                }
            }
        }

        private void Mi_DetectBpm(object sender, RoutedEventArgs e)
        {
            int waveIndex = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex;

            if (waveIndex >= 0)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                float bpm = Effects.DetectBpm(audioBlockMachine, waveIndex);
                Mouse.OverrideCursor = null;
                BpmDialog inputDialogNumber = new BpmDialog(audioBlockMachine.host.Machine.ParameterWindow.Resources, bpm);
                inputDialogNumber.ShowDialog();
            }
        }

        private void Mi_ClickRate(object sender, RoutedEventArgs e)
        {
            InputDialogNumber inputDialogNumber = new InputDialogNumber(audioBlockMachine.host.Machine.ParameterWindow.Resources, "Change rate (percents)", 0, -50, 100, 1, (decimal)0.1);
            inputDialogNumber.ShowDialog();
            if (inputDialogNumber.DialogResult.HasValue && inputDialogNumber.DialogResult.Value)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                int wtIndex = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex;
                audioBlockMachine.WaveUndo.SaveData(wtIndex);
                Effects.ChangeRate(audioBlockMachine, wtIndex, (float)inputDialogNumber.GetAnswer(), (int)inputDialogNumber.numSequence.Value, (int)inputDialogNumber.numSeekWindow.Value, (int)inputDialogNumber.numOverlap.Value);
                Mouse.OverrideCursor = null;

                audioBlockMachine.UpdateEnvData(audioBlockIndex);
                audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex, WaveUpdateEventType.ChangeRate);
                audioBlockMachine.NotifyBuzzDataChanged();
            }
        }

        private void Mi_ClickTempo(object sender, RoutedEventArgs e)
        {
            InputDialogNumber inputDialogNumber = new InputDialogNumber(audioBlockMachine.host.Machine.ParameterWindow.Resources, "Change tempo (percents)", 0, -50, 100, 1, (decimal)0.1);
            inputDialogNumber.ShowDialog();
            if (inputDialogNumber.DialogResult.HasValue && inputDialogNumber.DialogResult.Value)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                int wtIndex = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex;
                audioBlockMachine.WaveUndo.SaveData(wtIndex);
                Effects.ChangeTempo(audioBlockMachine, wtIndex, (float)inputDialogNumber.GetAnswer(), (int)inputDialogNumber.numSequence.Value, (int)inputDialogNumber.numSeekWindow.Value, (int)inputDialogNumber.numOverlap.Value);
                Mouse.OverrideCursor = null;

                audioBlockMachine.UpdateEnvData(audioBlockIndex);
                audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex, WaveUpdateEventType.ChangeTempo);
                audioBlockMachine.NotifyBuzzDataChanged();
            }
        }

        private void Mi_ClickPitch(object sender, RoutedEventArgs e)
        {
            InputDialogNumber inputDialogNumber = new InputDialogNumber(audioBlockMachine.host.Machine.ParameterWindow.Resources, "Change pitch semitones", 0, -60, 60, 1, (decimal)0.1);
            inputDialogNumber.ShowDialog();
            if (inputDialogNumber.DialogResult.HasValue && inputDialogNumber.DialogResult.Value)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                int wtIndex = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex;
                audioBlockMachine.WaveUndo.SaveData(wtIndex);
                Effects.ChangePitchSemitones(audioBlockMachine, wtIndex, (float)inputDialogNumber.GetAnswer(), (int)inputDialogNumber.numSequence.Value, (int)inputDialogNumber.numSeekWindow.Value, (int)inputDialogNumber.numOverlap.Value);
                Mouse.OverrideCursor = null;

                audioBlockMachine.UpdateEnvData(audioBlockIndex);
                audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex, WaveUpdateEventType.ChangePitch);
                audioBlockMachine.NotifyBuzzDataChanged();
            }
        }

        private void ContextMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            int wtIndex = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex;

            // Don't show menu if no wave selected or samplerate is already same as in Buzz
            if (wtIndex < 0 /*|| (audioBlockMachine.GetSampleFrequency(wtIndex) == Global.Buzz.SelectedAudioDriverSampleRate) */)
            {
                FrameworkElement fe = e.Source as FrameworkElement;
                ContextMenu cm = fe.ContextMenu;
                e.Handled = true;
            }
        }

        private void Mi_Click(object sender, RoutedEventArgs e)
        {
            ResampleWave();
        }

        public void ResampleWave()
        {
            int wtIndex = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex;
            if (wtIndex >= 0)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                audioBlockMachine.WaveUndo.SaveData(wtIndex);
                Effects.Resample(audioBlockMachine, wtIndex);
                Mouse.OverrideCursor = null;

                audioBlockMachine.UpdateEnvData(audioBlockIndex);
                audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex);
                audioBlockMachine.NotifyBuzzDataChanged();
            }
        }

        private void AutoResampleWave()
        {
            if (audioBlockMachine.MachineState.AutoResample)
                ResampleWave();
        }

        private void Mi_Click_Change_Color(object sender, RoutedEventArgs e)
        {
            MenuItem me = (MenuItem)sender;
            Color selectedColor = (Color)System.Windows.Media.ColorConverter.ConvertFromString((string)me.Header);
            int color = (selectedColor.R << 16) +
                (selectedColor.G << 8) +
                selectedColor.B;

            audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].Color = color;
            miColor.Icon = new Rectangle() { Height = 16, Width = 30, Stroke = Brushes.Black, Fill = Utils.GetBrushForPattern(audioBlockMachine, audioBlockIndex) };

            WaveUpdateEventType ev = WaveUpdateEventType.ColorChanged;
            audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex, ev);

            audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex);
            audioBlockMachine.NotifyBuzzDataChanged();
        }

        private void Mi_Click_Delete(object sender, RoutedEventArgs e)
        {
            IPattern pat = audioBlockMachine.GetPattern(audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].Pattern);
            if (pat != null)
            {
                audioBlockMachine.RemoveSequence(pat);
                audioBlockMachine.host.Machine.DeletePattern(pat);
                audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].Pattern = "";
                WaveUpdateEventType ev = WaveUpdateEventType.PatternChanged;
                audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex, ev);
                audioBlockMachine.NotifyBuzzDataChanged();
            }
        }

        private void Knob_MouseLeave(object sender, MouseEventArgs e)
        {
            ToolTip tt = ((ToolTip)knob.ToolTip);
            tt.IsOpen = false;
        }

        private void Knob_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToolTip tt = ((ToolTip)knob.ToolTip);
            tt.IsOpen = false;
        }

        private void ShowKnobTooltip()
        {
            ToolTip tt = ((ToolTip)knob.ToolTip);
            tt.Content = string.Format("{0:0.0} dB", Decibel.FromAmplitude(knob.Value / KNOB_1DB_MIDPOINT));
            tt.IsOpen = true;
        }

        private void Knob_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].Gain = (float)(knob.Value / KNOB_1DB_MIDPOINT);
            audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex);
            ShowKnobTooltip();
            audioBlockMachine.NotifyBuzzDataChanged();
        }

        private void Knob_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            knob.Value = KNOB_1DB_MIDPOINT;
        }

        private void Mi_Click_AddNew(object sender, RoutedEventArgs e)
        {
            var wt = audioBlockMachine.host.Machine.Graph.Buzz.Song.Wavetable;
            int waveIndex = audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex;

            int patternLength = 16;
            if (waveIndex >= 0)
            {
                patternLength = audioBlockMachine.CalculatePatternLength(wt.Waves[waveIndex].Layers[0].SampleCount, wt.Waves[waveIndex].Layers[0].SampleRate,
                    audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].OffsetInMs / 1000.0 + audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].OffsetInSeconds);
            }
            string newPattern = audioBlockMachine.AddPattern(patternLength);
            audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].Pattern = newPattern;
            audioBlockMachineGUI.UpdateUI();
            audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex);
            audioBlockMachine.RefreshBuzzViews();
            audioBlockMachine.NotifyBuzzDataChanged();
        }

        public void DisableEvents()
        {
            pattern.SelectionChanged -= Pattern_SelectionChanged;
            numBoxOffsetSeconds.ValueChanged -= NumBoxOffsetSeconds_ValueChanged;
            numBoxOffsetMs.ValueChanged -= NumBoxOffsetMs_ValueChanged;

            audioSample.SelectionChanged -= AudioSample_SelectionChanged;
            audioSample.PreviewDragEnter -= (sender, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
            audioSample.PreviewDragOver -= (sender, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
            audioSample.Drop -= AudioSample_Drop;

            knob.MouseRightButtonDown -= Knob_MouseRightButtonDown;
            knob.ValueChanged -= Knob_ValueChanged;
            knob.PreviewMouseLeftButtonUp -= Knob_PreviewMouseLeftButtonUp;
        }

        public void EnableEvents()
        {
            pattern.SelectionChanged += Pattern_SelectionChanged;
            numBoxOffsetMs.ValueChanged += NumBoxOffsetMs_ValueChanged;
            numBoxOffsetSeconds.ValueChanged += NumBoxOffsetSeconds_ValueChanged;

            audioSample.SelectionChanged += AudioSample_SelectionChanged;
            audioSample.PreviewDragEnter += (sender, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
            audioSample.PreviewDragOver += (sender, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
            audioSample.Drop += AudioSample_Drop;

            knob.MouseRightButtonDown += Knob_MouseRightButtonDown;
            knob.ValueChanged += Knob_ValueChanged;
            knob.PreviewMouseLeftButtonUp += Knob_PreviewMouseLeftButtonUp;
        }

        private void AudioSample_Drop(object sender, DragEventArgs e)
        {
            Utils.DragDropHelper(audioBlockMachine, audioBlockIndex, sender, e);
        }

        private void NumBoxOffsetMs_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {

            if (!audioBlockMachine.UpdateOffsets(audioBlockIndex, audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].OffsetInSeconds, (double)numBoxOffsetMs.Value))
            {
                // Not success, reset value
                numBoxOffsetMs.Value = (decimal)audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].OffsetInMs;
            }

            audioBlockMachine.SetPatternLength(audioBlockIndex); // Calling this too fast seem to cause issues.
            audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex);
            //Thread.Sleep(20);
            audioBlockMachine.NotifyBuzzDataChanged();

        }

        private void NumBoxOffsetSeconds_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            if (!audioBlockMachine.UpdateOffsets(audioBlockIndex, (double)numBoxOffsetSeconds.Value, audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].OffsetInMs))
            {
                // Not success, reset value
                numBoxOffsetSeconds.Value = (decimal)audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].OffsetInSeconds;
            }

            audioBlockMachine.SetPatternLength(audioBlockIndex);
            audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex);
            //Thread.Sleep(20);
            audioBlockMachine.NotifyBuzzDataChanged();
        }

        private void AudioSample_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // cbIndex ignores the empty item in cb
            int cbIndex = audioSample.SelectedIndex - 1;
            
            if (cbIndex < 0)
            {
                lock (AudioBlock.syncLock)
                {
                    audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex = -1;
                    audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].OffsetInMs = 0.0;
                    audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].OffsetInSeconds = 0.0;
                    audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].Gain = 1.0f;
                    audioBlockMachine.ResetPanEnvPoints(audioBlockIndex);
                    audioBlockMachine.ResetVolEnvPoints(audioBlockIndex);
                }
                audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex);
            }
            else
            {
                lock (AudioBlock.syncLock)
                {
                    audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex = this.wtIndexes[cbIndex];
                    audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].OffsetInMs = 0.0;
                    audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].OffsetInSeconds = 0.0;
                    audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].Gain = 1.0f;
                }
                AutoResampleWave();

                ((ComboBox)sender).ToolTip = "Wave View Length: " + string.Format("{0:0.0}", audioBlockMachine.GetSampleLenghtInSeconds(audioBlockIndex)) + " Seconds.";

                audioBlockMachine.UpdateEnvData(audioBlockIndex);
                audioBlockMachine.SetPatternLength(audioBlockIndex);
                WaveUpdateEventType ev = WaveUpdateEventType.NewWaveAdded;
                audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex, ev);
            }

            audioBlockMachineGUI.UpdateUI();
            audioBlockMachine.NotifyBuzzDataChanged();

            e.Handled = true;
        }

        private void Pattern_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool found = false;
            // Only allow pattern to be linked once
            for (int i = 0; i < AudioBlock.NUMBER_OF_AUDIO_BLOCKS; i++)
            {
                if (audioBlockMachine.MachineState.AudioBlockInfoTable[i].Pattern == (string)pattern.SelectedItem)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].Pattern = (string)pattern.SelectedItem;
                if (audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex >= 0)
                {
                    audioBlockMachine.SetPatternLength(audioBlockIndex);
                }
            }
            else
            {
                audioBlockMachine.MachineState.AudioBlockInfoTable[audioBlockIndex].Pattern = "";
                pattern.SelectedItem = "";
            }

            WaveUpdateEventType ev = WaveUpdateEventType.PatternChanged;
            audioBlockMachine.RaiseUpdateWaveGraphEvent(audioBlockIndex, ev);
            audioBlockMachine.NotifyBuzzDataChanged();

        }

        public void SetAudioBlockMachine(AudioBlock audioBlockMachine, AudioBlockGUI audioBlockMachineGUI)
        {
            this.audioBlockMachine = audioBlockMachine;
            this.audioBlockMachineGUI = audioBlockMachineGUI;
        }

        public void PopulateData()
        {
            var wt = audioBlockMachine.host.Machine.Graph.Buzz.Song.Wavetable;
            audioSample.Items.Clear();
            wtIndexes = new int[wt.Waves.Count()];
            int i = 0;

            audioSample.Items.Add("");
            foreach (IWave wave in wt.Waves)
            {
                if (wave != null)
                {
                    audioSample.Items.Add(wave.Name);
                    wtIndexes[i] = wave.Index;
                    i++;
                }
            }

            pattern.Items.Clear();
            pattern.Items.Add("");
            foreach (IPattern pat in audioBlockMachine.host.Machine.Patterns)
            {
                pattern.Items.Add(pat.Name);
            }

            numBoxOffsetSeconds.Value = 0;
            numBoxOffsetMs.Value = 0;
        }

        public void RestoreSelection(string selPattern, int selWavetableWave, double selOffsetSeconds, double selOffsetMs, double gain)
        {
            bool found = false;
            int cbi;

            for (cbi = 0; cbi < wtIndexes.Length; cbi++)
            {
                if (wtIndexes[cbi] == selWavetableWave)
                {
                    found = true;
                    break;
                }
            }
            if (found)
            {
                audioSample.SelectedIndex = cbi + 1;
                audioSample.ToolTip = "Wave View Length: " + string.Format("{0:0.0}", audioBlockMachine.GetSampleLenghtInSeconds(audioBlockIndex)) + " Seconds.";
            }
            else
                audioSample.SelectedIndex = 0;

            pattern.SelectedItem = selPattern;
            numBoxOffsetSeconds.Value = (decimal)selOffsetSeconds;
            numBoxOffsetMs.Value = (decimal)selOffsetMs;
            knob.Value = gain * KNOB_1DB_MIDPOINT;
            ((ToolTip)knob.ToolTip).Content = string.Format("{0:0.0} dB", Decibel.FromAmplitude(knob.Value / KNOB_1DB_MIDPOINT));

            miColor.Icon = new Rectangle() { Height = 16, Width = 30, Stroke = Brushes.Black, Fill = Utils.GetBrushForPattern(audioBlockMachine, audioBlockIndex) };
        }
    }
}
