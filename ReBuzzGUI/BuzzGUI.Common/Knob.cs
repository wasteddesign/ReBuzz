using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BuzzGUI.Common
{
    [TemplatePart(Name = "RotatingLayer1", Type = typeof(Grid)),
    TemplatePart(Name = "RotatingLayer1", Type = typeof(Grid)),
    TemplatePart(Name = "KnobGrid", Type = typeof(Grid))]
    public class Knob : Control
    {
        const int SnapPixels = 8;

        static Knob()
        {
            EventManager.RegisterClassHandler(typeof(Knob),
                Mouse.MouseWheelEvent, new MouseWheelEventHandler(Knob.OnMouseWheel), true);

            DefaultStyleKeyProperty.OverrideMetadata(typeof(Knob), new FrameworkPropertyMetadata(typeof(Knob)));
        }

        const double mouseWheelStep = (1.0 / 78.0);
        const double mouseWheelFineStep = (1.0 / (8 * 78.0));
        private static void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Knob control = (Knob)sender;

            var delta = (e.Delta / 120.0) * (Keyboard.Modifiers == ModifierKeys.Control ? mouseWheelFineStep : mouseWheelStep);
            control.ChangeValue(delta * (control.Maximum - control.Minimum));
            e.Handled = true;
        }

        public static readonly DependencyProperty ValueProperty =
                    DependencyProperty.Register(
                        "Value", typeof(double), typeof(Knob),
                        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, new PropertyChangedCallback(OnValueChanged)));

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        private static void OnValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            Knob knob = (Knob)obj;
            knob.UpdateRotateTransform();

            knob.RaiseEvent(new RoutedPropertyChangedEventArgs<double>((double)args.OldValue, (double)args.NewValue, ValueChangedEvent));
        }

        #region Events
        public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(
            "ValueChanged", RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<double>), typeof(Knob));

        public event RoutedPropertyChangedEventHandler<double> ValueChanged
        {
            add { AddHandler(ValueChangedEvent, value); }
            remove { RemoveHandler(ValueChangedEvent, value); }
        }
        #endregion

        public double Minimum { get; set; }
        public double Maximum { get; set; }

        double NormalizedValue { get { return (Value - Minimum) / (Maximum - Minimum); } }
        bool IsBipolar { get { return (Minimum < 0 && Maximum > 0) || (Minimum > 0 && Maximum < 0); } }

        int dragHeight = 100;
        public int DragHeight { get { return dragHeight; } set { dragHeight = value; } }
        double PixelsToRange(double pixels) { return pixels * (Maximum - Minimum) / DragHeight; }
        double SnapRange { get { return PixelsToRange(SnapPixels); } }

        readonly RotateTransform rt = new RotateTransform(0);
        Grid grid;
        const double maxAngle = 90 + 45;

        double dragStartY;
        double dragStartValue;
        public bool Dragging { get; private set; }

        public Knob()
        {
            Minimum = 0.0;
            Maximum = 1.0;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            Grid rl1 = EnforceInstance<Grid>("RotatingLayer1");
            Grid rl2 = EnforceInstance<Grid>("RotatingLayer2");

            rl1.RenderTransform = rl2.RenderTransform = rt;
            rl1.RenderTransformOrigin = rl2.RenderTransformOrigin = new Point(0.5, 0.5);

            grid = EnforceInstance<Grid>("KnobGrid");
            grid.MouseLeftButtonDown += new MouseButtonEventHandler(g_MouseLeftButtonDown);
            grid.MouseLeftButtonUp += new MouseButtonEventHandler(g_MouseLeftButtonUp);
            grid.MouseMove += new MouseEventHandler(g_MouseMove);
            grid.LostMouseCapture += new MouseEventHandler(g_LostMouseCapture);

            UpdateRotateTransform();
        }

        T EnforceInstance<T>(string partName) where T : FrameworkElement, new()
        {
            T element = GetTemplateChild(partName) as T;
            System.Diagnostics.Debug.Assert(element != null);
            if (element == null) element = new T();
            return element;
        }

        void UpdateRotateTransform()
        {
            rt.Angle = NormalizedValue * 2 * maxAngle - maxAngle;
        }

        void ChangeValue(double delta)
        {
            if (delta == 0) return;
            double v = Value + delta;
            v = Math.Max(v, Minimum);
            v = Math.Min(v, Maximum);
            Value = v;
        }

        void g_MouseMove(object sender, MouseEventArgs e)
        {
            if (Dragging)
            {
                double dy = dragStartY - e.GetPosition(null).Y;

                if (Keyboard.Modifiers == ModifierKeys.Control)
                    dy /= 10.0;

                double v = dragStartValue + PixelsToRange(dy);

                if (IsBipolar)
                {
                    if ((v > 0 && v < SnapRange) || (v < 0 && v > -SnapRange))
                        v = 0;
                    else if (v > 0)
                        v -= SnapRange;
                    else if (v < 0)
                        v += SnapRange;
                }

                ChangeValue(v - Value);
            }
        }

        void g_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Dragging = false;
            grid.ReleaseMouseCapture();
            e.Handled = true;
        }

        void g_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
                BeginDrag(e);
        }

        void g_LostMouseCapture(object sender, MouseEventArgs e)
        {
            Dragging = false;
            grid.ReleaseMouseCapture();
            e.Handled = true;
        }

        public void BeginDrag(MouseButtonEventArgs e)
        {
            dragStartY = e.GetPosition(null).Y;
            dragStartValue = Value;

            if (IsBipolar)
            {
                if (dragStartValue < 0)
                    dragStartValue -= SnapRange;
                else if (dragStartValue > 0)
                    dragStartValue += SnapRange;
            }

            Dragging = true;
            grid.CaptureMouse();
            e.Handled = true;
        }
    }
}
