using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Windows.Input;

namespace BuzzGUI.Common
{
    public class MenuItemVM : IMenuItem
    {
        public string Text { get; set; }
        public int ID { get; set; }
        public ICommand Command { get; set; }
        public object CommandParameter { get; set; }
        public IEnumerable<IMenuItem> Children { get; set; }
        public bool IsSeparator { get; set; }
        public bool IsLabel { get; set; }
        public bool IsCheckable { get; set; }
        public bool IsDefault { get; set; }
        public bool StaysOpenOnClick { get; set; }
        public string GestureText { get; set; }

        bool isEnabled = true;
        public bool IsEnabled { get { return isEnabled; } set { isEnabled = value; } }

        bool isChecked;
        public bool IsChecked
        {
            get { return isChecked; }
            set
            {
                if (value != isChecked)
                {
                    isChecked = value;
                    PropertyChanged.Raise(this, "IsChecked");

                    if (checkGroup != null && isChecked)
                        checkGroup.UncheckAllExcept(this);

                }
            }
        }

        public class Group
        {
            readonly List<MenuItemVM> items = new List<MenuItemVM>();

            internal void Add(MenuItemVM i)
            {
                items.Add(i);
            }

            internal void Remove(MenuItemVM i)
            {
                items.Remove(i);
            }

            internal void UncheckAllExcept(MenuItemVM x)
            {
                foreach (var i in items)
                {
                    if (i != x) i.IsChecked = false;
                }
            }
        }

        Group checkGroup;
        public Group CheckGroup
        {
            get { return checkGroup; }
            set
            {
                if (checkGroup != null) checkGroup.Remove(this);
                checkGroup = value;
                checkGroup.Add(this);
            }
        }

        #region INotifyPropertyChanged Members

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
