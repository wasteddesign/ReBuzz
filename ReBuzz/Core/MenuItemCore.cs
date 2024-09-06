using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

namespace ReBuzz.Core
{
    internal class MenuItemCore : IMenuItem
    {
        public List<MenuItemCore> ChildrenList { get; set; }
        public IEnumerable<IMenuItem> Children { get => ChildrenList; }

        public string Text { get; set; }

        public int ID { get; set; }

        public ICommand Command { get; set; }

        public object CommandParameter { get; set; }

        public bool IsEnabled { get; set; }

        public bool IsSeparator { get; set; }

        public bool IsLabel { get; set; }

        public bool IsCheckable { get; set; }

        public bool IsChecked { get; set; }

        public bool IsDefault { get; set; }

        public bool StaysOpenOnClick { get; set; }

        public string GestureText { get; set; }

        internal MenuItemCore()
        {
            ChildrenList = new List<MenuItemCore>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
