using System;
using System.Windows;
using System.Windows.Controls;

namespace BuzzGUI.ParameterWindow
{
    /// <summary>
    /// Interaction logic for RangeControl.xaml
    /// </summary>
    public partial class RangeControl : UserControl
    {
        #region Properties

        //--------------------------------------------------------------------------

        public int Minimum
        {
            get { return (int)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(int), typeof(RangeControl),
                new UIPropertyMetadata(0, (d, e) => (d as RangeControl).UpdateEverything()));

        //--------------------------------------------------------------------------

        public int Maximum
        {
            get { return (int)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(int), typeof(RangeControl),
                new UIPropertyMetadata(1, (d, e) => (d as RangeControl).UpdateEverything()));

        //--------------------------------------------------------------------------

        public int LowValue
        {
            get { return (int)GetValue(LowValueProperty); }
            set { SetValue(LowValueProperty, value); }
        }
        public static readonly DependencyProperty LowValueProperty =
            DependencyProperty.Register("LowValue", typeof(int), typeof(RangeControl),
                new UIPropertyMetadata(-1, (d, e) => (d as RangeControl).LowValueChanged()));

        //--------------------------------------------------------------------------

        public int HighValue
        {
            get { return (int)GetValue(HighValueProperty); }
            set { SetValue(HighValueProperty, value); }
        }
        public static readonly DependencyProperty HighValueProperty =
            DependencyProperty.Register("HighValue", typeof(int), typeof(RangeControl),
                new UIPropertyMetadata(-1, (d, e) => (d as RangeControl).HighValueChanged()));

        //--------------------------------------------------------------------------

        public int CurrentValue
        {
            get { return (int)GetValue(CurrentValueProperty); }
            set { SetValue(CurrentValueProperty, value); }
        }
        public static readonly DependencyProperty CurrentValueProperty =
            DependencyProperty.Register("CurrentValue", typeof(int), typeof(RangeControl),
                new UIPropertyMetadata(0, (d, e) => (d as RangeControl).CurrentValueChanged()));

        //--------------------------------------------------------------------------

        public string LowLabel
        {
            get { return (string)GetValue(LowLabelProperty); }
            set { SetValue(LowLabelProperty, value); }
        }
        public static readonly DependencyProperty LowLabelProperty =
            DependencyProperty.Register("LowLabel", typeof(string), typeof(RangeControl),
                new UIPropertyMetadata(""));

        //--------------------------------------------------------------------------

        public string HighLabel
        {
            get { return (string)GetValue(HighLabelProperty); }
            set { SetValue(HighLabelProperty, value); }
        }
        public static readonly DependencyProperty HighLabelProperty =
            DependencyProperty.Register("HighLabel", typeof(string), typeof(RangeControl),
                new UIPropertyMetadata(""));

        //--------------------------------------------------------------------------

        public Style SelectionStyle
        {
            get { return (Style)GetValue(SelectionStyleProperty); }
            set { SetValue(SelectionStyleProperty, value); }
        }
        public static readonly DependencyProperty SelectionStyleProperty =
            DependencyProperty.Register("SelectionStyle", typeof(Style), typeof(RangeControl),
                new UIPropertyMetadata());

        //--------------------------------------------------------------------------

        public int ThumbWidth
        {
            get { return (int)GetValue(ThumbWidthProperty); }
            set { SetValue(ThumbWidthProperty, value); }
        }
        public static readonly DependencyProperty ThumbWidthProperty =
            DependencyProperty.Register("ThumbWidth", typeof(int), typeof(RangeControl),
                new UIPropertyMetadata(10, (d, e) => (d as RangeControl).UpdateEverything()));

        //--------------------------------------------------------------------------

        public ControlTemplate CurrentThumbTemplate
        {
            get { return (ControlTemplate)GetValue(CurrentThumbTemplateProperty); }
            set { SetValue(CurrentThumbTemplateProperty, value); }
        }
        public static readonly DependencyProperty CurrentThumbTemplateProperty =
            DependencyProperty.Register("CurrentThumbTemplate", typeof(ControlTemplate), typeof(RangeControl),
                new UIPropertyMetadata(null));

        //--------------------------------------------------------------------------

        public ControlTemplate RangeThumbTemplate
        {
            get { return (ControlTemplate)GetValue(RangeThumbTemplateProperty); }
            set { SetValue(RangeThumbTemplateProperty, value); }
        }
        public static readonly DependencyProperty RangeThumbTemplateProperty =
            DependencyProperty.Register("RangeThumbTemplate", typeof(ControlTemplate), typeof(RangeControl),
                new UIPropertyMetadata(null));

        //--------------------------------------------------------------------------

        public Style TextStyle
        {
            get { return (Style)GetValue(TextStyleProperty); }
            set { SetValue(TextStyleProperty, value); }
        }
        public static readonly DependencyProperty TextStyleProperty =
            DependencyProperty.Register("TextStyle", typeof(Style), typeof(RangeControl),
                new UIPropertyMetadata());

        //--------------------------------------------------------------------------

        #endregion

        public RangeControl()
        {
            pixelsPerUnit = 1;

            InitializeComponent();
        }

        double pixelsPerUnit;

        double unitsToPixels(double x) { return (x - Minimum) * pixelsPerUnit; }
        double pixelsToUnits(double x) { return (x / pixelsPerUnit) + Minimum; }

        int SanitiseParamValue(double x)
        {
            int ix = (int)Math.Round(x);
            if (ix < Minimum) ix = Minimum;
            if (ix > Maximum) ix = Maximum;
            return ix;
        }

        private void UpdateEverything()
        {
            pixelsPerUnit = (Canvas.ActualWidth - 2 * ThumbWidth) / (Maximum - Minimum);
            if (double.IsNaN(pixelsPerUnit) || double.IsInfinity(pixelsPerUnit) || pixelsPerUnit <= 0)
                pixelsPerUnit = 1;

            LowValueChanged();
            HighValueChanged();
            CurrentValueChanged();
        }

        private void root_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateEverything();
        }

        private void UpdateSelectionRect()
        {
            Canvas.SetLeft(SelectionRect, unitsToPixels(LowValue) + ThumbWidth * 0.5);
            SelectionRect.Width = Math.Max(1, (HighValue - LowValue) * pixelsPerUnit) + ThumbWidth;
        }

        private void UpdateLabelPositions()
        {
            double desl = LowLabelText.DesiredSize.Width;
            double desh = HighLabelText.DesiredSize.Width;
            double pl = unitsToPixels(LowValue);
            double ph = unitsToPixels(HighValue);
            TextAlignment al = TextAlignment.Left, ah = TextAlignment.Right;

            if (desl + desh > ph - pl - ThumbWidth)
            {
                if (desl <= pl)
                    al = TextAlignment.Right;
                if (desh <= Canvas.ActualWidth - (ph + ThumbWidth))
                    ah = TextAlignment.Left;
            }

            LowLabelText.TextAlignment = al;
            if (al == TextAlignment.Left)
            {
                Canvas.SetLeft(LowLabelText, pl + ThumbWidth);
            }
            else
            {
                Canvas.SetLeft(LowLabelText, double.NaN);
                Canvas.SetRight(LowLabelText, Canvas.ActualWidth - pl);
            }

            HighLabelText.TextAlignment = ah;
            if (ah == TextAlignment.Left)
            {
                Canvas.SetLeft(HighLabelText, ph + 2 * ThumbWidth);
            }
            else
            {
                Canvas.SetLeft(HighLabelText, double.NaN);
                Canvas.SetRight(HighLabelText, Canvas.ActualWidth - (ph + ThumbWidth));
            }
        }

        private void LowValueChanged()
        {
            Canvas.SetLeft(LowThumb, unitsToPixels(LowValue));
            if (HighValue < LowValue && HighValue != -1) HighValue = LowValue;
            UpdateSelectionRect();
            UpdateLabelPositions();
        }

        private void HighValueChanged()
        {
            Canvas.SetLeft(HighThumb, unitsToPixels(HighValue) + ThumbWidth);
            if (HighValue < LowValue && LowValue != -1) LowValue = HighValue;
            UpdateSelectionRect();
            UpdateLabelPositions();
        }

        private void CurrentValueChanged()
        {
            Canvas.SetLeft(CurrentThumb, unitsToPixels(CurrentValue) + ThumbWidth * 0.5);
        }

        private void CurrentThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            // Note: error in MSDN documentation
            // e.HorizontalChange isn't the distance since the last DragDelta message, it's the distance
            // from the hotspot on the thumb. Since we don't move the thumb exactly with the mouse,
            // the two are not necessarily the same.

            int newvalue = SanitiseParamValue(CurrentValue + e.HorizontalChange / pixelsPerUnit);
            if (newvalue != CurrentValue) CurrentValue = newvalue;
        }

        private void LowThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            int newvalue = SanitiseParamValue(LowValue + e.HorizontalChange / pixelsPerUnit);
            if (newvalue != LowValue) LowValue = newvalue;
        }

        private void HighThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            int newvalue = SanitiseParamValue(HighValue + e.HorizontalChange / pixelsPerUnit);
            if (newvalue != HighValue) HighValue = newvalue;
        }

    }
}
