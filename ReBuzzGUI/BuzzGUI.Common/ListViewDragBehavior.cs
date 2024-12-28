using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BuzzGUI.Common
{
    public static class ListViewDragBehavior
    {
        public static DependencyProperty IsSourceProperty = DependencyProperty.RegisterAttached("IsSource", typeof(bool), typeof(ListViewDragBehavior), new FrameworkPropertyMetadata(false, new PropertyChangedCallback(ListViewDragBehavior.IsSourceChanged)));
        public static DependencyProperty IsItemSourceProperty = DependencyProperty.RegisterAttached("IsItemSource", typeof(bool), typeof(ListViewDragBehavior), new FrameworkPropertyMetadata(true, new PropertyChangedCallback(ListViewDragBehavior.IsItemSourceChanged)));

        public static void SetIsSource(DependencyObject target, bool value) { target.SetValue(ListViewDragBehavior.IsSourceProperty, value); }
        public static void SetIsItemSource(DependencyObject target, bool value) { target.SetValue(ListViewDragBehavior.IsItemSourceProperty, value); }
        public static bool GetIsItemSource(DependencyObject target) { return (bool)target.GetValue(ListViewDragBehavior.IsItemSourceProperty); }

        private static void IsSourceChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            ListView lv = target as ListView;
            if (lv == null) return;

            if ((bool)e.NewValue == true && (bool)e.OldValue == false)
            {
                lv.PreviewMouseDown += new MouseButtonEventHandler(lv_PreviewMouseDown);
                lv.PreviewMouseMove += new MouseEventHandler(lv_PreviewMouseMove);
            }
            else if ((bool)e.NewValue == false && (bool)e.OldValue == true)
            {
                lv.PreviewMouseDown -= new MouseButtonEventHandler(lv_PreviewMouseDown);
                lv.PreviewMouseMove -= new MouseEventHandler(lv_PreviewMouseMove);
            }

        }

        private static void IsItemSourceChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            ListViewItem lvi = target as ListViewItem;
        }

        static bool m_IsDown = false;
        static Point m_StartPoint;
        static ListViewItem dragItem;

        static ListViewItem GetElementFromPoint(ListView box, Point point)
        {
            object element = box.InputHitTest(point);
            while (true)
            {
                if (element == box)
                    return null;

                if (element is ListViewItem)
                    return element as ListViewItem;

                element = VisualTreeHelper.GetParent((DependencyObject)element);
            }
        }


        static void lv_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var lv = sender as ListView;

            dragItem = GetElementFromPoint(lv, e.GetPosition(lv));
            if (dragItem == null || !GetIsItemSource(dragItem)) return;

            m_StartPoint = e.GetPosition(lv);
            m_IsDown = true;

        }

        static void lv_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (dragItem == null)
                return;
            var lv = sender as ListView;

            if (m_IsDown)
            {
                if (Math.Abs(e.GetPosition(lv).X - m_StartPoint.X) >
                    SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(e.GetPosition(lv).Y - m_StartPoint.Y) >
                    SystemParameters.MinimumVerticalDragDistance)
                {
                    m_IsDown = false;
                    DragDrop.DoDragDrop(lv, dragItem.Content, DragDropEffects.All);
                }

            }

        }

    }
}
