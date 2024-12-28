using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shapes;

namespace BuzzGUI.Common
{
    /// <summary>
    /// Follow steps 1a or 1b and then 2 to use this custom control in a XAML file.
    ///
    /// Step 1a) Using this custom control in a XAML file that exists in the current project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:BuzzGUI.Common"
    ///
    ///
    /// Step 1b) Using this custom control in a XAML file that exists in a different project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:BuzzGUI.Common;assembly=BuzzGUI.Common"
    ///
    /// You will also need to add a project reference from the project where the XAML file lives
    /// to this project and Rebuild to avoid compilation errors:
    ///
    ///     Right click on the target project in the Solution Explorer and
    ///     "Add Reference"->"Projects"->[Browse to and select this project]
    ///
    ///
    /// Step 2)
    /// Go ahead and use your control in the XAML file.
    ///
    ///     <MyNamespace:ParameterSlider/>
    ///
    /// </summary>
    /// 
    [TemplatePart(Name = "PART_Thumb", Type = typeof(Thumb)),
    TemplatePart(Name = "PART_LeftButton", Type = typeof(RepeatButton)),
    TemplatePart(Name = "PART_RightButton", Type = typeof(RepeatButton)),
    TemplatePart(Name = "PART_Container", Type = typeof(StackPanel)),
    TemplatePart(Name = "PART_Indicator", Type = typeof(Rectangle))]
    public class ParameterSlider : Control
    {
        private static readonly RoutedCommand _increaseCommand;
        private static readonly RoutedCommand _decreaseCommand;

        Thumb thumb;
        RepeatButton leftButton;
        RepeatButton rightButton;
        StackPanel container;
        Rectangle indicatorRectangle;

        static ParameterSlider()
        {

            _increaseCommand = new RoutedCommand("IncreaseCommand", typeof(ParameterSlider));
            CommandManager.RegisterClassCommandBinding(typeof(ParameterSlider), new CommandBinding(_increaseCommand, OnIncreaseCommand));
            CommandManager.RegisterClassInputBinding(typeof(ParameterSlider), new InputBinding(_increaseCommand, new KeyGesture(Key.Right)));

            _decreaseCommand = new RoutedCommand("DecreaseCommand", typeof(ParameterSlider));
            CommandManager.RegisterClassCommandBinding(typeof(ParameterSlider), new CommandBinding(_decreaseCommand, OnDecreaseCommand));
            CommandManager.RegisterClassInputBinding(typeof(ParameterSlider), new InputBinding(_decreaseCommand, new KeyGesture(Key.Left)));

            EventManager.RegisterClassHandler(typeof(ParameterSlider),
                Mouse.MouseDownEvent, new MouseButtonEventHandler(ParameterSlider.OnMouseLeftButtonDown), true);

            EventManager.RegisterClassHandler(typeof(ParameterSlider),
                Mouse.MouseWheelEvent, new MouseWheelEventHandler(ParameterSlider.OnMouseWheel), true);

            EventManager.RegisterClassHandler(typeof(ParameterSlider),
                Keyboard.KeyDownEvent, new KeyEventHandler(ParameterSlider.OnKeyboardKeyDown), true);

            DefaultStyleKeyProperty.OverrideMetadata(typeof(ParameterSlider), new FrameworkPropertyMetadata(typeof(ParameterSlider)));
        }

        private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ParameterSlider control = (ParameterSlider)sender;

            if (!control.IsKeyboardFocusWithin)
            {
                e.Handled = control.Focus() || e.Handled;
            }
        }

        int mouseWheelAcc = 0;
        private static void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {

            ParameterSlider control = (ParameterSlider)sender;

            control.mouseWheelAcc += e.Delta;

            while (control.mouseWheelAcc <= -120)
            {
                control.mouseWheelAcc += 120;
                control.ChangeValue(control.Value - 1);
            }

            while (control.mouseWheelAcc >= 120)
            {
                control.mouseWheelAcc -= 120;
                control.ChangeValue(control.Value + 1);
            }

            e.Handled = true;
        }

