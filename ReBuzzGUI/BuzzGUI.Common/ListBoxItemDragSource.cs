using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace BuzzGUI.Common
{
    public class ListBoxItemDragSource
    {
        readonly ListBox listBox;

        public ListBoxItemDragSource(ListBox lb)
        {
            this.listBox = lb;
            listBox.PreviewMouseDown += new System.Windows.Input.MouseButtonEventHandler(listBox_PreviewMouseDown);
            listBox.PreviewMouseMove += new System.Windows.Input.MouseEventHandler(listBox_PreviewMouseMove);
        }



        bool m_IsDown = false;
        Point m_StartPoint;
        ListBoxItem dragItem;

        private ListBoxItem GetElementFromPoint(ListBox box, Point point)
        {
            object element = box.InputHitTest(point);
            while (true)
            {
                if (element == box)
                    return null;

                if (element is ListBoxItem)
                    return element as ListBoxItem;

                element = VisualTreeHelper.GetParent((DependencyObject)element);
            }
        }

        void listBox_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            dragItem = GetElementFromPoint(listBox, e.GetPosition(listBox));
            if (dragItem == null)
                return;

            m_StartPoint = e.GetPosition(listBox);
            m_IsDown = true;

        }

        void listBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (m_IsDown)
            {
                if (Math.Abs(e.GetPosition(listBox).X - m_StartPoint.X) >
                    SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(e.GetPosition(listBox).Y - m_StartPoint.Y) >
                    SystemParameters.MinimumVerticalDragDistance)
                {
                    m_IsDown = false;
                    DragDrop.DoDragDrop(listBox, dragItem.Content, DragDropEffects.All);
                }

            }
        }

    }
}
