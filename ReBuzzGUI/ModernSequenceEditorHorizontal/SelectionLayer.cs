using BuzzGUI.Common;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace WDE.ModernSequenceEditorHorizontal
{
    internal class SelectionLayer : Canvas, INotifyPropertyChanged
    {
        Point anchor = new Point(-1, -1);
        Point point;
        Rectangle selRect;

        bool selecting;
        public bool Selecting
        {
            get { return selecting; }
            set
            {
                selecting = value;
                PropertyChanged.Raise(this, "Selecting");
            }
        }

        public bool SelectionNotEmpty
        {
            get
            {
                return point.X >= 0;
            }
        }

        public SelectionLayer()
        {

        }

        public void BeginSelect(Point p)
        {
            if (selRect == null) selRect = new Rectangle() { Style = TryFindResource("SelectionRectangleStyle") as Style };

            anchor = p;
            point = new Point(-1, -1);

            selRect.Width = 0;
            selRect.Height = 0;
            Children.Clear();
            Children.Add(selRect);

            Selecting = true;
        }

        public void EndSelect()
        {
            Selecting = false;
        }


        public void UpdateSelect(Point p)
        {
            if (!Selecting)
                BeginSelect(p);

            point = p;
            UpdateVisual();
        }

        public void UpdateVisual()
        {
            if (!Selecting)
                return;

            var r = Rect;
            selRect.Width = r.Width * SequenceEditor.ViewSettings.TickWidth + 1; 
            selRect.Height = r.Height * SequenceEditor.ViewSettings.TrackHeight + 1;
            SetLeft(selRect, r.Left * SequenceEditor.ViewSettings.TickWidth);
            SetTop(selRect, r.Top * SequenceEditor.ViewSettings.TrackHeight);
        }

        public void KillSelection()
        {
            if (!Selecting)
                return;

            Children.Remove(selRect);
            point = new Point(-1, -1);
            Selecting = false;
        }

        public Rect Rect
        {
            get
            {
                int start = BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.Snap((int)Math.Min(point.X, anchor.X), int.MaxValue);
                int end = BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.Snap((int)Math.Max(point.X, anchor.X), int.MaxValue);
                end += BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.TimeSignatureList.GetBarLengthAt(end);

                return new Rect(start, Math.Min(point.Y, anchor.Y), end - start, Math.Abs(point.Y - anchor.Y) + 1);
            }
        }



        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
