using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace BuzzGUI.ParameterWindow
{
    public class ParameterWindow : Window
    {
        #region Properties

        public TabItem SelectedTabItem
        {
            get { return (TabItem)GetValue(SelectedTabItemProperty); }
            set { SetValue(SelectedTabItemProperty, value); }
        }
        public static readonly DependencyProperty SelectedTabItemProperty =
            DependencyProperty.Register("SelectedTabItem", typeof(TabItem), typeof(ParameterWindow),
                new UIPropertyMetadata());

        public double MaxAutoWidth
        {
            get { return (double)GetValue(MaxAutoWidthProperty); }
            set { SetValue(MaxAutoWidthProperty, value); }
        }
        public static readonly DependencyProperty MaxAutoWidthProperty =
            DependencyProperty.Register("MaxAutoWidth", typeof(double), typeof(ParameterWindow),
                new UIPropertyMetadata(double.PositiveInfinity));

        public double MaxAutoHeight
        {
            get { return (double)GetValue(MaxAutoHeightProperty); }
            set { SetValue(MaxAutoHeightProperty, value); }
        }
        public static readonly DependencyProperty MaxAutoHeightProperty =
            DependencyProperty.Register("MaxAutoHeight", typeof(double), typeof(ParameterWindow),
                new UIPropertyMetadata(double.PositiveInfinity));

        public static readonly DependencyProperty TabSizeModeProperty =
            DependencyProperty.RegisterAttached("TabSizeMode", typeof(SizeToContent), typeof(ParameterWindow),
                new UIPropertyMetadata(SizeToContent.Height));
        public static SizeToContent GetTabSizeMode(DependencyObject obj) { return (SizeToContent)obj.GetValue(TabSizeModeProperty); }
        public static void SetTabSizeMode(DependencyObject obj, SizeToContent value) { obj.SetValue(TabSizeModeProperty, value); }

        #endregion

        protected override Size MeasureOverride(Size availableSize)
        {
            bool stw = true, sth = true;

            if (SelectedTabItem != null)
            {
                SizeToContent stc = GetTabSizeMode(SelectedTabItem);
                stw = (stc == SizeToContent.Width || stc == SizeToContent.WidthAndHeight);
                sth = (stc == SizeToContent.Height || stc == SizeToContent.WidthAndHeight);
            }

            if (!stw) availableSize.Width = Width;
            if (!sth) availableSize.Height = Height;

            double maxw = MaxAutoWidth;
            if (maxw <= 1.0) maxw *= SystemParameters.PrimaryScreenWidth;
            double maxh = MaxAutoHeight;
            if (maxh <= 1.0) maxh *= SystemParameters.PrimaryScreenHeight;

            if (availableSize.Width > maxw) availableSize.Width = maxw;
            if (availableSize.Height > maxh) availableSize.Height = maxh;

            Size s = base.MeasureOverride(availableSize);
            System.Diagnostics.Trace.WriteLine(string.Format("Measure: {0}", s));

            if (!stw) s.Width = Width;
            if (!sth) s.Height = Height;

            if (s.Width > maxw) s.Width = maxw;
            if (s.Height > maxh) s.Height = maxh;
            
            return s;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            Size s = base.ArrangeOverride(arrangeBounds);
            System.Diagnostics.Trace.WriteLine(string.Format("Arrange: {0}", s));
            return s;
        }

    }
}
