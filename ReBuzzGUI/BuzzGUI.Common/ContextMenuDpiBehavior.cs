using BuzzGUI.Common;
using BuzzGUI.Common.Settings;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BuzzGUI.Common
{
    public static class ContextMenuDpiBehavior
    {
        public static readonly DependencyProperty AutoUpdateDpiProperty =
            DependencyProperty.RegisterAttached(
                "AutoUpdateDpi",
                typeof(bool),
                typeof(ContextMenuDpiBehavior),
                new PropertyMetadata(false, OnChanged));

        public static void SetAutoUpdateDpi(DependencyObject obj, bool value)
            => obj.SetValue(AutoUpdateDpiProperty, value);

        public static bool GetAutoUpdateDpi(DependencyObject obj)
            => (bool)obj.GetValue(AutoUpdateDpiProperty);

        private static void OnChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is ContextMenu cm)
            {
                if ((bool)e.NewValue)
                    cm.Opened += OnOpened;
                else
                    cm.Opened -= OnOpened;
            }
        }

        private static void OnOpened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu cm)
                UpdateDpi(cm);
        }

        public static void UpdateDpi(FrameworkElement frameworkElement)
        {
            if (Global.GeneralSettings.DpiScaling != DpiScalingType.System)
            {
                double scale = (double)Global.GeneralSettings.DpiScaling / 100.0;
                SetWindowDpiScaling(frameworkElement, scale);
            }
            else
            {
                frameworkElement.LayoutTransform = null;
            }
        }

        public static void SetWindowDpiScaling(FrameworkElement fwElement, double scale)
        {
            var source = PresentationSource.FromVisual(fwElement);
            if (source != null)
            {
                var matrix = source.CompositionTarget.TransformToDevice;
                double dpiX = matrix.M11;
                double dpiY = matrix.M22;
                fwElement.LayoutTransform = new ScaleTransform(scale / dpiX, scale / dpiY);
            }
        }
    }
}