        private static void OnKeyboardKeyDown(object sender, KeyEventArgs e)
        {
            ParameterSlider control = (ParameterSlider)sender;

            if (e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                e.Handled = true;
                Point p = control.PointToScreen(new Point(control.thumbX, control.ActualHeight + 20));

                ParameterValueEditor hw = new ParameterValueEditor(e.Key - Key.D0, control.Minimum, control.Maximum, false)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = p.X /= WPFExtensions.PixelsPerDip,
                    Top = p.Y /= WPFExtensions.PixelsPerDip
                };

                new WindowInteropHelper(hw).Owner = ((HwndSource)PresentationSource.FromVisual(control)).Handle;

                if (!(bool)hw.ShowDialog())
                    return;

                control.Value = hw.Value;
            }
        }

        public static RoutedCommand IncreaseCommand { get { return _increaseCommand; } }
        public static RoutedCommand DecreaseCommand { get { return _decreaseCommand; } }

        private static void OnIncreaseCommand(object sender, ExecutedRoutedEventArgs e)
        {
            ParameterSlider control = sender as ParameterSlider;
            if (control != null)
            {
                control.ChangeValue(control.Value + 1);
            }
        }
        private static void OnDecreaseCommand(object sender, ExecutedRoutedEventArgs e)
        {
            ParameterSlider control = sender as ParameterSlider;
            if (control != null)
            {
                control.ChangeValue(control.Value - 1);
            }
        }

        readonly PropertyChangeNotifier notifier;

        public ParameterSlider()
        {
            /*
			// leaks, http://agsmith.wordpress.com/2008/04/07/propertydescriptor-addvaluechanged-alternative/
			DependencyPropertyDescriptor.FromProperty(ActualWidthProperty, typeof(ParameterSlider)).
                AddValueChanged(this, delegate { RecalculateWidths(); });
			 */

            notifier = new PropertyChangeNotifier(this, "ActualWidth");
            notifier.ValueChanged += (sender, e) => { RecalculateWidths(); };

        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            thumb = EnforceInstance<Thumb>("PART_Thumb");
            leftButton = EnforceInstance<RepeatButton>("PART_LeftButton");
            rightButton = EnforceInstance<RepeatButton>("PART_RightButton");
            container = EnforceInstance<StackPanel>("PART_Container");
            indicatorRectangle = EnforceInstance<Rectangle>("PART_Indicator");

            thumb.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(thumb_MouseLeftButtonDown);
            thumb.PreviewMouseMove += new MouseEventHandler(thumb_MouseMove);
            thumb.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(thumb_MouseLeftButtonUp);
            thumb.LostMouseCapture += new MouseEventHandler(thumb_LostMouseCapture);
            leftButton.Click += new RoutedEventHandler(leftButton_Click);
            rightButton.Click += new RoutedEventHandler(rightButton_Click);

            RecalculateWidths();
        }

        T EnforceInstance<T>(string partName) where T : FrameworkElement, new()
        {
            T element = GetTemplateChild(partName) as T;
            System.Diagnostics.Debug.Assert(element != null);
            if (element == null) element = new T();
            return element;
        }

        int thumbX;
        int thumbW;


        void RecalculateWidths()
        {
            if (container == null)
                return;

            int totalw = (int)ActualWidth;
            int value = Value;

            thumbW = Math.Max(MinimumThumbWidth, totalw / (Maximum - Minimum + 1));

            if (thumbW > MinimumThumbWidth)
                thumbX = (value - Minimum) * totalw / (Maximum - Minimum + 1);
            else
                thumbX = (value - Minimum) * (totalw - thumbW) / Math.Max(1, Maximum - Minimum);


            leftButton.Width = Math.Max(0, thumbX);
            thumb.Width = thumbW;
            rightButton.Width = Math.Max(0, totalw - ((int)leftButton.Width + (int)thumb.Width));

            UpdateIndicator();
        }

        double lastX;
        double dragStart = 0;
        bool dragging;

        void thumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(this);
            lastX = p.X;
            dragStart = p.X - thumbX;
            dragging = true;
            thumb.CaptureMouse();
        }

        void thumb_MouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging)
                return;

            Point p = e.GetPosition(this);
            if (p.X == lastX) return;
            lastX = p.X;

            int value = Value;
            int oldval = value;


            int x = (int)(p.X - dragStart);
            int px = (int)p.X;

            int totalw = (int)ActualWidth;
            int mv = Math.Max(1, totalw - thumbW);

            if (thumbW > MinimumThumbWidth)
            {
                value = Minimum + px * (Maximum - Minimum + 1) / totalw;
            }
            else
            {
                value = (int)Math.Round(Minimum + (double)x * (Maximum - Minimum) / mv);
                //                value = Minimum + x * (Maximum - Minimum) / mv;
            }

            value = Math.Min(Maximum, Math.Max(Minimum, value));


            if (value != oldval)
            {
                RecalculateWidths();
                SetValue(ValueProperty, value);
                //   Value = value;
            }

        }


        void thumb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (dragging)
            {
                thumb.ReleaseMouseCapture();
                dragging = false;
            }
        }

        void thumb_LostMouseCapture(object sender, MouseEventArgs e)
        {
            dragging = false;
        }

        void leftButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeValue(Value - ((Maximum - Minimum) / 6 + 1));
        }

        void rightButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeValue(Value + ((Maximum - Minimum) / 6 + 1));
        }





        #region Value
        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set
            {
                //if (!dragging)
                SetValue(ValueProperty, value);
            }
        }

        void ChangeValue(int v)
        {
            Value = Math.Max(Minimum, Math.Min(Maximum, v));
        }

        /// <summary>
        /// Identifies the Value dependency property.
        /// </summary>
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                "Value", typeof(int), typeof(ParameterSlider),
                new FrameworkPropertyMetadata(0,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    new PropertyChangedCallback(OnValueChanged),
                    new CoerceValueCallback(CoerceValue)
                )
            );

        private static void OnValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            ParameterSlider control = (ParameterSlider)obj;

            int oldValue = (int)args.OldValue;
            int newValue = (int)args.NewValue;

            RoutedPropertyChangedEventArgs<int> e = new RoutedPropertyChangedEventArgs<int>(
                oldValue, newValue, ValueChangedEvent);

            control.OnValueChanged(e);
        }

        /// <summary>
        /// Raises the ValueChanged event.
        /// </summary>
        /// <param name="args">Arguments associated with the ValueChanged event.</param>
        protected virtual void OnValueChanged(RoutedPropertyChangedEventArgs<int> args)
        {
            RaiseEvent(args);
            RecalculateWidths();
        }

        private static object CoerceValue(DependencyObject element, object value)
        {
            int newValue = (int)value;
            ParameterSlider control = (ParameterSlider)element;

            newValue = Math.Max(control.Minimum, Math.Min(control.Maximum, newValue));

            return newValue;
        }
        #endregion

        #region Minimum
        public int Minimum
        {
            get { return (int)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(
                "Minimum", typeof(int), typeof(ParameterSlider),
                new FrameworkPropertyMetadata(0,
                    new PropertyChangedCallback(OnMinimumChanged), new CoerceValueCallback(CoerceMinimum)
                )
            );

        private static void OnMinimumChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
        {
            element.CoerceValue(MaximumProperty);
            element.CoerceValue(ValueProperty);
        }
        private static object CoerceMinimum(DependencyObject element, object value)
        {
            int minimum = (int)value;
            return minimum;
        }
        #endregion

        #region Maximum
        public int Maximum
        {
            get { return (int)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(
                "Maximum", typeof(int), typeof(ParameterSlider),
                new FrameworkPropertyMetadata(3,
                    new PropertyChangedCallback(OnMaximumChanged),
                    new CoerceValueCallback(CoerceMaximum)
                )
            );

        private static void OnMaximumChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
        {
            element.CoerceValue(ValueProperty);
        }

        private static object CoerceMaximum(DependencyObject element, object value)
        {
            int newMaximum = (int)value;
            return newMaximum;
        }
        #endregion

        #region MinimumThumbWidth
        public int MinimumThumbWidth
        {
            get { return (int)GetValue(MinimumThumbWidthProperty); }
            set { SetValue(MinimumThumbWidthProperty, value); }
        }

        public static readonly DependencyProperty MinimumThumbWidthProperty =
            DependencyProperty.Register(
                "MinimumThumbWidth", typeof(int), typeof(ParameterSlider),
                new FrameworkPropertyMetadata(12,
                    new PropertyChangedCallback(OnMinimumThumbWidthChanged),
                    new CoerceValueCallback(CoerceMinimumThumbWidth)
                )
            );

        private static void OnMinimumThumbWidthChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
        {
        }

        private static object CoerceMinimumThumbWidth(DependencyObject element, object value)
        {
            int x = (int)value;
            return Math.Max(1, x);
        }
        #endregion

        #region Indicator
        public double Indicator
        {
            get { return (double)GetValue(IndicatorProperty); }
            set
            {
                SetValue(IndicatorProperty, value);
            }
        }

        /// <summary>
        /// Identifies the Indicator dependency property.
        /// </summary>
        public static readonly DependencyProperty IndicatorProperty =
            DependencyProperty.Register(
                "Indicator", typeof(double), typeof(ParameterSlider),
                new FrameworkPropertyMetadata(-1.0,
                    FrameworkPropertyMetadataOptions.None,
                    new PropertyChangedCallback(OnIndicatorChanged)
                )
            );

        private static void OnIndicatorChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            ParameterSlider control = (ParameterSlider)obj;

            var oldValue = (double)args.OldValue;
            var newValue = (double)args.NewValue;

            control.UpdateIndicator();
        }

        void UpdateIndicator()
        {
            if (indicatorRectangle == null) return;

            var ind = Indicator;
            if (ind < 0.0)
            {
                indicatorRectangle.Visibility = Visibility.Collapsed;
            }
            else
            {
                indicatorRectangle.Visibility = Visibility.Visible;

                int totalw = (int)ActualWidth;
                double x = (double)thumbW / 2 + ind * (totalw - thumbW) - 1;
                indicatorRectangle.Margin = new Thickness((int)x, 0, 0, 0);
            }
        }

        #endregion



        #region Events
        /// <summary>
        /// Identifies the ValueChanged routed event.
        /// </summary>
        public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(
            "ValueChanged", RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<int>), typeof(ParameterSlider));

        /// <summary>
        /// Occurs when the Value property changes.
        /// </summary>
        public event RoutedPropertyChangedEventHandler<int> ValueChanged
        {
            add { AddHandler(ValueChangedEvent, value); }
            remove { RemoveHandler(ValueChangedEvent, value); }
        }
        #endregion

    }
}
