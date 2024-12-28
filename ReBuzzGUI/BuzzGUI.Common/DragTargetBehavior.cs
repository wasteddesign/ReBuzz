using System;
using System.Windows;
using System.Windows.Input;

namespace BuzzGUI.Common
{
    public static class DragTargetBehavior
    {
        public static DependencyProperty CommandProperty = DependencyProperty.RegisterAttached("Command", typeof(ICommand), typeof(DragTargetBehavior), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(DragTargetBehavior.CommandChanged)));
        public static void SetCommand(DependencyObject target, ICommand value) { target.SetValue(DragTargetBehavior.CommandProperty, value); }
        public static ICommand GetCommand(DependencyObject target) { return (ICommand)target.GetValue(DragTargetBehavior.CommandProperty); }

        public static DependencyProperty ParameterProperty = DependencyProperty.RegisterAttached("Parameter", typeof(string), typeof(DragTargetBehavior), new FrameworkPropertyMetadata(null));
        public static void SetParameter(DependencyObject target, string value) { target.SetValue(DragTargetBehavior.ParameterProperty, value); }
        public static string GetParameter(DependencyObject target) { return (string)target.GetValue(DragTargetBehavior.ParameterProperty); }

        private static void CommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            var element = target as UIElement;

            if ((e.NewValue != null) && (e.OldValue == null))
            {
                element.PreviewDragEnter += new DragEventHandler(element_PreviewDragEnter);
                element.PreviewDragOver += new DragEventHandler(element_PreviewDragOver);
                element.Drop += new DragEventHandler(element_Drop);
            }
            else if ((e.NewValue == null) && (e.OldValue != null))
            {
                element.PreviewDragEnter -= new DragEventHandler(element_PreviewDragEnter);
                element.PreviewDragOver -= new DragEventHandler(element_PreviewDragOver);
                element.Drop -= new DragEventHandler(element_Drop);

            }

        }

        static void UpdateDragEffect(UIElement element, DragEventArgs e)
        {
            var cmd = GetCommand(element);

            if (cmd != null && cmd.CanExecute(Tuple.Create(e, element)))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;

            }

            e.Handled = true;
        }

        static void element_PreviewDragEnter(object sender, DragEventArgs e)
        {
            UpdateDragEffect(sender as UIElement, e);
        }

        static void element_PreviewDragOver(object sender, DragEventArgs e)
        {
            UpdateDragEffect(sender as UIElement, e);
        }

        static void element_Drop(object sender, DragEventArgs e)
        {
            var element = sender as UIElement;

            var cmd = GetCommand(element);
            if (cmd != null)
                cmd.Execute(Tuple.Create(e, element));

            e.Handled = true;
        }


    }
}
