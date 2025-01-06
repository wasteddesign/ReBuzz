using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace WDE.ModernPatternEditor
{
    class SelectionLayer : Canvas
    {
        Rectangle selRect;

        public Rect Rect
        {
            set
            {
                if (selRect == null)
                {
                    selRect = new Rectangle() { Style = TryFindResource("SelectionRectangleStyle") as Style };
                    Children.Add(selRect);
                }

                if (value.IsEmpty)
                {
                    selRect.Visibility = Visibility.Collapsed;
                }
                else
                {
                    selRect.Width = value.Width;
                    selRect.Height = value.Height;
                    Canvas.SetLeft(selRect, value.Left);
                    Canvas.SetTop(selRect, value.Top);
                    selRect.Visibility = Visibility.Visible;
                }
            }

        }


    }
}
