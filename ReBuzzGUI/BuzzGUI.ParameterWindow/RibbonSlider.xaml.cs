using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BuzzGUI.ParameterWindow
{
    /// <summary>
    /// Interaction logic for RibbonSlider.xaml
    /// </summary>
    public partial class RibbonSlider : UserControl
    {
        #region Properties

        public int Minimum
        {
            get { return (int)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(int), typeof(RibbonSlider),
                new UIPropertyMetadata(0, (d, e) => (d as RibbonSlider).UpdateEverything()));

        public int Maximum
        {
            get { return (int)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(int), typeof(RibbonSlider),
                new UIPropertyMetadata(1, (d, e) => (d as RibbonSlider).UpdateEverything()));

        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(int), typeof(RibbonSlider),
                new UIPropertyMetadata(0, (d, e) => (d as RibbonSlider).ValueChanged()));

        public Orientation Orientation
        {
            get { return (Orientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, Orientation); }
        }
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register("Orientation", typeof(Orientation), typeof(RibbonSlider),
                new UIPropertyMetadata(Orientation.Horizontal, (d, e) => (d as RibbonSlider).UpdateEverything()));

        public int ThumbWidth
        {
            get { return (int)GetValue(ThumbWidthProperty); }
            set { SetValue(ThumbWidthProperty, value); }
        }
        public static readonly DependencyProperty ThumbWidthProperty =
            DependencyProperty.Register("ThumbWidth", typeof(int), typeof(RibbonSlider),
                new UIPropertyMetadata(10, (d, e) => (d as RibbonSlider).UpdateEverything()));

        public Style ThumbStyle
        {
            get { return (Style)GetValue(ThumbStyleProperty); }
            set { SetValue(ThumbStyleProperty, value); }
        }
        public static readonly DependencyProperty ThumbStyleProperty =
            DependencyProperty.Register("ThumbStyle", typeof(Style), typeof(RibbonSlider),
                new UIPropertyMetadata());

        public Style BarStyle
        {
            get { return (Style)GetValue(BarStyleProperty); }
            set { SetValue(BarStyleProperty, value); }
        }
        public static readonly DependencyProperty BarStyleProperty =
            DependencyProperty.Register("BarStyle", typeof(Style), typeof(RibbonSlider),
                new UIPropertyMetadata());

        #endregion

        public RibbonSlider()
        {
            InitializeComponent();
        }

        double pixelsPerUnit = 1;

        double unitsToPixels(double x) { return (x - Minimum) * pixelsPerUnit; }
        double pixelsToUnits(double x) { return (x / pixelsPerUnit) + Minimum; }

        int SanitiseParamValue(double x)
        {
            int ix = (int)Math.Round(x);
            if (ix < Minimum) ix = Minimum;
            if (ix > Maximum) ix = Maximum;
            return ix;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point pos = e.GetPosition(Canvas);
                double p;
                if (Orientation == Orientation.Horizontal)
                    p = pos.X - ThumbWidth * 0.5;
                else
                    p = Canvas.ActualHeight - pos.Y - ThumbWidth * 0.5;

                Value = SanitiseParamValue(pixelsToUnits(p));
            }
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Canvas_MouseMove(sender, e);
        }

        private void UpdateEverything()
        {
            if (Orientation == Orientation.Horizontal)
            {
                pixelsPerUnit = (Canvas.ActualWidth - ThumbWidth) / (Maximum - Minimum);

                ThumbRect.Width = ThumbWidth;
                ThumbRect.Height = Canvas.ActualHeight;
                Canvas.SetLeft(ThumbRect, 0);
                Canvas.SetTop(ThumbRect, 0);

                BarRect.Height = Canvas.ActualHeight;
                Canvas.SetLeft(BarRect, 0);
                Canvas.SetTop(BarRect, 0);
            }
            else
            {
                pixelsPerUnit = (Canvas.ActualHeight - ThumbWidth) / (Maximum - Minimum);

                ThumbRect.Height = ThumbWidth;
                ThumbRect.Width = Canvas.ActualWidth;
                Canvas.SetLeft(ThumbRect, 0);
                Canvas.SetTop(ThumbRect, double.NaN);

                BarRect.Width = Canvas.ActualWidth;
                Canvas.SetLeft(BarRect, 0);
                Canvas.SetTop(BarRect, double.NaN);
                Canvas.SetBottom(BarRect, 0);
            }

            if (double.IsNaN(pixelsPerUnit) || double.IsInfinity(pixelsPerUnit) || pixelsPerUnit <= 0)
                pixelsPerUnit = 1;

            ValueChanged();
        }

        private void ValueChanged()
        {
            if (Orientation == Orientation.Horizontal)
            {
                Canvas.SetLeft(ThumbRect, unitsToPixels(Value));
                BarRect.Width = Math.Max(1, unitsToPixels(Value));
            }
            else
            {
                Canvas.SetBottom(ThumbRect, unitsToPixels(Value));
                BarRect.Height = Math.Max(1, unitsToPixels(Value));
            }
        }

        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateEverything();
        }

    }
}
