using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace BuzzGUI.MachineView
{
    class SelectionLayer : Canvas
    {
        Point anchor = new Point(-1, -1);
        Rectangle selRect;
        bool selecting;

        public bool Selecting { get { return selecting; } }

        public SelectionLayer()
        {

        }

        public void BeginSelect(Point p)
        {
            if (selecting)
                return;

            if (selRect == null) selRect = new Rectangle() { Style = (Style)FindResource("SelectionRectangleStyle") };

            anchor = p;

            selRect.Width = 0;
            selRect.Height = 0;
            Children.Add(selRect);

            selecting = true;
        }

        public Rect  UpdateSelect(Point p)
        {
            if (!selecting)
                return new Rect();

            selRect.Width = Math.Abs(p.X - anchor.X);
            selRect.Height = Math.Abs(p.Y - anchor.Y);
            SetLeft(selRect, Math.Min(p.X, anchor.X));
            SetTop(selRect, Math.Min(p.Y, anchor.Y));

            return GetRect();
        }

        public void EndSelect(Point p)
        {
            if (!selecting)
                return;

            Children.Remove(selRect);
            selecting = false;
        }

        Rect GetRect()
        {
            return new Rect(GetLeft(selRect), GetTop(selRect), selRect.Width, selRect.Height);
        }


    }
}
