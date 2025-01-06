using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace BuzzGUI.EnvelopeControl
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class EnvelopeControl : UserControl
    {
        public static readonly DependencyProperty EnvelopeProperty = DependencyProperty.Register("Envelope", typeof(IEnvelope), typeof(EnvelopeControl), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnEnvelopeChanged)));

        public IEnvelope Envelope
        {
            get { return (IEnvelope)GetValue(EnvelopeControl.EnvelopeProperty); }
            set { SetValue(EnvelopeControl.EnvelopeProperty, value); }
        }

        static void OnEnvelopeChanged(DependencyObject controlInstance, DependencyPropertyChangedEventArgs args)
        {
            var x = (EnvelopeControl)controlInstance;
            var oldval = args.OldValue as IEnvelope;
            var newval = args.NewValue as IEnvelope;

            if (oldval != null)
                oldval.PropertyChanged -= x.envelope_PropertyChanged;

            if (newval != null)
                newval.PropertyChanged += x.envelope_PropertyChanged;

            x.ModelChanged();
        }

        const int MaxValue = 65535;
        const int Range = MaxValue + 1;

        List<Point> points;
        Vector scale;
        int sustainPoint = -1;

        int handleSize = 7;
        public int HandleSize
        {
            get { return handleSize; }
            set { handleSize = value; CreateVisuals(); }
        }

        void envelope_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Points": ModelChanged(); break;
                case "SustainPoint": ModelChanged(); break;
            }

        }

        void ModelChanged()
        {
            if (Envelope != null)
            {
                var ep = Envelope.Points;
                points = new List<Point>(ep.Count);
                for (int i = 0; i < ep.Count; i++) points.Add(new Point(ep[i].Item1, ep[i].Item2));

                var sp = Envelope.SustainPoint;
                sustainPoint = sp < points.Count ? sp : -1;
                sustainRectangle.Visibility = sustainPoint >= 0 ? Visibility.Visible : Visibility.Collapsed;

                CreateVisuals();
            }
            else
            {
                Reset();
            }
        }

        void ViewChanged()
        {
            if (Envelope != null)
            {
                var ep = new Tuple<int, int>[points.Count];
                for (int i = 0; i < points.Count; i++) ep[i] = Tuple.Create((int)points[i].X, (int)points[i].Y);
                Envelope.Update(ep, sustainPoint);
            }
        }

        public ICommand InvertCommand { get; private set; }
        public ICommand MirrorCommand { get; private set; }
        public ICommand ResetCommand { get; private set; }


        public EnvelopeControl()
        {
            InitializeComponent();
            DataContext = this;

            grid.SizeChanged += (sender, e) =>
            {
                handleCanvas.Width = e.NewSize.Width;
                handleCanvas.Height = e.NewSize.Height;
                lineCanvas.Width = e.NewSize.Width;
                lineCanvas.Height = e.NewSize.Height;
                sustainCanvas.Width = e.NewSize.Width;
                sustainCanvas.Height = e.NewSize.Height;
                sustainRectangle.Height = e.NewSize.Height;
                playPosRectangle.Height = e.NewSize.Height;
                scale = new Vector((e.NewSize.Width - HandleSize) / Range, (e.NewSize.Height - HandleSize) / Range);
                UpdateVisuals();
            };

            handleCanvas.MouseLeftButtonDown += (sender, e) =>
            {
                AddPoint(DPToLP(e.GetPosition(handleCanvas)));
            };


            DispatcherTimer dt = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(50) };
            dt.Tick += (sender, e) =>
            {
                if (Envelope != null)
                {
                    int pos = Envelope.PlayPosition;
                    if (pos >= 0)
                    {
                        Point p = LPToDP(new Point(pos, 0));
                        Canvas.SetLeft(playPosRectangle, p.X);
                        playPosRectangle.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        playPosRectangle.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    playPosRectangle.Visibility = Visibility.Collapsed;
                }
            };
            dt.Start();


            this.Unloaded += (sender, e) =>
            {
                dt.Stop();
            };

            InvertCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    for (int i = 0; i < points.Count; i++) points[i] = new Point(points[i].X, MaxValue - points[i].Y);
                    UpdateVisuals();
                    ViewChanged();
                }
            };

            MirrorCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    var op = points.ToArray();
                    for (int i = 0; i < points.Count; i++) points[points.Count - 1 - i] = new Point(MaxValue - op[i].X, op[i].Y);
                    UpdateVisuals();
                    ViewChanged();
                }
            };

            ResetCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => Reset()
            };

            Reset();
        }

        Point Clamp(Point p) { return new Point(Math.Min(Math.Max(Math.Round(p.X), 0), MaxValue), Math.Min(Math.Max(Math.Round(p.Y), 0), MaxValue)); }
        Point DPToLP(Point p) { return Clamp((Point)((Vector)p).ElementDiv(scale)); }
        Point LPToDP(Point p) { return (Point)((Vector)p).ElementMul(scale); }
        Vector DVToLV(Vector v) { return v.ElementDiv(scale); }

        int GetPointLeftTo(double x)
        {
            for (int i = 0; i < points.Count; i++)
                if (points[i].X > x) return i - 1;

            Debug.Assert(false);
            return -1;
        }

        void Reset()
        {
            points = new List<Point>();
            points.Add(new Point(0, 0));
            points.Add(new Point(MaxValue, MaxValue));
            sustainPoint = -1;
            sustainRectangle.Visibility = Visibility.Collapsed;
            CreateVisuals();
            ViewChanged();
        }

        internal void AddPoint(Point p)
        {
            p.X = Math.Min(Math.Max(p.X, 1), MaxValue - 1);
            int i = GetPointLeftTo(p.X);
            points.Insert(i + 1, p);
            if (sustainPoint >= 0 && sustainPoint > i) sustainPoint++;
            CreateVisuals();
            ViewChanged();
        }

        internal void DeletePoint(int i)
        {
            if (i <= 0 || i >= points.Count - 1)
                return;

            points.RemoveAt(i);

            if (sustainPoint == i) { sustainPoint = -1; sustainRectangle.Visibility = Visibility.Collapsed; }
            else if (sustainPoint >= 0 && sustainPoint > i) sustainPoint--;

            CreateVisuals();
            ViewChanged();
        }

        internal void SustainPoint(int i)
        {
            if (i == sustainPoint || i < 0)
            {
                sustainPoint = -1;
                sustainRectangle.Visibility = Visibility.Collapsed;
            }
            else
            {
                sustainPoint = i;
                sustainRectangle.Visibility = Visibility.Visible;

                UpdateVisual(i);
            }

            ViewChanged();
        }

        void UpdateVisual(int i)
        {
            if (handleCanvas.Children.Count > i)
            {
                var p = LPToDP(points[i]);
                Canvas.SetLeft(handleCanvas.Children[i], p.X);
                Canvas.SetTop(handleCanvas.Children[i], p.Y);

                if (i > 0)
                {
                    var pprev = LPToDP(points[i - 1]);
                    Line l = (Line)lineCanvas.Children[i - 1];
                    l.X1 = pprev.X + HandleSize / 2.0;
                    l.Y1 = pprev.Y + HandleSize / 2.0;
                    l.X2 = p.X + HandleSize / 2.0;
                    l.Y2 = p.Y + HandleSize / 2.0;
                }

                if (i < handleCanvas.Children.Count - 1)
                {
                    var pnext = LPToDP(points[i + 1]);
                    Line l = (Line)lineCanvas.Children[i];
                    l.X1 = p.X + HandleSize / 2.0;
                    l.Y1 = p.Y + HandleSize / 2.0;
                    l.X2 = pnext.X + HandleSize / 2.0;
                    l.Y2 = pnext.Y + HandleSize / 2.0;
                }

                if (i == sustainPoint)
                {
                    Canvas.SetLeft(sustainRectangle, Math.Floor(p.X + HandleSize / 2.0));
                }
            }

        }

        void UpdateVisuals()
        {
            for (int i = 0; i < points.Count; i++)
                UpdateVisual(i);
        }

        void CreateVisuals()
        {
            lineCanvas.Children.Clear();
            handleCanvas.Children.Clear();

            var lineStyle = TryFindResource("EnvelopeControlLineStyle") as Style;

            for (int _i = 0; _i < points.Count; _i++)
            {
                int i = _i;

                if (i < points.Count - 1)
                {
                    var line = new Line() { Style = lineStyle };
                    lineCanvas.Children.Add(line);
                }

                var handle = new HandleControl(this, i) { Width = HandleSize, Height = HandleSize };

                handle.MouseDown += (sender, e) =>
                {
                    if (e.ChangedButton == MouseButton.Middle) DeletePoint(i);
                    else if (e.ChangedButton == MouseButton.XButton1) SustainPoint(i);
                };

                Point dragStartPoint = new Point();

                new Dragger
                {
                    Element = handle,
                    Container = handleCanvas,
                    Gesture = new DragMouseGesture { Button = MouseButton.Left },
                    MouseOverCursor = Cursors.Hand,
                    Mode = DraggerMode.DeltaFromOrigin,
                    BeginDrag = (p, a, cc) => { dragStartPoint = points[i]; },
                    Drag = delta =>
                    {
                        var p = Clamp(dragStartPoint + DVToLV((Vector)delta));
                        if (i == 0) p.X = 0;
                        else if (i == points.Count - 1) p.X = MaxValue;
                        else p.X = Math.Min(Math.Max(p.X, points[i - 1].X + 1), points[i + 1].X - 1);
                        points[i] = p;
                        UpdateVisual(i);
                        ViewChanged();
                    }
                };

                handleCanvas.Children.Add(handle);
            }

            UpdateVisuals();
        }


    }
}
