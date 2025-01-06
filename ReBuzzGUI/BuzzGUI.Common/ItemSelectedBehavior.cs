using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace BuzzGUI.Common
{
    public static class ItemSelectedBehavior
    {
        public static DependencyProperty ItemSelectedProperty =
                    DependencyProperty.RegisterAttached("ItemSelected",
                        typeof(ICommand),
                        typeof(ItemSelectedBehavior),
                        new FrameworkPropertyMetadata(null, new PropertyChangedCallback(ItemSelectedBehavior.ItemSelectedChanged)));

        public static void SetItemSelected(DependencyObject target, ICommand value)
        {
            target.SetValue(ItemSelectedBehavior.ItemSelectedProperty, value);
        }

        private static void ItemSelectedChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            Selector element = target as Selector;

            if (element != null)
            {
                // If we're putting in a new command and there wasn't one already
                // hook the event
                if ((e.NewValue != null) && (e.OldValue == null))
                {
                    element.SelectionChanged += element_SelectionChanged;

                    if (element is ComboBox)
                    {
                        ComboBox cb = element as ComboBox;
                        cb.DropDownOpened += new EventHandler(cb_DropDownOpened);
                        cb.DropDownClosed += new EventHandler(cb_DropDownClosed);
                    }
                }
                // If we're clearing the command and it wasn't already null
                // unhook the event
                else if ((e.NewValue == null) && (e.OldValue != null))
                {
                    element.SelectionChanged -= element_SelectionChanged;

                    if (element is ComboBox)
                    {
                        ComboBox cb = element as ComboBox;
                        cb.DropDownOpened -= new EventHandler(cb_DropDownOpened);
                        cb.DropDownClosed -= new EventHandler(cb_DropDownClosed);
                    }

                }
            }
        }

        private static void element_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Selector element = sender as Selector;
            if (element != null)
            {
                if (e.RemovedItems.Count > 0)   // hack to prevent execution when combobox is created
                {
                    ICommand command = (ICommand)element.GetValue(ItemSelectedBehavior.ItemSelectedProperty);
                    command.Execute(element.SelectedItem);
                }
            }
        }

        static string oldText;  // assuming only one dropdown can be open at any given time

        static void cb_DropDownOpened(object sender, EventArgs e)
        {
            ComboBox cb = sender as ComboBox;
            oldText = cb.Text;
        }

        static void cb_DropDownClosed(object sender, EventArgs e)
        {
            ComboBox cb = sender as ComboBox;
            if (oldText == cb.Text)
            {
                // execute command when the previously selected item is selected again using the dropdown menu
                ICommand command = (ICommand)cb.GetValue(ItemSelectedBehavior.ItemSelectedProperty);
                command.Execute(cb.SelectedItem);
            }
        }

    }
}
