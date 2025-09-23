using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ModernSequenceEditor.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using static BuzzGUI.Common.Templates.Sequence;

namespace EnvelopeBlock
{

    /// <summary>
    /// 
    /// </summary>
    class EnvelopeLayer : EnvelopeBase, IEnvelopeLayer
    {
        private Point newBoxPosition;
        private double ENVELOPE_MAX = 1.0;

        public EnvelopeLayer(double width, double height) : base(width, height)
        {
            strokeBrush = Brushes.Black;
            fillBrush = Brushes.Orange;
            lineBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x10, 0x10, 0x10));
            if (strokeBrush.CanFreeze) strokeBrush.Freeze();
            if (fillBrush.CanFreeze) fillBrush.Freeze();
            if (lineBrush.CanFreeze) lineBrush.Freeze();
        }

        private void EnvelopeBlock_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Fixed, but leaving for now.
            if (envelopeBoxes.Count == 0)
            {
                return;
            }

            if (e.PropertyName == "EnvelopeChanged")
            {
                EnvelopeBox evbSender = (EnvelopeBox)sender;

                if (envelopePatternIndex != evbSender.EnvelopePatternIndex ||
                    envelopeParamIndex != evbSender.EnvelopeParamIndex)
                    return;

                UpdateEnvelopBox(evbSender.DraggedEnvIndex);
                UpdatePolyLinePath();
            }
            else if (e.PropertyName == "EnvelopeVisibility")
            {
                EnvelopeBase evbSender = (EnvelopeBase)sender;

                if (envelopePatternIndex != evbSender.envelopePatternIndex ||
                envelopeParamIndex != evbSender.envelopeParamIndex)
                    return;

                EnvelopeVisible = envelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeVisible;

                if (EnvelopeVisible)
                    Draw();
            }
            else if (e.PropertyName == "EnvelopeBoxAdded")
            {
                EnvelopeBox evbSender = ((EnvelopeBox)sender).Clone(this);

                if (envelopePatternIndex != evbSender.EnvelopePatternIndex ||
                    envelopeParamIndex != evbSender.EnvelopeParamIndex)
                    return;

                if(envelopesCanvas.HostCanvas.ContextMenu != null)
                    evbSender.ContextMenu.Resources.MergedDictionaries.Add(this.envelopesCanvas.HostCanvas.ContextMenu.Resources);

                // Update EnvelopeLayer List
                int indexAdded = AddEnvelopeBoxToList(evbSender, evbSender.TimeStamp);

                // Update Visual
                AddEnvelopeBoxToCanvas(evbSender, indexAdded);
                evbSender.MouseLeftButtonDown += Evb_MouseLeftButtonDown;
                evbSender.Freezed = false;
                //UpdateToolTip(evbSender);
                UpdatePolyLinePath();
            }
            else if (e.PropertyName == "EnvelopeBoxRemoved")
            {
                EnvelopeBox evbSender = (EnvelopeBox)sender;

                if (envelopePatternIndex != evbSender.EnvelopePatternIndex ||
                    envelopeParamIndex != evbSender.EnvelopeParamIndex)
                    return;

                int envelopeBoxIndex = ((EnvelopeBox)sender).Index;
                envPolyLine.Points.RemoveAt(envelopeBoxIndex);
                EnvelopeBox ebThis = envelopeBoxes[envelopeBoxIndex];
                ebThis.MouseLeftButtonDown -= Evb_MouseLeftButtonDown;
                envelopeBoxes.Remove(ebThis);
                this.Children.Remove(ebThis);

                UpdatePolyLinePath();
            }
            else if (e.PropertyName == "EnvelopeReDraw")
            {
                EnvelopeBase evbSender = (EnvelopeBase)sender;

                if (envelopePatternIndex != evbSender.envelopePatternIndex ||
                envelopeParamIndex != evbSender.envelopeParamIndex)
                    return;
                Draw();
            }
            else if (e.PropertyName == "Unassign")
            {
                EnvelopeBase evbSender = (EnvelopeBase)sender;

                if (envelopePatternIndex != evbSender.envelopePatternIndex ||
                envelopeParamIndex != evbSender.envelopeParamIndex)
                    return;

                EnvelopeVisible = false;
                envelopeBoxes.Clear();
                envelopesCanvas.UpdateMenus();

                Draw();
            }
        }

        public void UpdateEnvelopBox(int envIndex)
        {
            EnvelopePoint pep = envelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints[envIndex];
            EnvelopeBox evb = envelopeBoxes[envIndex];

            Point point;
            if (layoutMode == SequencerLayout.Vertical)
            {
                point = new Point(ValueToScreen(pep.Value), ConvertTimeStampInSecondsToPixels(pep.TimeStamp));

                point = UpdateEnvelopeBoxCenterPosition(evb, point, false);

                if (point.Y < evb.Height / 2.0 && point.Y >= 0)
                    point.Y = evb.Height / 2.0;
                else if (point.Y > (int)(this.Height - evb.Height / 2.0) && (int)point.Y <= this.Height)
                    point.Y = this.Height - evb.Height / 2.0;
            }
            else
            {
                point = new Point(ConvertTimeStampInSecondsToPixels(pep.TimeStamp), ValueToScreen(pep.Value));

                point = UpdateEnvelopeBoxCenterPosition(evb, point, false);

                if (point.X < evb.Width / 2.0 && point.X >= 0)
                    point.X = evb.Width / 2.0;
                else if (point.X > (int)(this.Width - evb.Width / 2.0) && (int)point.X <= this.Width)
                    point.X = this.Width - evb.Width / 2.0;
            }

            Canvas.SetLeft(evb, point.X - evb.Width / 2.0);
            Canvas.SetTop(evb, point.Y - evb.Height / 2.0);

            string desc;
            int paramValue;
            envelopeBlockMachine.GetParamValues(envelopePatternIndex, envelopeParamIndex, pep.Value, out desc, out paramValue);

            string paramValueStr = EnvelopeBlockMachine.Settings.NumeralSystem == DisplayValueTypes.Dec ? "" + paramValue : "" + paramValue.ToString("X");
            evb.ToolTip = string.Format("Time: {0:0.0}, " + desc + ": {1:0}", pep.TimeStamp, paramValueStr);
        }

        internal void LoadData()
        {
            envelopeBlockMachine.UpdateEnvPoints(envelopePatternIndex, envelopeParamIndex);
            envelopeBlockMachine.UpdateSpline(envelopePatternIndex, envelopeParamIndex);
        }

        public override void Draw()
        {
            this.Children.Clear();
            ReleaseEnvelopeBoxes();
            this.Children.Add(envPolyLine);
            this.ClipToBounds = true;

            int counter = 0;
            var pattern = envelopeBlockMachine.MachineState.Patterns[EnvelopePatternIndex];

            if (pattern == null)
                return;

            var envelopePoints = pattern.Envelopes[envelopeParamIndex].EnvelopePoints;

            foreach (EnvelopePoint pep in envelopePoints)
            {
                EnvelopeBox env = new EnvelopeBox(this, 10, 10, fillBrush, strokeBrush);
                envelopeBoxes.Add(env);
                env.MouseLeftButtonDown += Evb_MouseLeftButtonDown;
                if(envelopesCanvas.HostCanvas.ContextMenu != null)
                    env.ContextMenu.Resources.MergedDictionaries.Add(this.envelopesCanvas.HostCanvas.ContextMenu.Resources);
                env.Freezed = pep.Freezed;

                Point point;
                if (layoutMode == SequencerLayout.Vertical)
                {
                    point = new Point(ValueToScreen(pep.Value), ConvertTimeStampInSecondsToPixels(pep.TimeStamp));

                    point = UpdateEnvelopeBoxCenterPosition(env, point, false);

                    if (point.Y < env.Height / 2.0 && point.Y >= 0)
                        point.Y = env.Height / 2.0;
                    else if (point.Y > (int)(this.Height - env.Height / 2.0) && (int)point.Y <= this.Height)
                        point.Y = this.Height - env.Height / 2.0;
                }
                else
                {
                    point = new Point(ConvertTimeStampInSecondsToPixels(pep.TimeStamp), ValueToScreen(pep.Value));

                    point = UpdateEnvelopeBoxCenterPosition(env, point, false);

                    if (point.X < env.Width / 2.0 && point.X >= 0)
                        point.X = env.Width / 2.0;
                    else if (point.X > (int)(this.Width - env.Width / 2.0) && (int)point.X <= this.Width)
                        point.X = this.Width - env.Width / 2.0;
                }

                Canvas.SetLeft(env, point.X - env.Width / 2.0);
                Canvas.SetTop(env, point.Y - env.Height / 2.0);

                string desc;
                int paramValue;
                envelopeBlockMachine.GetParamValues(envelopePatternIndex, envelopeParamIndex, pep.Value, out desc, out paramValue);

                string paramValueStr = EnvelopeBlockMachine.Settings.NumeralSystem == DisplayValueTypes.Dec ? "" + paramValue : "" + paramValue.ToString("X");
                env.ToolTip = string.Format("Time: {0:0.0}, " + desc + ": {1:0}", pep.TimeStamp, paramValueStr);

                this.Children.Add(env);

                counter++;
            }

            UpdatePolyLinePath();

            EnvelopeVisible = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeVisible;
        }

        public void UpdateToolTip(EnvelopeBox env)
        {
            string desc;
            int paramValue;
            int index = envelopeBoxes.IndexOf(env);

            EnvelopePoint ep = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints[index];
            double realVal = ep.Value;
            double timeStamp = ep.TimeStamp;

            envelopeBlockMachine.GetParamValues(envelopePatternIndex, envelopeParamIndex, realVal, out desc, out paramValue);


            string paramValueStr = EnvelopeBlockMachine.Settings.NumeralSystem == DisplayValueTypes.Dec ? "" + paramValue : "" + paramValue.ToString("X");
            env.ToolTip = string.Format("Time: {0:0.0} | " + desc + ": {1:0}", timeStamp, paramValueStr);
        }

        public override void UpdatePolyLinePath()
        {
            envPolyLine.Points.Clear();
            envPolyLine.Stroke = lineBrush;
            envPolyLine.StrokeThickness = 2;

            if (!envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeCurves)
            {
                foreach (EnvelopeBox evb in envelopeBoxes)
                {
                    Point point;
                    if (layoutMode == SequencerLayout.Vertical)
                    {
                        point = new Point(ValueToScreen(GetEnvelopeValue(evb)), ConvertTimeStampInSecondsToPixels(GetEnvelopeTimeStamp(evb)));
                        point = UpdateEnvelopeBoxCenterPosition(evb, point, false);
                    }
                    else
                    {
                        point = new Point(ConvertTimeStampInSecondsToPixels(GetEnvelopeTimeStamp(evb)), ValueToScreen(GetEnvelopeValue(evb)));
                        point = UpdateEnvelopeBoxCenterPosition(evb, point, false);
                    }
                    envPolyLine.Points.Add(point);
                }
            }
            else
            {
                SplineCache spline = envelopeBlockMachine.GetSpline(envelopePatternIndex, envelopeParamIndex);

                if (layoutMode == SequencerLayout.Vertical)
                {
                    double realHeight = GetPixelsPerSecond() * DrawLengthInSeconds;

                    for (int i = 0; i < spline.YValues.Length; i++)
                    {
                        double y = realHeight * (((double)i * spline.StepSizeInSeconds) / DrawLengthInSeconds);
                        double x = this.Width * ((spline.YValues[i] / MaxBoxValue()) * ENVELOPE_VIEW_SCALE_ADJUST + ((1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0));
                        Point polyPoint = new Point(x, y);
                        envPolyLine.Points.Add(polyPoint);
                    }
                }
                else
                {
                    double realWidth = GetPixelsPerSecond() * DrawLengthInSeconds;

                    for (int i = 0; i < spline.YValues.Length; i++)
                    {
                        double x = realWidth * (((double)i * spline.StepSizeInSeconds) / DrawLengthInSeconds);
                        double y = this.Height - this.Height * (spline.YValues[i] / MaxBoxValue() * ENVELOPE_VIEW_SCALE_ADJUST + ((1.0 - ENVELOPE_VIEW_SCALE_ADJUST) / 2.0));
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
                envelopeBox.EnvelopePatternIndex = envelopePatternIndex;
                envelopeBox.EnvelopeParamIndex = envelopeParamIndex;
                envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints.RemoveAt(envelopeBoxIndex);
                if (envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeCurves)
                    envelopeBlockMachine.UpdateSpline(envelopePatternIndex, envelopeParamIndex);

                envelopeBlockMachine.RaisePropertyChangedEnvBoxRemoved(envelopeBox);
            }

        }

        internal override double GetEnvelopeValue(EnvelopeBox envelopeBox)
        {
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);
            return GetEnvelopeValue(envelopeBoxIndex);
        }

        internal override double GetEnvelopeValue(int envelopeBoxIndex)
        {
            return envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints[envelopeBoxIndex].Value;
        }

        internal override double GetEnvelopeTimeStamp(EnvelopeBox envelopeBox)
        {
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);
            return GetEnvelopeTimeStamp(envelopeBoxIndex);
        }

        internal override double GetEnvelopeTimeStamp(int envelopeBoxIndex)
        {
            return envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints[envelopeBoxIndex].TimeStamp;
        }

        internal override void SetEnvelopeValue(EnvelopeBox envelopeBox, double value)
        {
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);
            SetEnvelopeValue(envelopeBoxIndex, value);
        }

        internal override void SetEnvelopeValue(int envelopeBoxIndex, double value)
        {
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints[envelopeBoxIndex].Value = value;
        }

        internal override void SetEnvelopeTimeStamp(EnvelopeBox envelopeBox, double value)
        {
            int envelopeBoxIndex = envelopeBoxes.IndexOf(envelopeBox);
            SetEnvelopeTimeStamp(envelopeBoxIndex, value);
        }

        internal override void SetEnvelopeTimeStamp(int envelopeBoxIndex, double value)
        {
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints[envelopeBoxIndex].TimeStamp = value;
        }

        public new void ResetEnvelopeBox(EnvelopeBox envelopeBox)
        {
            envelopeBox.DraggedEnvIndex = envelopeBoxes.IndexOf(envelopeBox);
            int track = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].Track;
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints[envelopeBox.DraggedEnvIndex].Value =
                envelopeBlockMachine.GetParamDefaulValueScaled(envelopePatternIndex, envelopeParamIndex, track);
            if (envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeCurves)
                envelopeBlockMachine.UpdateSpline(envelopePatternIndex, envelopeParamIndex);
            envelopeBox.EnvelopeParamIndex = envelopeParamIndex;
            envelopeBox.EnvelopePatternIndex = envelopePatternIndex;
            envelopeBlockMachine.RaisePropertyChangedEnv(envelopeBox);
        }

        public new void Init(EnvelopeBlockMachine ab, Envelopes c, int paramIndex, SequencerLayout layoutMode)
        {
            base.Init(ab, c, paramIndex, layoutMode);

            fillBrush = brushes[envelopeParamIndex % brushes.Length];

            LoadData();

            // Start always hidden
            EnvelopeVisible = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeVisible;

            this.Loaded += EnvelopeLayer_Loaded;
            this.Unloaded += EnvelopeLayer_Unloaded;

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
                envelopesCanvas.SelectEnvelope(this);

                if (!(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                {
                    e.Handled = true;
                }
            };

        }

        private void EnvelopeLayer_Loaded(object sender, RoutedEventArgs e)
        {
            envelopeBlockMachine.PropertyChanged += EnvelopeBlock_PropertyChanged;

            envelopesCanvas.SizeChanged += EnvelopesCanvas_SizeChanged;

            envelopesCanvas.HostCanvas.MouseMove += C_MouseMove;
            envelopesCanvas.HostCanvas.MouseRightButtonDown += HostCanvas_MouseRightButtonDown;
            envelopesCanvas.HostCanvas.MouseLeftButtonDown += HostCanvas_MouseLeftButtonDown;
            envelopesCanvas.HostCanvas.MouseLeftButtonUp += HostCanvas_MouseLeftButtonUp;
        }

        void ReleaseEnvelopeBoxes()
        {
            foreach (EnvelopeBox eb in envelopeBoxes)
            {
                eb.MouseLeftButtonDown -= Evb_MouseLeftButtonDown;
            }
            envelopeBoxes.Clear();
        }

        private void EnvelopeLayer_Unloaded(object sender, RoutedEventArgs e)
        {
            ReleaseEnvelopeBoxes();
            envelopeBlockMachine.PropertyChanged -= EnvelopeBlock_PropertyChanged;

            envelopesCanvas.SizeChanged -= EnvelopesCanvas_SizeChanged;

            envelopesCanvas.HostCanvas.MouseMove -= C_MouseMove;
            envelopesCanvas.HostCanvas.MouseRightButtonDown -= HostCanvas_MouseRightButtonDown;
            envelopesCanvas.HostCanvas.MouseLeftButtonDown -= HostCanvas_MouseLeftButtonDown;
            envelopesCanvas.HostCanvas.MouseLeftButtonUp -= HostCanvas_MouseLeftButtonUp;

            envelopesCanvas.HostCanvas.ReleaseMouseCapture();
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
                if (envelopesCanvas.IsSelectedLayer(this))
                {
                    Add_Point(newBoxPosition);
                    e.Handled = true;
                }
            }
        }

        private void EnvelopesCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.Width = ((Canvas)sender).Width;
            this.Height = ((Canvas)sender).Height;
            Draw();
        }

        internal override double DefaulBoxValue()
        {
            int track = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].Track;
            double ret = envelopeBlockMachine.GetParamDefaulValueScaled(EnvelopePatternIndex, envelopeParamIndex, track);
            return ret;
        }

        internal override double MaxBoxValue()
        {
            return ENVELOPE_MAX;
        }

        private void HostCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            newBoxPositionFromMenu = e.GetPosition(envelopesCanvas.HostCanvas);
            e.Handled = true;
        }

        private void Evb_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            envelopesCanvas.SelectEnvelope(this);
            if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && !((EnvelopeBox)sender).Freezed)
            {
                // Delete always
                //if (envelopesCanvas.IsSelectedLayer(this))
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
                var ebox = (EnvelopeBox)sender;
                this.draggedBoxPreviousXPos = Canvas.GetLeft(ebox);
                this.draggedBoxPreviousYPos = Canvas.GetTop(ebox);

                ebox.DraggedEnvIndex = boxDraggedIndex;
                ebox.EnvelopePatternIndex = envelopePatternIndex;
                ebox.EnvelopeParamIndex = envelopeParamIndex;
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

                    if (layoutMode == SequencerLayout.Vertical)
                    {
                        point.Y = SnapToTime(point.Y, draggedBoxPreviousYPos);
                        point.X = SnapToBox(boxDraggedIndex, point.X, draggedBoxPreviousXPos);
                    }
                    else
                    {
                        point.X = SnapToTime(point.X, draggedBoxPreviousXPos);
                        point.Y = SnapToBox(boxDraggedIndex, point.Y, draggedBoxPreviousYPos);
                    }

                    point = UpdateEnvelopeBoxCenterPosition(evb, point, false);

                    UpdateEnvPoint(evb, point);
                    envelopeBlockMachine.UpdateSpline(envelopePatternIndex, envelopeParamIndex);

                    envelopeBlockMachine.NotifyBuzzDataChanged();
                    envelopeBlockMachine.RaisePropertyChangedEnv(evb);

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
                    //envelopesCanvas.MouseRightButtonDown += HostCanvas_MouseRightButtonDown;
                }
                else
                {
                    //envelopesCanvas.MouseRightButtonDown -= HostCanvas_MouseRightButtonDown;
                    this.Visibility = Visibility.Hidden;
                }
            }
        }

        MenuItem miEnv;
        MenuItem menuItemLayerVisible;
        MenuItem miEnabled;
        MenuItem miTrack;
        private double draggedBoxPreviousXPos;
        private double draggedBoxPreviousYPos;

        public int EnvelopePatternIndex { get => envelopePatternIndex; set => envelopePatternIndex = value; }

        public MenuItem CreateEnvelopeMenu()
        {
            MenuItem ret = null;

            string macName = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].MachineName;
            if (macName == "")
                ret = CreateEnvelopeMenuNotAssigned();
            else
                ret = CreateEnvelopeMenuAssigned();

            return ret;
        }

        public MenuItem CreateEnvelopeMenuNotAssigned()
        {
            return CreateAllMachineMenu();
        }

        private MenuItem CreateAllMachineMenu()
        {
            MenuItem miMac = new MenuItem();
            miMac.Header = "Assign";

            object dummySub2 = new object();
            miMac.Items.Add(dummySub2);
            miMac.SubmenuOpened += delegate
            {
                if (miMac.Items[0].GetType() == typeof(object))
                {
                    miMac.Items.RemoveAt(0);
                    foreach (IMachine mac in Global.Buzz.Song.Machines)
                    {
                        MenuItem mi = new MenuItem();
                        mi.Header = mac.Name;
                        mi.Tag = mac;
                        object dummySub = new object();
                        mi.Items.Add(dummySub);

                        mi.SubmenuOpened += delegate
                        {
                            if (mi.Items[0].GetType() == typeof(object))
                            {
                                mi.Items.RemoveAt(0);
                                CreateParamsGroupMenu((IMachine)mi.Tag, mi);
                            }
                        };
                        miMac.Items.Add(mi);
                    }
                }
            };

            return miMac;
        }

        private void CreateParamsGroupMenu(IMachine mac, MenuItem miParent)
        {
            int i = 0;
            foreach (IParameterGroup pg in mac.ParameterGroups)
            {
                MenuItem mi = new MenuItem();
                mi.Header = "Group " + i;
                mi.Tag = new Tuple<int, IParameterGroup>(i, pg);
                i++;

                if (pg.Parameters.Count > 0)
                {
                    object dummySub = new object();
                    mi.Items.Add(dummySub);

                    mi.SubmenuOpened += delegate
                    {
                        if (mi.Items[0].GetType() == typeof(object))
                        {
                            mi.Items.Clear();
                            IParameterGroup pg1 = ((Tuple<int, IParameterGroup>)mi.Tag).Item2;
                            foreach (IParameter par in pg1.Parameters)
                            {
                                MenuItem miPar = new MenuItem();
                                miPar.Header = par.Name;
                                mi.Items.Add(miPar);
                                miPar.Click += Mi_Click_Assign;
                            }
                        }
                    };

                    miParent.Items.Add(mi);
                }
            }
        }


        private void Mi_Click_Assign(object sender, RoutedEventArgs e)
        {
            MenuItem mi = (MenuItem)sender;
            MenuItem miPg = (MenuItem)mi.Parent;
            MenuItem miMachine = (MenuItem)miPg.Parent;

            string paramName = (string)mi.Header;
            int paramGroup = ((Tuple<int, IParameterGroup>)miPg.Tag).Item1;
            string machine = (string)miMachine.Header;

            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].ParamName = paramName;
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].ParamGroup = paramGroup;
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].MachineName = machine;
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].Track = 0;

            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeVisible = true;
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeEnabled = true;

            envelopeBlockMachine.ResetEnvPoints(envelopePatternIndex, envelopeParamIndex);
            envelopeBlockMachine.UpdateEnvPoints(envelopePatternIndex, envelopeParamIndex);
            envelopeBlockMachine.UpdateSpline(envelopePatternIndex, envelopeParamIndex);

            envelopeBlockMachine.RaisePropertyChangedVisibility(this);
            envelopeBlockMachine.RaisePropertyUpdateMenu(envelopesCanvas.HostCanvas);

            // Global.Buzz.DCWriteLine("Envelope Machine: " + machine + " | " + paramGroup + " | " + paramName);

        }

        public MenuItem CreateEnvelopeMenuAssigned()
        {
            string machineName = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].MachineName;
            string paramName = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].ParamName;


            miEnv = new MenuItem();
            miEnv.Header = machineName + " | " + paramName;

            object dummySub2 = new object();

            miEnv.Items.Add(dummySub2);
            miEnv.SubmenuOpened += delegate
            {
                if (miEnv.Items[0].GetType() == typeof(object))
                {
                    miEnv.Items.Clear();

                    miEnabled = new MenuItem();
                    miEnabled.Header = "Enabled";
                    miEnabled.IsCheckable = true;
                    miEnabled.IsChecked = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeEnabled;
                    miEnabled.Checked += MiEnabled_Checked;
                    miEnabled.Unchecked += MiEnabled_Unchecked;
                    miEnv.Items.Add(miEnabled);
                    menuItemLayerVisible = new MenuItem();
                    menuItemLayerVisible.Header = "Visible";
                    menuItemLayerVisible.IsCheckable = true;
                    menuItemLayerVisible.IsChecked = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeVisible;
                    menuItemLayerVisible.Checked += MenuItemPanLayerVisible_Checked;
                    menuItemLayerVisible.Unchecked += MenuItemPanLayerVisible_Unchecked;

                    miEnv.Items.Add(menuItemLayerVisible);
                    miEnv.Items.Add(new Separator());

                    MenuItem mi = new MenuItem();
                    mi.Header = "Add Point";
                    mi.Click += Mi_Click_Add_Point;
                    miEnv.Items.Add(mi);
                    mi = new MenuItem();
                    mi.Header = "Freeze All";
                    mi.Click += Mi_Click_Freeze_All;
                    miEnv.Items.Add(mi);
                    mi = new MenuItem();
                    mi.Header = "Unfreeze All";
                    mi.Click += Mi_Click_Unfreeze_All;
                    miEnv.Items.Add(mi);
                    mi = new MenuItem();
                    mi.Header = "Reset";
                    mi.Click += Mi_Click_Reset;
                    miEnv.Items.Add(mi);
                    mi = new MenuItem();
                    mi.Header = "Unassign";
                    mi.Click += Mi_Click_Unassign;
                    miEnv.Items.Add(mi);
                    mi = new MenuItem();
                    mi.Header = "Curves";
                    mi.IsCheckable = true;
                    mi.IsChecked = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeCurves;
                    mi.Checked += Mi_Checked_Curves;
                    mi.Unchecked += Mi_Unchecked_Curves;
                    miEnv.Items.Add(mi);
                    mi = new MenuItem();
                    mi.Header = "Logarithmic Param Value";
                    mi.IsCheckable = true;
                    mi.IsChecked = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].ConvertToLogarithmic;
                    mi.Checked += Mi_Checked_ToLog;
                    mi.Unchecked += Mi_Unchecked_ToLog;
                    miEnv.Items.Add(mi);

                    CreateTrackSubMenu(miEnv);

                    foreach (Control item in miEnv.Items)
                        if (item != menuItemLayerVisible && item != miEnabled)
                            item.IsEnabled = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeVisible;
                }
            };
            return miEnv;
        }

        private void Mi_Unchecked_ToLog(object sender, RoutedEventArgs e)
        {
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].ConvertToLogarithmic = false;

            envelopeBlockMachine.NotifyBuzzDataChanged();
        }

        private void Mi_Checked_ToLog(object sender, RoutedEventArgs e)
        {
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].ConvertToLogarithmic = true;

            envelopeBlockMachine.NotifyBuzzDataChanged();
        }

        private void CreateTrackSubMenu(MenuItem miParent)
        {
            miTrack = new MenuItem();
            miTrack.Header = "Track";
            miParent.Items.Add(miTrack);

            object dummySub = new object();
            miTrack.Items.Add(dummySub);

            miTrack.SubmenuOpened += delegate
            {
                int trackCount = 1;
                IMachine mac = Global.Buzz.Song.Machines.First(m => m.Name == envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].MachineName);

                if (mac != null)
                    trackCount = mac.TrackCount > 0 ? mac.TrackCount : 1;

                trackCount = trackCount > 32 ? 32 : trackCount; // Max 32 tracks supported

                miTrack.Items.Clear();
                for (int i = 0; i < trackCount; i++)
                {
                    MenuItem mi = new MenuItem();
                    mi.Header = "" + i;
                    mi.Tag = i;
                    mi.IsCheckable = true;
                    miTrack.Items.Add(mi);

                    if (i == envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].Track)
                    {
                        mi.IsChecked = true;
                    }
                    mi.Checked += Mi_Checked;
                    mi.Unchecked += Mi_Unchecked;
                }

                if (envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].Track >= trackCount)
                {
                    ((MenuItem)miTrack.Items[0]).IsChecked = true;
                    envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].Track = 0;
                }
            };
        }

        private void Mi_Unchecked(object sender, RoutedEventArgs e)
        {
            MenuItem mi = (MenuItem)sender;
            mi.Checked -= Mi_Checked;
            mi.IsChecked = true;
            mi.Checked += Mi_Checked;
            e.Handled = true;
        }

        private void Mi_Checked(object sender, RoutedEventArgs e)
        {
            foreach (MenuItem mi in miTrack.Items)
            {
                if (mi != sender)
                    mi.IsChecked = false;
            }

            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].Track = (int)((MenuItem)sender).Tag;
            envelopeBlockMachine.RaisePropertyUpdateMenu(envelopesCanvas.HostCanvas);
        }

        private void Mi_Click_Unassign(object sender, RoutedEventArgs e)
        {
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].ParamName = "";
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].ParamGroup = 0;
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].MachineName = "";
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].Track = 0;

            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeVisible = false;
            EnvelopeVisible = false;
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints.Clear();
            envelopeBlockMachine.UpdateEnvPoints(envelopePatternIndex, envelopeParamIndex);
            envelopeBlockMachine.RaisePropertyUnassign(this);
        }

        private void Mi_Unchecked_Curves(object sender, RoutedEventArgs e)
        {
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeCurves = false;

            UpdatePolyLinePath();
            envelopeBlockMachine.RaisePropertyReDraw(this);
            envelopeBlockMachine.NotifyBuzzDataChanged();

        }

        private void Mi_Checked_Curves(object sender, RoutedEventArgs e)
        {
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeCurves = true;
            envelopeBlockMachine.UpdateSpline(envelopePatternIndex, envelopeParamIndex);
            UpdatePolyLinePath();
            envelopeBlockMachine.RaisePropertyReDraw(this);
            envelopeBlockMachine.NotifyBuzzDataChanged();
        }

        private void Mi_Click_Reset(object sender, RoutedEventArgs e)
        {
            envelopeBlockMachine.ResetEnvPoints(envelopePatternIndex, envelopeParamIndex);
            this.LoadData();
            if (envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeCurves)
                envelopeBlockMachine.UpdateSpline(envelopePatternIndex, envelopeParamIndex);
            this.Draw();
            envelopeBlockMachine.RaisePropertyReDraw(this);
            envelopeBlockMachine.NotifyBuzzDataChanged();
        }

        private void MiEnabled_Unchecked(object sender, RoutedEventArgs e)
        {
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeEnabled = false;
            envelopeBlockMachine.NotifyBuzzDataChanged();
        }

        private void MiEnabled_Checked(object sender, RoutedEventArgs e)
        {
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeEnabled = true;
            envelopeBlockMachine.NotifyBuzzDataChanged();
        }

        private void MenuItemPanLayerVisible_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (Control item in miEnv.Items)
                if (item != menuItemLayerVisible && item != miEnabled)
                    item.IsEnabled = false;

            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeVisible = false;
            EnvelopeVisible = false;
            envelopeBlockMachine.RaisePropertyChangedVisibility(this);
        }

        private void MenuItemPanLayerVisible_Checked(object sender, RoutedEventArgs e)
        {
            foreach (Control item in miEnv.Items)
                if (item != menuItemLayerVisible && item != miEnabled)
                    item.IsEnabled = true;

            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeVisible = true;
            EnvelopeVisible = true;
            envelopeBlockMachine.RaisePropertyChangedVisibility(this);
        }


        private void Mi_Click_Unfreeze_All(object sender, RoutedEventArgs e)
        {
            List<EnvelopePoint> epl = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints;
            foreach (EnvelopePoint ep in epl)
                ep.Freezed = false;

            envelopeBlockMachine.RaisePropertyReDraw(this);
        }

        private void Mi_Click_Freeze_All(object sender, RoutedEventArgs e)
        {
            List<EnvelopePoint> epl = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints;
            foreach (EnvelopePoint ep in epl)
                ep.Freezed = true;

            envelopeBlockMachine.RaisePropertyReDraw(this);
        }

        private void Mi_Click_Add_Point(object sender, RoutedEventArgs e)
        {
            Add_Point(newBoxPositionFromMenu);
        }

        private void Add_Point(Point pos)
        {
            Brush brushBlack = Brushes.Black;
            if (brushBlack.CanFreeze) brushBlack.Freeze();
            EnvelopeBox evb = new EnvelopeBox(this, 10, 10, fillBrush, brushBlack);

            double timeStamp;
            double value;

            if (layoutMode == SequencerLayout.Vertical)
            {
                pos.Y = SnapToTime(pos.Y, double.MinValue);

                pos = UpdateEnvelopeBoxCenterPosition(evb, pos, true);
                Canvas.SetLeft(evb, pos.X - evb.Width / 2.0);
                Canvas.SetTop(evb, pos.Y - evb.Height / 2.0);

                // Update time stamp
                timeStamp = ConvertPixelToTimeStampInSeconds(pos);

                value = pos.X;
            }
            else
            {
                pos.X = SnapToTime(pos.X, double.MinValue);

                pos = UpdateEnvelopeBoxCenterPosition(evb, pos, true);
                Canvas.SetLeft(evb, pos.X - evb.Width / 2.0);
                Canvas.SetTop(evb, pos.Y - evb.Height / 2.0);

                // Update time stamp
                timeStamp = ConvertPixelToTimeStampInSeconds(pos);

                value = pos.Y;
            }

            evb.TimeStamp = timeStamp;
            int index = GetIndexForNewEnvelope(evb, timeStamp);
            evb.Index = index;

            // Update Machine Data
            SaveEnvelopeBoxData(evb, index, ScreenToValue(value), timeStamp, false);
            evb.EnvelopePatternIndex = envelopePatternIndex;
            evb.EnvelopeParamIndex = envelopeParamIndex;
            envelopeBlockMachine.RaisePropertyChangedEnvBoxAdded(evb);
            envelopeBlockMachine.NotifyBuzzDataChanged();
        }

        private void AddEnvelopeBoxToCanvas(EnvelopeBox evb, int index)
        {
            Point pos = new Point();
            pos.X = Canvas.GetLeft(evb) + evb.Width / 2.0;
            pos.Y = Canvas.GetTop(evb) + evb.Height / 2.0;

            this.Children.Add(evb);
            envPolyLine.Points.Insert(index, pos);
        }

        private void SaveEnvelopeBoxData(EnvelopeBox evb, int index, double value, double timeStamp, bool freezed)
        {
            EnvelopePoint vep = new EnvelopePoint();
            vep.Value = value;
            vep.TimeStamp = timeStamp;
            vep.Freezed = freezed;
            envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints.Insert(index, vep);

            // Update spline & draw
            envelopeBlockMachine.UpdateSpline(envelopePatternIndex, envelopeParamIndex);
            UpdatePolyLinePath();
        }

        internal override Point MakeSureNotOverlapping(Point pos)
        {
            Point ret = pos;

            double maxTimeStamp = DrawLengthInSeconds;
            double posTimeStamp = ConvertPixelToTimeStampInSeconds(pos);

            List<EnvelopePoint> panEnvelopePoints = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints;

            if (layoutMode == SequencerLayout.Vertical)
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

                for (int i = 0; i < panEnvelopePoints.Count; i++)
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
                envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints[index].Freezed = v;

            envelopeBlockMachine.RaisePropertyReDraw(this);
        }

        public void SetEnvelopeBoxValue(EnvelopeBox envelopeBox)
        {
            envelopesCanvas.SelectEnvelope(this);

            //Window wnd = Window.GetWindow(this);
            Point p = new Point(envelopeBox.Width * 2, envelopeBox.Height * 2); //Mouse.GetPosition(wnd);
            p = envelopeBox.PointToScreen(p);

            IParameter par = envelopeBlockMachine.GetParameter(EnvelopePatternIndex, envelopeParamIndex);

            string desc;
            int paramValue;
            int index = envelopeBoxes.IndexOf(envelopeBox);
            double realVal = envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints[index].Value;

            envelopeBlockMachine.GetParamValues(envelopePatternIndex, envelopeParamIndex, realVal, out desc, out paramValue);

            SetValEditWindow svew = new SetValEditWindow(paramValue, par.MinValue, par.MaxValue)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = p.X,
                Top = p.Y
            };

            svew.Top -= svew.Height;

            new WindowInteropHelper(svew).Owner = ((HwndSource)PresentationSource.FromVisual(this)).Handle;

            if ((bool)svew.ShowDialog())
            {
                envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints[index].Value =
                    envelopeBlockMachine.GetParamValueFromInt(par, svew.Value);
                envelopeBox.EnvelopePatternIndex = envelopePatternIndex;
                envelopeBox.EnvelopeParamIndex = envelopeParamIndex;
                envelopeBox.DraggedEnvIndex = index;
                if (envelopeBlockMachine.MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeCurves)
                    envelopeBlockMachine.UpdateSpline(envelopePatternIndex, envelopeParamIndex);
                envelopeBlockMachine.RaisePropertyChangedEnv(envelopeBox);
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
