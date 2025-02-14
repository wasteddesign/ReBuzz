using System.Windows;
using System.Windows.Media;

namespace Pianoroll.GUI
{
    public static class Parallax
    {
        public static readonly DependencyProperty ScrollSpeedProperty = DependencyProperty.RegisterAttached(
          "ScrollSpeed",
          typeof(double),
          typeof(Parallax),
            new FrameworkPropertyMetadata(0.0, new PropertyChangedCallback(Parallax.ScrollSpeedChanged))
        );

        public static void SetScrollSpeed(ImageBrush element, double value) { element.SetValue(ScrollSpeedProperty, value); }
        public static double GetScrollSpeed(ImageBrush element) { return (double)element.GetValue(ScrollSpeedProperty); }

        static void ScrollSpeedChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
        }


    }
}
