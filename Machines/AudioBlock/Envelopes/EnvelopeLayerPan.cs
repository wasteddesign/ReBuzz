using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static WDE.AudioBlock.AudioBlock;

namespace WDE.AudioBlock
{

    /// <summary>
    /// Pan and Volume envelopes are pretty much copies with small differences.
    /// </summary>
    class EnvelopeLayerPan : EnvelopeBase, IEnvelopeLayer
    {
        private Point newBoxPosition;
        private double ENVELOPE_MAX_PAN = 2.0;

        public EnvelopeLayerPan() : base()
        {
            strokeBrush = Brushes.Black;
            fillBrush = Brushes.Orange;
            lineBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x0, 0x0, 0x0));
        }

        private void AudioBlock_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // There is a bug somewhere, don't know the root cause. This will avoid issues...
            // Update: This is fixed but leaving here for now.
            if (envelopeBoxes.Count == 0)
            {
                return;
            }

            if (e.PropertyName == "PanEnvelopeChanged")
            {
                EnvelopeBox evbSender = (EnvelopeBox)sender;

                if (audioBlockIndex != evbSender.AudioBlockIndex)
                    return;

                UpdateEnvelopBox(evbSender.Index, evbSender.AudioBlockIndex);
                UpdatePolyLinePath();
            }
            else if (e.PropertyName == "PanEnvelopeVisibility")
            {
                EnvelopeBase evbSender = (EnvelopeBase)sender;

                if (audioBlockIndex != evbSender.audioBlockIndex)
                    return;
                envelopesCanvas.UpdateMenus();
                EnvelopeVisible = audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].PanEnvelopeVisible;

                if (EnvelopeVisible)
                    Draw();
            }
            else if (e.PropertyName == "PanEnvelopeBoxAdded")
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
            else if (e.PropertyName == "PanEnvelopeBoxRemoved")
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
            else if (e.PropertyName == "PanEnvelopeReDraw")
            {
                EnvelopeBase evbSender = (EnvelopeBase)sender;

                if (audioBlockIndex != evbSender.audioBlockIndex)
                    return;
                Draw();
                envelopesCanvas.UpdateMenus();
            }
            else if (e.PropertyName == "PanEnvBoxFreezedState")
            {
                EnvelopeBox evbSender = (EnvelopeBox)sender;

                if (audioBlockIndex != evbSender.AudioBlockIndex)
                    return;

                EnvelopeBox eb = envelopeBoxes[evbSender.Index];
                eb.Freezed = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints[evbSender.Index].Freezed;

            }
            else if (e.PropertyName == "PanEnvBoxesFreezedState")
            {
                EnvelopeBase evbSender = (EnvelopeBase)sender;

                if (audioBlockIndex != evbSender.audioBlockIndex)
                    return;

                List<PanEnvelopePoint> pal = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints;
                for (int i = 0; i < pal.Count; i++)
                    envelopeBoxes[i].Freezed = pal[i].Freezed;
            }
        }

        public void UpdateEnvelopBox(int envIndex, int abIndex)
        {
            if (audioBlockIndex != abIndex)
                return;

            var envelopePoints = audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].PanEnvelopePoints;
            PanEnvelopePoint pep = envelopePoints[envIndex];
            EnvelopeBox evb = envelopeBoxes[envIndex];

            if (viewMode == ViewOrientationMode.Vertical)
            {

                Point point = new Point(ValueToScreen(pep.Value), ConvertTimeStampInSecondsToPixels(pep.TimeStamp));
                point = UpdateEnvelopeBoxCenterPosition(evb, point, false);

                if (point.Y < evb.Height / 2.0 && point.Y >= 0)
                    point.Y = evb.Height / 2.0;
                else if (point.Y > this.Height - evb.Height / 2.0 && point.Y < this.Height)
                    point.Y = this.Height - evb.Height / 2.0;

                Canvas.SetLeft(evb, point.X - evb.Width / 2.0);
                Canvas.SetTop(evb, point.Y - evb.Height / 2.0);

                evb.ToolTip = string.Format("Time: {0:0.0}, Pan: {1:0.0}", pep.TimeStamp, pep.Value - 1);
            }
            else
            {
                Point point = new Point(ConvertTimeStampInSecondsToPixels(pep.TimeStamp), ValueToScreen(pep.Value));
                point = UpdateEnvelopeBoxCenterPosition(evb, point, false);

                if (point.X < evb.Width / 2.0 && point.X >= 0)
                    point.X = evb.Width / 2.0;
                else if (point.X > this.Width - evb.Width / 2.0 && point.X < this.Width)
                    point.X = this.Width - evb.Width / 2.0;

                Canvas.SetLeft(evb, point.X - evb.Width / 2.0);
                Canvas.SetTop(evb, point.Y - evb.Height / 2.0);

                evb.ToolTip = string.Format("Time: {0:0.0}, Pan: {1:0.0}", pep.TimeStamp, pep.Value - 1);
            }
        }

        internal void LoadData()
        {
            if (audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].PanEnvelopePoints.Count == 0)
            {
                PanEnvelopePoint vep = new PanEnvelopePoint();
                vep.Value = this.DefaulBoxValue();
                vep.TimeStamp = 0.0; // First time stamp starts always from 0 seconds.
                audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].PanEnvelopePoints.Add(vep);

                vep = new PanEnvelopePoint();
                vep.Value = this.DefaulBoxValue();
                vep.TimeStamp = audioBlock.GetSampleLengthInSecondsWithOffset(audioBlockIndex); // Last time stamp is the lenght of sample.
                audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].PanEnvelopePoints.Add(vep);
            }
        }

        void ReleaseEnvelopeBoxes()
        {
            foreach (var e in envelopeBoxes)
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
                foreach (PanEnvelopePoint pep in audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].PanEnvelopePoints)
                {
                    EnvelopeBox env = new EnvelopeBox(this, 10, 10, fillBrush, strokeBrush);
                    envelopeBoxes.Add(env);
                    env.MouseLeftButtonDown += Evb_MouseLeftButtonDown;
                    if(this.envelopesCanvas.HostCanvas.ContextMenu != null)
                        env.ContextMenu.Resources.MergedDictionaries.Add(this.envelopesCanvas.HostCanvas.ContextMenu.Resources);
                    env.Freezed = pep.Freezed;

                    Point point = new Point(ValueToScreen(pep.Value), ConvertTimeStampInSecondsToPixels(pep.TimeStamp));
                    point = UpdateEnvelopeBoxCenterPosition(env, point, false);

                    if (point.Y < env.Height / 2.0 && point.Y >= 0)
                        point.Y = env.Height / 2.0;
                    else if (point.Y > this.Height - env.Height / 2 && point.Y < this.Height)
                        point.Y = this.Height - env.Height / 2.0;

                    Canvas.SetLeft(env, point.X - env.Width / 2.0);
                    Canvas.SetTop(env, point.Y - env.Height / 2.0);

                    env.ToolTip = string.Format("Time: {0:0.0}, Pan: {1:0.0}", pep.TimeStamp, pep.Value - 1.0);

                    this.Children.Add(env);
                }
            }
            else
            {
                foreach (PanEnvelopePoint pep in audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].PanEnvelopePoints)
                {
                    EnvelopeBox env = new EnvelopeBox(this, 10, 10, fillBrush, strokeBrush);
                    envelopeBoxes.Add(env);
                    env.MouseLeftButtonDown += Evb_MouseLeftButtonDown;
                    if (this.envelopesCanvas.HostCanvas.ContextMenu != null)
                        env.ContextMenu.Resources.MergedDictionaries.Add(this.envelopesCanvas.HostCanvas.ContextMenu.Resources);
                    env.Freezed = pep.Freezed;

                    Point point = new Point(ConvertTimeStampInSecondsToPixels(pep.TimeStamp), ValueToScreen(pep.Value));
                    point = UpdateEnvelopeBoxCenterPosition(env, point, false);

                    if (point.X < env.Width / 2.0 && point.X >= 0)
                        point.X = env.Width / 2.0;
                    else if (point.X > this.Width - env.Width / 2.0 && point.X < this.Width)
                        point.X = this.Width - env.Width / 2.0;

                    Canvas.SetLeft(env, point.X - env.Width / 2.0);
                    Canvas.SetTop(env, point.Y - env.Height / 2.0);

                    env.ToolTip = string.Format("Time: {0:0.0}, Pan: {1:0.0}", pep.TimeStamp, pep.Value - 1.0);

                    this.Children.Add(env);
                }
            }
            UpdatePolyLinePath();

            EnvelopeVisible = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeVisible;
        }

        public void UpdateToolTip(EnvelopeBox env)
        {
            env.ToolTip = string.Format("Time: {0:0.00} Seconds, Pan: {1:0.0}", GetEnvelopeTimeStamp(env), GetEnvelopeValue(env) - 1.0);
        }

        public override void UpdatePolyLinePath()
        {
            envPolyLine.Points.Clear();
            envPolyLine.Stroke = lineBrush;
            envPolyLine.StrokeThickness = 2;

            if (viewMode == ViewOrientationMode.Vertical)
            {
                if (!audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeCurves)
                {
                    foreach (EnvelopeBox evb in envelopeBoxes)
                    {
                        Point point = new Point(ValueToScreen(GetEnvelopeValue(evb)), ConvertTimeStampInSecondsToPixels(GetEnvelopeTimeStamp(evb)));
                        point = UpdateEnvelopeBoxCenterPosition(evb, point, false);
                        envPolyLine.Points.Add(point);
                    }
                }
                else if (audioBlock.GetSplinePan(audioBlockIndex) != null)
                {
                    SplineCache spline = audioBlock.GetSplinePan(audioBlockIndex);
                    double slidingOffsetWindowInSeconds = envelopesCanvas.HostCanvas.SlidingWindowOffsetSeconds;

                    double realHeight = GetPixelsPerSecond() * DrawLengthInSeconds;

                    for (int i = 0; i < spline.YValues.Length; i++)
                    {
                        double y = realHeight * (((double)i * spline.StepSizeInSeconds - slidingOffsetWindowInSeconds) / DrawLengthInSeconds);
                        double x = this.envelopesCanvas.HostCanvas.ActualWidth * ((spline.YValues[i] / MaxBoxValue()) * ENVELOPE_VIEW_SCALE_ADJUST + ((1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0));
                        Point polyPoint = new Point(x, y);
                        envPolyLine.Points.Add(polyPoint);
                    }
                }
            }
            else
            {
                if (!audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeCurves)
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
                    SplineCache spline = audioBlock.GetSplinePan(audioBlockIndex);
                    double slidingOffsetWindowInSeconds = envelopesCanvas.HostCanvas.SlidingWindowOffsetSeconds;
                    double realWidth = GetPixelsPerSecond() * DrawLengthInSeconds;

                    for (int i = 0; i < spline.YValues.Length; i++)
                    {
                        double x = realWidth * (((double)i * spline.StepSizeInSeconds - slidingOffsetWindowInSeconds) / DrawLengthInSeconds);
                        double y = this.envelopesCanvas.HostCanvas.ActualHeight * ((1.0 - (spline.YValues[i] / MaxBoxValue())) * ENVELOPE_VIEW_SCALE_ADJUST + ((1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0));
                        Point polyPoint = new Point(x, y);
                        envPolyLine.Points.Add(polyPoint);
                    }
                }
            }
        }


        public void DeleteEnvelopeBox(EnvelopeBox envelopeBox)
        {
            // Can't delete first or last
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);
            if ((envelopeBoxIndex > 0) && (envelopeBoxIndex < envelopeBoxes.Count - 1))
            {
                envelopeBox.Index = envelopeBoxIndex;
                envelopeBox.AudioBlockIndex = audioBlockIndex;

                audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints.RemoveAt(envelopeBoxIndex);
                if (audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeCurves)
                    audioBlock.UpdateSplinePan(audioBlockIndex);
                
                audioBlock.RaisePropertyChangedPanEnvBoxRemoved(envelopeBox);
            }

        }

        internal override double GetEnvelopeValue(EnvelopeBox envelopeBox)
        {
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);
            return GetEnvelopeValue(envelopeBoxIndex);
        }

        internal override double GetEnvelopeValue(int envelopeBoxIndex)
        {
            return audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints[envelopeBoxIndex].Value;
        }

        internal override double GetEnvelopeTimeStamp(EnvelopeBox envelopeBox)
        {
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);
            return GetEnvelopeTimeStamp(envelopeBoxIndex);
        }

        internal override double GetEnvelopeTimeStamp(int envelopeBoxIndex)
        {
            return audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints[envelopeBoxIndex].TimeStamp;
        }

        internal override void SetEnvelopeValue(EnvelopeBox envelopeBox, double value)
        {
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);
            SetEnvelopeValue(envelopeBoxIndex, value);
        }

        internal override void SetEnvelopeValue(int envelopeBoxIndex, double value)
        {
            audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints[envelopeBoxIndex].Value = value;
        }

        internal override void SetEnvelopeTimeStamp(EnvelopeBox envelopeBox, double value)
        {
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);
            SetEnvelopeTimeStamp(envelopeBoxIndex, value);
        }

        internal override void SetEnvelopeTimeStamp(int envelopeBoxIndex, double value)
        {
            audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints[envelopeBoxIndex].TimeStamp = value;
        }

        public new void ResetEnvelopeBox(EnvelopeBox envelopeBox)
        {
            base.ResetEnvelopeBox(envelopeBox);

            if (audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeCurves)
                audioBlock.UpdateSplinePan(audioBlockIndex);
            //this.UpdatePolyLinePath();
            audioBlock.RaisePropertyChangedPanEnv(envelopeBox);
        }

        public void Init(AudioBlock ab, int audioBlockIndex, Envelopes c)
        {
            base.Init(ab, c);
            this.audioBlockIndex = audioBlockIndex;
            LoadData();

            this.Loaded += EnvelopeLayerPan_Loaded;
            this.Unloaded += EnvelopeLayerPan_Unloaded;

            // Start always hidden
            EnvelopeVisible = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeVisible;
            
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
                envelopesCanvas.SelectPanEnvelope();

                if (!(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                {
                    e.Handled = true;
                }
            };
        }

        private void EnvelopeLayerPan_Loaded(object sender, RoutedEventArgs e)
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

        private void EnvelopeLayerPan_Unloaded(object sender, RoutedEventArgs e)
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
                if (envelopesCanvas.IsSelectedLayer(EEnvelopeType.Pan))
                {
                    Add_Point(newBoxPosition);
                    e.Handled = true;
                }
            }
        }

        private void EnvelopesCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.Width = ((Canvas)sender).ActualWidth;
            this.Height = ((Canvas)sender).ActualHeight;
            Draw();
        }

        internal override double DefaulBoxValue()
        {
            return ENVELOPE_MAX_PAN / 2.0;
        }

        internal override double MaxBoxValue()
        {
            return ENVELOPE_MAX_PAN;
        }

        private void HostCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            newBoxPositionFromMenu = e.GetPosition(envelopesCanvas.HostCanvas);
        }

        private void Evb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            envelopesCanvas.SelectPanEnvelope();
            if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && !((EnvelopeBox)sender).Freezed)
            {
                //List<EEnvelopeType> visibleEnvelopes = envelopesCanvas.VisbleEnvelopes();
                //if (visibleEnvelopes.Count == 1 && visibleEnvelopes[0] == EEnvelopeType.Pan)
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
                    audioBlock.UpdateSplinePan(audioBlockIndex);
                    audioBlock.NotifyBuzzDataChanged();
                    audioBlock.RaisePropertyChangedPanEnv(evb);
                    // UpdatePolyLinePath(); // Better responsiveness

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
                    envelopesCanvas.MouseRightButtonDown += HostCanvas_MouseRightButtonDown;
                }
                else
                {
                    this.Visibility = Visibility.Hidden;
                    envelopesCanvas.MouseRightButtonDown -= HostCanvas_MouseRightButtonDown;
                }
            }
        }

        MenuItem miPan;
        MenuItem menuItemPanLayerVisible;
        MenuItem miEnabled;
        MenuItem miCurves;

        public int AudioBlockIndex { get => audioBlockIndex; set => audioBlockIndex = value; }

        public MenuItem CreateEnvelopeMenu()
        {
            miPan = new MenuItem();
            miPan.Header = "Pan";

            object dummySub = new object();

            miPan.Items.Add(dummySub);
            miPan.SubmenuOpened += delegate
            {
                if (miPan.Items[0].GetType() == typeof(object))
                {
                    miPan.Items.Clear();

                    miEnabled = new MenuItem();
                    miEnabled.Header = "Enabled";
                    miEnabled.IsCheckable = true;
                    miEnabled.IsChecked = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeEnabled;
                    miEnabled.Checked += MiEnabled_Checked;
                    miEnabled.Unchecked += MiEnabled_Unchecked;
                    miPan.Items.Add(miEnabled);
                    menuItemPanLayerVisible = new MenuItem();
                    menuItemPanLayerVisible.Header = "Visible";
                    menuItemPanLayerVisible.IsCheckable = true;
                    menuItemPanLayerVisible.IsChecked = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeVisible;
                    menuItemPanLayerVisible.Checked += MenuItemPanLayerVisible_Checked;
                    menuItemPanLayerVisible.Unchecked += MenuItemPanLayerVisible_Unchecked;

                    miPan.Items.Add(menuItemPanLayerVisible);
                    miPan.Items.Add(new Separator());
                    MenuItem mi = new MenuItem();
                    mi.Header = "Add Point";
                    mi.Click += Mi_Click_Add_Point;
                    miPan.Items.Add(mi);
                    mi = new MenuItem();
                    mi.Header = "Freeze All";
                    mi.Click += Mi_Click_Freeze_All;
                    miPan.Items.Add(mi);
                    mi = new MenuItem();
                    mi.Header = "Unfreeze All";
                    mi.Click += Mi_Click_Unfreeze_All;
                    miPan.Items.Add(mi);
                    mi = new MenuItem();
                    mi.Header = "Reset";
                    mi.Click += Mi_Click_Reset;
                    miPan.Items.Add(mi);
                    miCurves = new MenuItem();
                    miCurves.Header = "Curves";
                    miCurves.IsCheckable = true;
                    miCurves.IsChecked = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeCurves;
                    miCurves.Checked += Mi_Checked_Curves;
                    miCurves.Unchecked += Mi_Unchecked_Curves;
                    miPan.Items.Add(miCurves);

                    foreach (Control item in miPan.Items)
                        if (item != menuItemPanLayerVisible && item != miEnabled)
                            item.IsEnabled = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeVisible;

                }
            };

            return miPan;
        }

        public void RemoveMenuCheckedEvents()
        {
            if (miEnabled != null)
            {
                miEnabled.Checked -= MiEnabled_Checked;
                miEnabled.Unchecked -= MiEnabled_Unchecked;
            }
            if (menuItemPanLayerVisible != null)
            {
                menuItemPanLayerVisible.Checked -= MenuItemPanLayerVisible_Checked;
                menuItemPanLayerVisible.Unchecked -= MenuItemPanLayerVisible_Unchecked;
            }
            if (miCurves != null)
            {
                miCurves.Checked -= Mi_Checked_Curves;
                miCurves.Unchecked -= Mi_Unchecked_Curves;
            }
        }

        private void Mi_Unchecked_Curves(object sender, RoutedEventArgs e)
        {
            audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeCurves = false;
            // ToDo: Clean cache 
            UpdatePolyLinePath();
            audioBlock.RaisePropertyReDrawPan(this);
            audioBlock.NotifyBuzzDataChanged();
        }

        private void Mi_Checked_Curves(object sender, RoutedEventArgs e)
        {
            audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeCurves = true;
            audioBlock.UpdateSplinePan(audioBlockIndex);
            UpdatePolyLinePath();
            audioBlock.RaisePropertyReDrawPan(this);
            audioBlock.NotifyBuzzDataChanged();
        }

        private void DisableMiEvents()
        {
        }

        private void EnableMiEvents()
        {
        }

        private void Mi_Click_Reset(object sender, RoutedEventArgs e)
        {
            audioBlock.ResetPanEnvPoints(audioBlockIndex);
            this.LoadData();
            if (audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeCurves)
                audioBlock.UpdateSplinePan(audioBlockIndex);

            audioBlock.RaisePropertyReDrawPan(this);
            audioBlock.NotifyBuzzDataChanged();
        }

        private void MiEnabled_Unchecked(object sender, RoutedEventArgs e)
        {
            audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].PanEnvelopeEnabled = false;
            audioBlock.NotifyBuzzDataChanged();
        }

        private void MiEnabled_Checked(object sender, RoutedEventArgs e)
        {
            audioBlock.MachineState.AudioBlockInfoTable[AudioBlockIndex].PanEnvelopeEnabled = true;
            audioBlock.NotifyBuzzDataChanged();
        }

        private void MenuItemPanLayerVisible_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (Control item in miPan.Items)
                if (item != menuItemPanLayerVisible && item != miEnabled)
                    item.IsEnabled = false;

            audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeVisible = false;
            EnvelopeVisible = false;
            audioBlock.RaisePropertyChangedPanVisibility(this);
        }

        private void MenuItemPanLayerVisible_Checked(object sender, RoutedEventArgs e)
        {
            foreach (Control item in miPan.Items)
                if (item != menuItemPanLayerVisible && item != miEnabled)
                    item.IsEnabled = true;

            audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeVisible = true;
            EnvelopeVisible = true;
            audioBlock.RaisePropertyChangedPanVisibility(this);
        }


        private void Mi_Click_Unfreeze_All(object sender, RoutedEventArgs e)
        {
            List<PanEnvelopePoint> epl = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints;
            foreach (PanEnvelopePoint ep in epl)
                ep.Freezed = false;

            audioBlock.RaisePropertyChangedPanEnvBoxesFreezedState(this);
        }

        private void Mi_Click_Freeze_All(object sender, RoutedEventArgs e)
        {
            List<PanEnvelopePoint> epl = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints;
            foreach (PanEnvelopePoint ep in epl)
                ep.Freezed = true;

            audioBlock.RaisePropertyChangedPanEnvBoxesFreezedState(this);
        }

        private void Mi_Click_Add_Point(object sender, RoutedEventArgs e)
        {
            Add_Point(newBoxPositionFromMenu);
        }

        private void Add_Point(Point pos)
        {
            EnvelopeBox evb = new EnvelopeBox(this, 10, 10, Brushes.Orange, Brushes.Black);
            evb.Freezed = false;

            // evb.MouseLeftButtonDown += Evb_MouseLeftButtonDown;

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

            //UpdateToolTip(evb);
            evb.AudioBlockIndex = audioBlockIndex;
            audioBlock.RaisePropertyChangedPanEnvBoxAdded(evb);
            audioBlock.NotifyBuzzDataChanged();
        }

        private void AddEnvelopeBoxToCanvas(EnvelopeBox evb, int index)
        {
            Point pos = new Point();
            if (viewMode == ViewOrientationMode.Vertical)
            {
                pos.Y = ConvertTimeStampInSecondsToPixels(audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints[index].TimeStamp) - evb.Height / 2.0;
                pos.X = ValueToScreen(audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints[index].Value) - evb.Width / 2.0;
            }
            else
            {
                pos.X = ConvertTimeStampInSecondsToPixels(audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints[index].TimeStamp) - evb.Width / 2.0;
                pos.Y = ValueToScreen(audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints[index].Value) - evb.Height / 2.0;

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
            PanEnvelopePoint pep = new AudioBlock.PanEnvelopePoint();
            pep.Value = value;
            pep.TimeStamp = timeStamp;
            pep.Freezed = freezed;
            audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints.Insert(index, pep);

            // Update spline & draw
            audioBlock.UpdateSplinePan(audioBlockIndex);
        }

        internal override Point MakeSureNotOverlapping(Point pos)
        {
            Point ret = pos;

            double maxTimeStamp = audioBlock.GetSampleLengthInSecondsWithOffset(audioBlockIndex);
            double posTimeStamp = ConvertPixelToTimeStampInSeconds(pos);

            List<PanEnvelopePoint> panEnvelopePoints = audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints;

            if (viewMode == ViewOrientationMode.Vertical)
            {
                if (posTimeStamp > maxTimeStamp)
                {
                    posTimeStamp = maxTimeStamp;
                    ret.Y = ConvertTimeStampInSecondsToPixels(posTimeStamp);
                }

                for (int i = 0; i < panEnvelopePoints.Count; i++)
                {
                    double envTimeStamp = panEnvelopePoints[i].TimeStamp;
                    if (posTimeStamp == envTimeStamp)
                    {
                        if (i < envelopeBoxes.Count - 1)
                        {
                            double newTimeStamp = posTimeStamp + (panEnvelopePoints[i + 1].TimeStamp - posTimeStamp) / 10.0;
                            ret.Y = ConvertTimeStampInSecondsToPixels(newTimeStamp);
                        }
                        else
                        {
                            double newTimeStamp = posTimeStamp - (posTimeStamp - panEnvelopePoints[i - 1].TimeStamp) / 10.0;
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
                    double envTimeStamp = panEnvelopePoints[i].TimeStamp;
                    if (posTimeStamp == envTimeStamp)
                    {
                        if (i < envelopeBoxes.Count - 1)
                        {
                            double newTimeStamp = posTimeStamp + (panEnvelopePoints[i + 1].TimeStamp - posTimeStamp) / 10.0;
                            ret.X = ConvertTimeStampInSecondsToPixels(newTimeStamp);
                        }
                        else
                        {
                            double newTimeStamp = posTimeStamp - (posTimeStamp - panEnvelopePoints[i - 1].TimeStamp) / 10.0;
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
                audioBlock.MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints[index].Freezed = v;

                envelopeBox.AudioBlockIndex = audioBlockIndex;
                envelopeBox.Index = index;
                audioBlock.RaisePropertyChangedPanEnvBoxFreezedState(envelopeBox);
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
