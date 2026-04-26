using BuzzGUI.Common;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static WDE.AudioBlock.AudioBlock;

namespace WDE.AudioBlock
{
    enum VolumeEnvelopeModes
    {
        dB0,
        dB6
    }

    /// <summary>
    /// Pan and Volume envelopes are pretty much copies with small differences.
    /// </summary>
    class EnvelopeLayerVolume : EnvelopeBase, IEnvelopeLayer
    {
        public static double ENVELOPE_MODE_0DB_MAX = 1.0;
        public static double ENVELOPE_MODE_6DB_MAX = 2.0;

        private VolumeEnvelopeModes volumeEnvelopeMode = VolumeEnvelopeModes.dB0;

        public EnvelopeLayerVolume() : base()
        {
            strokeBrush = Brushes.Black;
            fillBrush = Brushes.LightSkyBlue;
            lineBrush = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0));
        }

        private void AudioBlock_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // There is a bug somewhere, don't know the root cause. This will avoid issues...
            // Update: This is fixed but leaving here for now.
            if (envelopeBoxes.Count == 0)
            {
                return;
            }

            if (e.PropertyName == "VolumeEnvelopeChanged")
            {
                EnvelopeBox evbSender = (EnvelopeBox)sender;

                if (audioBlockIndex != evbSender.AudioBlockIndex)
                    return;

                UpdateEnvelopBox(evbSender.Index, evbSender.AudioBlockIndex);
                UpdatePolyLinePath();
            }
            else if (e.PropertyName == "VolumeEnvelopeVisibility")
            {
                EnvelopeBase evbSender = (EnvelopeBase)sender;

                if (audioBlockIndex != evbSender.audioBlockIndex)
                    return;

                envelopesCanvas.UpdateMenus();
                EnvelopeVisible = audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].VolumeEnvelopeVisible;

                if (EnvelopeVisible)
                    Draw();
            }
            else if (e.PropertyName == "VolumeEnvelopeBoxAdded")
            {
                EnvelopeBox evbSender = ((EnvelopeBox)sender).Clone(this);

                if (audioBlockIndex != evbSender.AudioBlockIndex)
                    return;

                evbSender.ContextMenu.Resources.MergedDictionaries.Add(this.envelopesCanvas.HostCanvas.ContextMenu.Resources);

                // Update EnvelopeLayer List
                int indexAdded = AddEnvelopeBoxToList(evbSender, evbSender.TimeStamp);

                // Update Visual
                AddEnvelopeBoxToCanvas(evbSender, indexAdded);
                evbSender.MouseLeftButtonDown += Evb_MouseLeftButtonDown;
                evbSender.Freezed = false;
                UpdateToolTip(evbSender);
                UpdatePolyLinePath();
            }
            else if (e.PropertyName == "VolumeEnvelopeBoxRemoved")
            {
                EnvelopeBox evbSender = (EnvelopeBox)sender;

                if (audioBlockIndex != evbSender.AudioBlockIndex)
                    return;

                int envelopeBoxIndex = ((EnvelopeBox)sender).Index;
                envPolyLine.Points.RemoveAt(envelopeBoxIndex);
                EnvelopeBox ebThis = envelopeBoxes[envelopeBoxIndex];
                ebThis.MouseLeftButtonDown -= Evb_MouseLeftButtonDown;
                envelopeBoxes.Remove(ebThis);
                this.Children.Remove(ebThis);

                UpdatePolyLinePath();
            }
            else if (e.PropertyName == "VolumeEnvelopeReDraw")
            {
                EnvelopeBase evbSender = (EnvelopeBase)sender;

                if (audioBlockIndex != evbSender.audioBlockIndex)
                    return;
                Draw();
                envelopesCanvas.UpdateMenus();
            }
            else if (e.PropertyName == "VolumeEnvBoxFreezedState")
            {
                EnvelopeBox evbSender = (EnvelopeBox)sender;

                if (audioBlockIndex != evbSender.AudioBlockIndex || evbSender.Index == -1)
                    return;

                EnvelopeBox eb = envelopeBoxes[evbSender.Index];
                eb.Freezed = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints[evbSender.Index].Freezed;

            }
            else if (e.PropertyName == "VolumeEnvBoxesFreezedState")
            {
                EnvelopeBase evbSender = (EnvelopeBase)sender;

                if (audioBlockIndex != evbSender.audioBlockIndex)
                    return;

                List<VolumeEnvelopePoint> vep = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints;
                for (int i = 0; i < vep.Count; i++)
                    envelopeBoxes[i].Freezed = vep[i].Freezed;
            }
        }

        public void UpdateEnvelopBox(int envIndex, int abIndex)
        {
            if (audioBlockIndex != abIndex)
                return;

            var envelopePoints = audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].VolumeEnvelopePoints;
            VolumeEnvelopePoint vep = envelopePoints[envIndex];
            EnvelopeBox evb = envelopeBoxes[envIndex];

            if (viewMode == ViewOrientationMode.Vertical)
            {
                Point point = new Point(ValueToScreen(vep.Value), ConvertTimeStampInSecondsToPixels(vep.TimeStamp));
                point = UpdateEnvelopeBoxCenterPosition(evb, point, false);

                if (point.Y < evb.Height / 2.0 && point.Y >= 0)
                    point.Y = evb.Height / 2.0;
                else if (point.Y > this.Height - evb.Height / 2.0 && point.Y < this.Height)
                    point.Y = this.Height - evb.Height / 2.0;

                Canvas.SetLeft(evb, point.X - evb.Width / 2.0);
                Canvas.SetTop(evb, point.Y - evb.Height / 2.0);

                evb.ToolTip = string.Format("Time: {0:0.0}, Vol: {1:0.0}", vep.TimeStamp, Decibel.FromAmplitude(vep.Value));
            }
            else
            {
                Point point = new Point(ConvertTimeStampInSecondsToPixels(vep.TimeStamp), ValueToScreen(vep.Value));
                point = UpdateEnvelopeBoxCenterPosition(evb, point, false);

                if (point.X < evb.Width / 2 && point.X >= 0)
                    point.X = evb.Width / 2;
                else if (point.X > this.Width - evb.Width / 2 && point.X < this.Width)
                    point.X = this.Width - evb.Width / 2;

                Canvas.SetLeft(evb, point.X - evb.Width / 2);
                Canvas.SetTop(evb, point.Y - evb.Height / 2);

                evb.ToolTip = string.Format("Time: {0:0.0}, Vol: {1:0.0}", vep.TimeStamp, Decibel.FromAmplitude(vep.Value));
            }
        }

        public void Init(AudioBlock ab, int audioBlockIndex, Envelopes c)
        {
            base.Init(ab, c);
            this.audioBlockIndex = audioBlockIndex;
            VolumeEnvelopeMode = (VolumeEnvelopeModes)audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeMode;

            LoadData();

            // Start always hidden
            EnvelopeVisible = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeVisible;

            this.Loaded += EnvelopeLayerVolume_Loaded;
            this.Unloaded += EnvelopeLayerVolume_Unloaded;

            envPolyLine.MouseEnter += (sender, e) =>
            {
                envPolyLine.StrokeThickness = 4;
                envPolyLine.Stroke = lineBrushSelected;
            };

            envPolyLine.MouseLeave += (sender, e) =>
            {
                envPolyLine.StrokeThickness = 2;
                envPolyLine.Stroke = lineBrush;
            };

            envPolyLine.MouseLeftButtonDown += (sender, e) =>
            {
                envelopesCanvas.SelectVolEnvelope();

                if (!(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                {
                    e.Handled = true;
                }
            };
        }

        private void EnvelopeLayerVolume_Loaded(object sender, RoutedEventArgs e)
        {
            envelopesCanvas.SizeChanged += EnvelopesCanvas_SizeChanged;

            envelopesCanvas.HostCanvas.MouseMove += C_MouseMove;
            envelopesCanvas.HostCanvas.MouseRightButtonDown += HostCanvas_MouseRightButtonDown;
            envelopesCanvas.HostCanvas.MouseLeftButtonDown += HostCanvas_MouseLeftButtonDown;
            envelopesCanvas.HostCanvas.MouseLeftButtonUp += HostCanvas_MouseLeftButtonUp;
            audioBlock.PropertyChanged += AudioBlock_PropertyChanged;

            this.Width = envelopesCanvas.Width;
            this.Height = envelopesCanvas.Height;

            Draw();
        }

        private void EnvelopeLayerVolume_Unloaded(object sender, RoutedEventArgs e)
        {
            envelopesCanvas.SizeChanged -= EnvelopesCanvas_SizeChanged;

            envelopesCanvas.HostCanvas.MouseMove -= C_MouseMove;
            envelopesCanvas.HostCanvas.MouseRightButtonDown -= HostCanvas_MouseRightButtonDown;
            envelopesCanvas.HostCanvas.MouseLeftButtonDown -= HostCanvas_MouseLeftButtonDown;
            envelopesCanvas.HostCanvas.MouseLeftButtonUp -= HostCanvas_MouseLeftButtonUp;
            audioBlock.PropertyChanged -= AudioBlock_PropertyChanged;

            ReleaseEnvelopeBoxes();

            envelopesCanvas.HostCanvas.ReleaseMouseCapture();
            Children.Clear();
            envelopesCanvas = null;
            audioBlock = null;
        }

        private void HostCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (boxDragged)
            {
                envelopesCanvas.HostCanvas.ReleaseMouseCapture();
                this.boxDragged = false;
                e.Handled = true;
            }
        }

        private void HostCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (envelopesCanvas.IsSelectedLayer(EEnvelopeType.Volume))
                {
                    Add_Point(newBoxPosition);
                    e.Handled = true;
                }
            }
        }

        internal void LoadData()
        {
            if (audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].VolumeEnvelopePoints.Count < 2)
            {
                VolumeEnvelopePoint vep = new VolumeEnvelopePoint();
                vep.Value = this.DefaulBoxValue();
                vep.TimeStamp = 0.0; // First time stamp starts always from 0 seconds.
                audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].VolumeEnvelopePoints.Add(vep);

                vep = new VolumeEnvelopePoint();
                vep.Value = this.DefaulBoxValue();
                vep.TimeStamp = audioBlock.GetSampleLengthInSecondsWithOffset(audioBlockIndex); // Last time stamp is the lenght of sample.
                audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].VolumeEnvelopePoints.Add(vep);
            }
        }

        public override void UpdatePolyLinePath()
        {
            envPolyLine.Points.Clear();
            envPolyLine.Stroke = lineBrush;
            envPolyLine.StrokeThickness = 2;

            if (viewMode == ViewOrientationMode.Vertical)
            {
                if (!audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeCurves)
                {
                    foreach (EnvelopeBox evb in envelopeBoxes)
                    {
                        Point point = new Point(ValueToScreen(GetEnvelopeValue(evb)), ConvertTimeStampInSecondsToPixels(GetEnvelopeTimeStamp(evb)));
                        point = UpdateEnvelopeBoxCenterPosition(evb, point, false);
                        envPolyLine.Points.Add(point);
                    }
                }
                else if (audioBlock.GetSplineVol(audioBlockIndex) != null)
                {
                    SplineCache spline = audioBlock.GetSplineVol(audioBlockIndex);
                    double slidingOffsetWindowInSeconds = envelopesCanvas.HostCanvas.SlidingWindowOffsetSeconds;

                    double realHeight = GetPixelsPerSecond() * DrawLengthInSeconds;

                    for (int i = 0; i < spline.YValues.Length; i++)
                    {
                        double y = realHeight * (((double)i * spline.StepSizeInSeconds - slidingOffsetWindowInSeconds) / DrawLengthInSeconds);
                        double x = this.envelopesCanvas.HostCanvas.Width * ((spline.YValues[i] / MaxBoxValue()) * ENVELOPE_VIEW_SCALE_ADJUST + ((1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0));
                        Point polyPoint = new Point(x, y);
                        envPolyLine.Points.Add(polyPoint);
                    }
                }
            }
            else
            {
                if (!audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeCurves)
                {
                    foreach (EnvelopeBox evb in envelopeBoxes)
                    {
                        Point point = new Point(ConvertTimeStampInSecondsToPixels(GetEnvelopeTimeStamp(evb)), ValueToScreen(GetEnvelopeValue(evb)));
                        point = UpdateEnvelopeBoxCenterPosition(evb, point, false);
                        envPolyLine.Points.Add(point);
                    }
                }
                else
                {
                    // audioBlock.UpdateSplineVol(audioBlockIndex);
                    SplineCache spline = audioBlock.GetSplineVol(audioBlockIndex);
                    double slidingOffsetWindowInSeconds = envelopesCanvas.HostCanvas.SlidingWindowOffsetSeconds;
                    double realWidth = GetPixelsPerSecond() * DrawLengthInSeconds;

                    for (int i = 0; i < spline.YValues.Length; i++)
                    {
                        double x = realWidth * (((double)i * spline.StepSizeInSeconds - slidingOffsetWindowInSeconds) / DrawLengthInSeconds);
                        double y = this.envelopesCanvas.HostCanvas.Height * ((1.0 - (spline.YValues[i] / MaxBoxValue())) * ENVELOPE_VIEW_SCALE_ADJUST + ((1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0));
                        Point polyPoint = new Point(x, y);
                        envPolyLine.Points.Add(polyPoint);
                    }
                }
            }
        }

        void ReleaseEnvelopeBoxes()
        {
            foreach(var e in envelopeBoxes)
            {
                e.MouseLeftButtonDown -= Evb_MouseLeftButtonDown;
            }
            envelopeBoxes.Clear();
        }

        public override void Draw()
        {
            this.Children.Clear();
            ReleaseEnvelopeBoxes();
            this.Children.Add(envPolyLine);

            if (viewMode == ViewOrientationMode.Vertical)
            {
                foreach (VolumeEnvelopePoint vep in audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].VolumeEnvelopePoints)
                {
                    EnvelopeBox env = new EnvelopeBox(this, 10, 10, fillBrush, strokeBrush);
                    envelopeBoxes.Add(env);
                    env.MouseLeftButtonDown += Evb_MouseLeftButtonDown;
                    if (this.envelopesCanvas.HostCanvas.ContextMenu != null)
                        env.ContextMenu.Resources.MergedDictionaries.Add(this.envelopesCanvas.HostCanvas.ContextMenu.Resources);
                    env.Freezed = vep.Freezed;

                    Point point = new Point(ValueToScreen(vep.Value), ConvertTimeStampInSecondsToPixels(vep.TimeStamp));
                    point = UpdateEnvelopeBoxCenterPosition(env, point, false);

                    if (point.Y < env.Height / 2 && point.Y >= 0)
                        point.Y = env.Height / 2;
                    else if (point.Y > this.Height - env.Height / 2.0 && point.Y < this.Height)
                        point.Y = this.Height - env.Height / 2.0;



                    Canvas.SetLeft(env, point.X - env.Width / 2);
                    Canvas.SetTop(env, point.Y - env.Height / 2);

                    env.ToolTip = string.Format("Time: {0:0.0}, Volume: {1:0.0} bB", vep.TimeStamp, Decibel.FromAmplitude(vep.Value));

                    this.Children.Add(env);
                }
            }
            else
            {
                foreach (VolumeEnvelopePoint vep in audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].VolumeEnvelopePoints)
                {
                    EnvelopeBox env = new EnvelopeBox(this, 10, 10, fillBrush, strokeBrush);
                    envelopeBoxes.Add(env);
                    env.MouseLeftButtonDown += Evb_MouseLeftButtonDown;
                    if (this.envelopesCanvas.HostCanvas.ContextMenu != null)
                        env.ContextMenu.Resources.MergedDictionaries.Add(this.envelopesCanvas.HostCanvas.ContextMenu.Resources);
                    env.Freezed = vep.Freezed;

                    Point point = new Point(ConvertTimeStampInSecondsToPixels(vep.TimeStamp), ValueToScreen(vep.Value));
                    point = UpdateEnvelopeBoxCenterPosition(env, point, false);

                    if (point.X < env.Width / 2 && point.X >= 0)
                        point.X = env.Width / 2;
                    else if (point.X > this.Width - env.Width / 2 && point.X < this.Width)
                        point.X = this.Width - env.Width / 2;

                    Canvas.SetLeft(env, point.X - env.Width / 2);
                    Canvas.SetTop(env, point.Y - env.Height / 2);

                    env.ToolTip = string.Format("Time: {0:0.0}, Volume: {1:0.0} bB", vep.TimeStamp, Decibel.FromAmplitude(vep.Value));

                    this.Children.Add(env);
                }
            }

            UpdatePolyLinePath();

            EnvelopeVisible = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeVisible;
        }

        public void UpdateToolTip(EnvelopeBox env)
        {
            env.ToolTip = string.Format("Time: {0:0.00} Seconds, Volume: {1:0.0} bB", GetEnvelopeTimeStamp(env), Decibel.FromAmplitude(GetEnvelopeValue(env)));
        }

        public void DeleteEnvelopeBox(EnvelopeBox envelopeBox)
        {
            // Can't delete first or last
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);
            if ((envelopeBoxIndex > 0) && (envelopeBoxIndex < envelopeBoxes.Count - 1))
            {
                envelopeBox.Index = envelopeBoxIndex;
                envelopeBox.AudioBlockIndex = audioBlockIndex;

                audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints.RemoveAt(envelopeBoxIndex);
                if (audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeCurves)
                    audioBlock.UpdateSplineVol(audioBlockIndex);

                audioBlock.RaisePropertyChangedVolumeEnvBoxRemoved(envelopeBox);
            }
        }

        internal override double GetEnvelopeValue(EnvelopeBox envelopeBox)
        {
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);
            return GetEnvelopeValue(envelopeBoxIndex);
        }

        internal override double GetEnvelopeValue(int envelopeBoxIndex)
        {
            return audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints[envelopeBoxIndex].Value;
        }

        internal override double GetEnvelopeTimeStamp(EnvelopeBox envelopeBox)
        {
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);
            return GetEnvelopeTimeStamp(envelopeBoxIndex);
        }

        internal override double GetEnvelopeTimeStamp(int envelopeBoxIndex)
        {
            return audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints[envelopeBoxIndex].TimeStamp;
        }

        internal override void SetEnvelopeValue(EnvelopeBox envelopeBox, double value)
        {
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);
            SetEnvelopeValue(envelopeBoxIndex, value);
        }

        internal override void SetEnvelopeValue(int envelopeBoxIndex, double value)
        {
            audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints[envelopeBoxIndex].Value = value;
        }

        internal override void SetEnvelopeTimeStamp(EnvelopeBox envelopeBox, double value)
        {
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);
            SetEnvelopeTimeStamp(envelopeBoxIndex, value);
        }

        internal override void SetEnvelopeTimeStamp(int envelopeBoxIndex, double value)
        {
            audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints[envelopeBoxIndex].TimeStamp = value;
        }

        public new void ResetEnvelopeBox(EnvelopeBox envelopeBox)
        {
            base.ResetEnvelopeBox(envelopeBox);

            if (audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeCurves)
                audioBlock.UpdateSplineVol(audioBlockIndex);

            audioBlock.RaisePropertyChangedVolumeEnv(envelopeBox);
            //UpdatePolyLinePath();
        }

        private void EnvelopesCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.Width = ((Canvas)sender).Width;
            this.Height = ((Canvas)sender).Height;
            Draw();
        }

        internal override double DefaulBoxValue()
        {
            if (VolumeEnvelopeMode == VolumeEnvelopeModes.dB0)
            {
                return ENVELOPE_MODE_0DB_MAX;
            }
            else
            {
                return ENVELOPE_MODE_0DB_MAX;
            }
        }

        internal override double MaxBoxValue()
        {
            if (VolumeEnvelopeMode == VolumeEnvelopeModes.dB0)
            {
                return ENVELOPE_MODE_0DB_MAX;
            }
            else
            {
                return ENVELOPE_MODE_6DB_MAX;
            }
        }

        //private void HostCanvas_MouseLeave(object sender, MouseEventArgs e)
        //{
        //this.boxDragged = false;
        //}

        private void HostCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            newBoxPositionFromMenu = e.GetPosition(envelopesCanvas.HostCanvas);
        }

        private void Evb_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            envelopesCanvas.SelectVolEnvelope();
            if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && !((EnvelopeBox)sender).Freezed)
            {
                //List<EEnvelopeType> visibleEnvelopes = envelopesCanvas.VisbleEnvelopes();
                //if (visibleEnvelopes.Count == 1 && visibleEnvelopes[0] == EEnvelopeType.Volume)
                {
                    DeleteEnvelopeBox((EnvelopeBox)sender);
                    e.Handled = true;
                }
            }
            else if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                e.Handled = true; // Don't allow boxes to be create on top of each other.
            }
            else
            {
                Mouse.Capture(envelopesCanvas.HostCanvas);
                this.boxDragged = true;
                boxDraggedIndex = envelopeBoxes.IndexOf((EnvelopeBox)sender);
                ((EnvelopeBox)sender).Index = boxDraggedIndex;
                ((EnvelopeBox)sender).AudioBlockIndex = audioBlockIndex;
                e.Handled = true;
            }
        }

        private void C_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            newBoxPosition = e.GetPosition(envelopesCanvas.HostCanvas);
            if (envelopesCanvas.IsVisible && boxDragged)
            {
                EnvelopeBox evb = envelopeBoxes[boxDraggedIndex];

                if (!evb.Freezed && (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed))
                {
                    Point point = e.GetPosition(envelopesCanvas.HostCanvas);

                    if (viewMode == ViewOrientationMode.Vertical)
                    {
                        point.Y = SnapTo(point.Y);
                    }
                    else
                    {
                        point.X = SnapTo(point.X);
                    }

                    point = UpdateEnvelopeBoxCenterPosition(evb, point, false);

                    UpdateEnvPoint(evb, point);
                    audioBlock.UpdateSplineVol(audioBlockIndex);
                    audioBlock.NotifyBuzzDataChanged();
                    audioBlock.RaisePropertyChangedVolumeEnv(evb);
                    //UpdatePolyLinePath(); // Better responsiveness

                    e.Handled = true;
                }
            }
        }

        bool envelopeVisible = false;
        public bool EnvelopeVisible
        {
            get
            {
                return envelopeVisible;
            }
            set
            {
                envelopeVisible = value;
                if (envelopeVisible)
                {
                    this.Visibility = Visibility.Visible;
                    this.MouseRightButtonDown += HostCanvas_MouseRightButtonDown;
                }
                else
                {
                    this.Visibility = Visibility.Hidden;
                    this.MouseRightButtonDown -= HostCanvas_MouseRightButtonDown;
                }
            }
        }

        MenuItem miVolume;
        MenuItem miCurves;
        MenuItem menuItemVolumeLayerVisible;
        MenuItem miVolume0dB;
        MenuItem miVolume6dB;
        MenuItem miEnabled;
        private Point newBoxPosition;


        public int AudioBlockIndex { get => audioBlockIndex; set => audioBlockIndex = value; }

        internal VolumeEnvelopeModes VolumeEnvelopeMode { get => volumeEnvelopeMode; set => volumeEnvelopeMode = value; }

        public MenuItem CreateEnvelopeMenu()
        {
            miVolume = new MenuItem();
            miVolume.Header = "Volume";

            object dummySub = new object();

            miVolume.Items.Add(dummySub);
            miVolume.SubmenuOpened += delegate
            {
                if (miVolume.Items[0].GetType() == typeof(object))
                {
                    miVolume.Items.Clear();

                    miEnabled = new MenuItem();
                    miEnabled.Header = "Enabled";
                    miEnabled.IsCheckable = true;
                    miEnabled.IsChecked = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeEnabled;
                    miEnabled.Checked += MiEnabled_Checked;
                    miEnabled.Unchecked += MiEnabled_Unchecked;
                    miVolume.Items.Add(miEnabled);
                    menuItemVolumeLayerVisible = new MenuItem();
                    menuItemVolumeLayerVisible.Header = "Visible";
                    menuItemVolumeLayerVisible.IsCheckable = true;
                    menuItemVolumeLayerVisible.IsChecked = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeVisible;
                    menuItemVolumeLayerVisible.Checked += MenuItemVolumeLayerVisible_Checked;
                    menuItemVolumeLayerVisible.Unchecked += MenuItemVolumeLayerVisible_Unchecked;

                    miVolume.Items.Add(menuItemVolumeLayerVisible);
                    miVolume.Items.Add(new Separator());
                    MenuItem mi = new MenuItem();
                    mi.Header = "Add Point";
                    mi.Click += Mi_Click_Add_Point;
                    miVolume.Items.Add(mi);
                    mi = new MenuItem();
                    mi.Header = "Freeze All";
                    mi.Click += Mi_Click_Freeze_All;
                    miVolume.Items.Add(mi);
                    mi = new MenuItem();
                    mi.Header = "Unfreeze All";
                    mi.Click += Mi_Click_Unfreeze_All;
                    miVolume.Items.Add(mi);
                    mi = new MenuItem();
                    mi.Header = "Reset";
                    mi.Click += Mi_Click_Reset;
                    miVolume.Items.Add(mi);
                    MenuItem miMode = new MenuItem();
                    miMode.Header = "Mode";
                    miVolume.Items.Add(miMode);
                    miVolume0dB = new MenuItem();
                    miVolume0dB.IsCheckable = true;
                    miVolume0dB.IsChecked = VolumeEnvelopeMode == VolumeEnvelopeModes.dB0;
                    miVolume0dB.Checked += Mi_Checked_0dB;
                    miVolume0dB.Unchecked += MiVolume0dB_Unchecked;
                    miVolume0dB.Header = string.Format("Max {0:0.0} bB", Decibel.FromAmplitude(1.0));
                    miMode.Items.Add(miVolume0dB);
                    miVolume6dB = new MenuItem();
                    miVolume6dB.IsCheckable = true;
                    miVolume6dB.IsChecked = VolumeEnvelopeMode == VolumeEnvelopeModes.dB6;
                    miVolume6dB.Checked += Mi_Checked_6dB;
                    miVolume6dB.Unchecked += MiVolume6dB_Unchecked;
                    miVolume6dB.Header = string.Format("Max {0:0.0} bB", Decibel.FromAmplitude(2.0));
                    miMode.Items.Add(miVolume6dB);
                    miCurves = new MenuItem();
                    miCurves.Header = "Curves";
                    miCurves.IsCheckable = true;
                    miCurves.IsChecked = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeCurves;
                    miCurves.Checked += Mi_Checked_Curves;
                    miCurves.Unchecked += Mi_Unchecked_Curves;
                    miVolume.Items.Add(miCurves);

                    foreach (Control item in miVolume.Items)
                        if (item != menuItemVolumeLayerVisible && item != miEnabled)
                            item.IsEnabled = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeVisible;
                }
            };

            return miVolume;
        }

        public void RemoveMenuCheckedEvents()
        {
            if (miEnabled != null)
            {
                miEnabled.Checked -= MiEnabled_Checked;
                miEnabled.Unchecked -= MiEnabled_Unchecked;
            }
            if (menuItemVolumeLayerVisible != null)
            {
                menuItemVolumeLayerVisible.Checked -= MenuItemVolumeLayerVisible_Checked;
                menuItemVolumeLayerVisible.Unchecked -= MenuItemVolumeLayerVisible_Unchecked;
            }
            if (miVolume0dB != null)
            {
                miVolume0dB.Checked -= Mi_Checked_0dB;
                miVolume0dB.Unchecked -= MiVolume0dB_Unchecked;
            }
            if (miVolume6dB != null)
            {
                miVolume6dB.Checked -= Mi_Checked_6dB;
                miVolume6dB.Unchecked -= MiVolume6dB_Unchecked;
            }
            if (miCurves != null)
            {
                miCurves.Checked -= Mi_Checked_Curves;
                miCurves.Unchecked -= Mi_Unchecked_Curves;
            }
        }

        private void Mi_Unchecked_Curves(object sender, RoutedEventArgs e)
        {
            audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeCurves = false;
            // ToDo: Clean cache 
            UpdatePolyLinePath();
            audioBlock.RaisePropertyReDrawVolume(this);
            audioBlock.NotifyBuzzDataChanged();
        }

        private void Mi_Checked_Curves(object sender, RoutedEventArgs e)
        {
            audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeCurves = true;
            audioBlock.UpdateSplineVol(audioBlockIndex);
            UpdatePolyLinePath();
            audioBlock.RaisePropertyReDrawVolume(this);
            audioBlock.NotifyBuzzDataChanged();
        }

        private void DisableMiEvents()
        {
            miVolume6dB.Checked -= Mi_Checked_6dB;
            miVolume6dB.Unchecked -= MiVolume6dB_Unchecked;
            miVolume0dB.Checked -= Mi_Checked_0dB;
            miVolume0dB.Unchecked -= MiVolume0dB_Unchecked;
        }

        private void EnableMiEvents()
        {
            miVolume6dB.Checked += Mi_Checked_6dB;
            miVolume6dB.Unchecked += MiVolume6dB_Unchecked;
            miVolume0dB.Checked += Mi_Checked_0dB;
            miVolume0dB.Unchecked += MiVolume0dB_Unchecked;
        }

        private void MiVolume6dB_Unchecked(object sender, RoutedEventArgs e)
        {
            // User can't unclick checkbox.
            DisableMiEvents();
            miVolume6dB.IsChecked = true;
            EnableMiEvents();
        }

        private void MiVolume0dB_Unchecked(object sender, RoutedEventArgs e)
        {
            DisableMiEvents();
            miVolume0dB.IsChecked = true;
            EnableMiEvents();
        }

        private void Mi_Click_Reset(object sender, RoutedEventArgs e)
        {
            audioBlock.ResetVolEnvPoints(AudioBlockIndex);
            this.LoadData();
            if (audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeCurves)
                audioBlock.UpdateSplineVol(audioBlockIndex);
            audioBlock.RaisePropertyReDrawVolume(this);
            audioBlock.NotifyBuzzDataChanged();
        }

        private void MiEnabled_Unchecked(object sender, RoutedEventArgs e)
        {
            audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].VolumeEnvelopeEnabled = false;
            audioBlock.NotifyBuzzDataChanged();
        }

        private void MiEnabled_Checked(object sender, RoutedEventArgs e)
        {
            audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].VolumeEnvelopeEnabled = true;
            audioBlock.NotifyBuzzDataChanged();
        }

        private void MenuItemVolumeLayerVisible_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (Control item in miVolume.Items)
                if (item != menuItemVolumeLayerVisible && item != miEnabled)
                    item.IsEnabled = false;

            EnvelopeVisible = false;
            audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeVisible = false;
            audioBlock.RaisePropertyChangedVolumeVisibility(this);
        }

        private void MenuItemVolumeLayerVisible_Checked(object sender, RoutedEventArgs e)
        {
            foreach (Control item in miVolume.Items)
                if (item != menuItemVolumeLayerVisible && item != miEnabled)
                    item.IsEnabled = true;

            EnvelopeVisible = true;
            audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeVisible = true;
            audioBlock.RaisePropertyChangedVolumeVisibility(this);
        }

        private void Mi_Checked_6dB(object sender, RoutedEventArgs e)
        {
            VolumeEnvelopeMode = VolumeEnvelopeModes.dB6;
            audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].VolumeEnvelopeMode = (int)VolumeEnvelopeMode;
            DisableMiEvents();
            miVolume0dB.IsChecked = false;
            EnableMiEvents();

            Draw();

            audioBlock.NotifyBuzzDataChanged();
        }

        private void Mi_Checked_0dB(object sender, RoutedEventArgs e)
        {
            VolumeEnvelopeMode = VolumeEnvelopeModes.dB0;
            audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].VolumeEnvelopeMode = (int)VolumeEnvelopeMode;
            DisableMiEvents();
            miVolume6dB.IsChecked = false;
            EnableMiEvents();

            // Cut all down to 0dB
            foreach (VolumeEnvelopePoint vep in audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].VolumeEnvelopePoints)
                if (vep.Value > ENVELOPE_MODE_0DB_MAX)
                    vep.Value = ENVELOPE_MODE_0DB_MAX;

            Draw();

            audioBlock.NotifyBuzzDataChanged();
        }

        private void Mi_Click_Unfreeze_All(object sender, RoutedEventArgs e)
        {
            List<VolumeEnvelopePoint> veps = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints;
            foreach (VolumeEnvelopePoint ep in veps)
                ep.Freezed = false;

            audioBlock.RaisePropertyChangedVolumeEnvBoxesFreezedState(this);

        }

        private void Mi_Click_Freeze_All(object sender, RoutedEventArgs e)
        {
            List<VolumeEnvelopePoint> veps = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints;
            foreach (VolumeEnvelopePoint ep in veps)
                ep.Freezed = true;

            audioBlock.RaisePropertyChangedVolumeEnvBoxesFreezedState(this);
        }

        private void Mi_Click_Add_Point(object sender, RoutedEventArgs e)
        {
            Add_Point(newBoxPositionFromMenu);
        }

        private void Add_Point(Point pos)
        {
            EnvelopeBox evb = new EnvelopeBox(this, 10, 10, Brushes.LightSkyBlue, Brushes.Black);
            evb.Freezed = false;

            if (viewMode == ViewOrientationMode.Vertical)
            {
                pos.Y = SnapTo(pos.Y);
            }
            else
            {
                pos.X = SnapTo(pos.X);
            }

            pos = UpdateEnvelopeBoxCenterPosition(evb, pos, true);
            Canvas.SetLeft(evb, pos.X - evb.Width / 2);
            Canvas.SetTop(evb, pos.Y - evb.Height / 2);

            // Update time stamp
            // ToDo: save this
            double timeStamp = ConvertPixelToTimeStampInSeconds(pos);
            double value;

            if (viewMode == ViewOrientationMode.Vertical)
            {
                value = pos.X;
            }
            else
            {
                value = pos.Y;
            }


            // Update Visual
            evb.TimeStamp = timeStamp;
            int index = GetIndexForNewEnvelope(evb, timeStamp);
            evb.Index = index;
            // Update Machine Data
            SaveEnvelopeBoxData(evb, index, ScreenToValue(value), timeStamp, false);

            evb.AudioBlockIndex = audioBlockIndex;

            audioBlock.RaisePropertyChangedVolumeEnvBoxAdded(evb);
            audioBlock.NotifyBuzzDataChanged();
        }

        private void AddEnvelopeBoxToCanvas(EnvelopeBox evb, int index)
        {
            Point pos = new Point();
            var abEnvelopePoints = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints;
            if (viewMode == ViewOrientationMode.Vertical)
            {
                pos.Y = ConvertTimeStampInSecondsToPixels(abEnvelopePoints[index].TimeStamp) - evb.Height / 2.0;
                pos.X = ValueToScreen(abEnvelopePoints[index].Value) - evb.Width / 2.0;

                if (pos.Y < evb.Height)
                    pos.Y = evb.Height;
                else if (pos.Y > this.Height - evb.Height)
                    pos.Y = this.Height - evb.Height;
            }
            else
            {
                pos.X = ConvertTimeStampInSecondsToPixels(abEnvelopePoints[index].TimeStamp) - evb.Width / 2.0;
                pos.Y = ValueToScreen(abEnvelopePoints[index].Value) - evb.Height / 2.0;

                if (pos.Y < evb.Width)
                    pos.Y = evb.Width;
                else if (pos.Y > this.Width - evb.Width)
                    pos.Y = this.Width - evb.Width;
            }

            Canvas.SetLeft(evb, pos.X);
            Canvas.SetTop(evb, pos.Y);

            pos.X = Canvas.GetLeft(evb) + evb.Width / 2.0;
            pos.Y = Canvas.GetTop(evb) + evb.Height / 2.0;

            this.Children.Add(evb);
            envPolyLine.Points.Insert(index, pos);
        }

        private void SaveEnvelopeBoxData(EnvelopeBox evb, int index, double value, double timeStamp, bool freezed)
        {
            AudioBlock.VolumeEnvelopePoint vep = new AudioBlock.VolumeEnvelopePoint();
            vep.Value = value;
            vep.TimeStamp = timeStamp;
            vep.Freezed = freezed;
            audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints.Insert(index, vep);

            // Update spline & draw
            audioBlock.UpdateSplineVol(audioBlockIndex);
        }

        internal override Point MakeSureNotOverlapping(Point pos)
        {
            Point ret = pos;

            double maxTimeStamp = audioBlock.GetSampleLengthInSecondsWithOffset(audioBlockIndex);
            double posTimeStamp = ConvertPixelToTimeStampInSeconds(pos);

            List<VolumeEnvelopePoint> volumeEnvelopePoints = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints;

            if (viewMode == ViewOrientationMode.Vertical)
            {
                if (posTimeStamp > maxTimeStamp)
                {
                    posTimeStamp = maxTimeStamp;
                    ret.Y = ConvertTimeStampInSecondsToPixels(posTimeStamp);
                }

                for (int i = 0; i < volumeEnvelopePoints.Count; i++)
                {
                    double envTimeStamp = volumeEnvelopePoints[i].TimeStamp;
                    if (posTimeStamp == envTimeStamp)
                    {
                        if (i < envelopeBoxes.Count - 1)
                        {
                            double newTimeStamp = posTimeStamp + (volumeEnvelopePoints[i + 1].TimeStamp - posTimeStamp) / 10.0;
                            ret.Y = ConvertTimeStampInSecondsToPixels(newTimeStamp);
                        }
                        else
                        {
                            double newTimeStamp = posTimeStamp - (posTimeStamp - volumeEnvelopePoints[i - 1].TimeStamp) / 10.0;
                            ret.Y = ConvertTimeStampInSecondsToPixels(newTimeStamp);
                        }
                        break;
                    }
                }
            }
            else
            {
                if (posTimeStamp > maxTimeStamp)
                {
                    posTimeStamp = maxTimeStamp;
                    ret.X = ConvertTimeStampInSecondsToPixels(posTimeStamp);
                }

                for (int i = 0; i < envelopeBoxes.Count; i++)
                {
                    double envTimeStamp = volumeEnvelopePoints[i].TimeStamp;
                    if (posTimeStamp == envTimeStamp)
                    {
                        if (i < envelopeBoxes.Count - 1)
                        {
                            double newTimeStamp = posTimeStamp + (volumeEnvelopePoints[i + 1].TimeStamp - posTimeStamp) / 10.0;
                            ret.X = ConvertTimeStampInSecondsToPixels(newTimeStamp);
                        }
                        else
                        {
                            double newTimeStamp = posTimeStamp - (posTimeStamp - volumeEnvelopePoints[i - 1].TimeStamp) / 10.0;
                            ret.X = ConvertTimeStampInSecondsToPixels(newTimeStamp);
                        }
                        break;
                    }
                }
            }
            return ret;
        }

        public void SetFreezed(EnvelopeBox envelopeBox, bool v)
        {
            int index = envelopeBoxes.IndexOf(envelopeBox);
            if (index >= 0)
            {
                audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints[index].Freezed = v;

                envelopeBox.AudioBlockIndex = audioBlockIndex;
                envelopeBox.Index = index;
                audioBlock.RaisePropertyChangedVolumeEnvBoxFreezedState(envelopeBox);
            }
        }

        public void EnvelopeBoxMouseLeave()
        {
            envPolyLine.StrokeThickness = 2;
            envPolyLine.Stroke = lineBrush;
        }

        public void EnvelopeBoxMouseEnter()
        {
            envPolyLine.StrokeThickness = 4;
            envPolyLine.Stroke = lineBrushSelected;
        }
    }
}
