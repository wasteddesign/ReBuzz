using BuzzGUI.Common;
using System;
using System.ComponentModel;
using System.Windows;

namespace ReBuzz.Common
{
    public partial class RenameMachineWindow : Window, INotifyPropertyChanged
    {
        private readonly bool skipInputCheck;
        private readonly string oldName;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsInputValid
        {
            set
            {
                PropertyChanged.Raise(this, "IsInputValid");
            }
            get
            {
                if (skipInputCheck)
                    return true;

                try
                {
                    var n = tbName.Text.Trim();
                    if (n.Length > 0 &&
                        oldName != n)
                    {
                        return true;
                    }
                }
                catch
                { }

                return false;
            }
        }

        public RenameMachineWindow(string title, string name, bool skipInputValidation)
        {
            this.skipInputCheck = skipInputValidation;
            this.oldName = name;
            DataContext = this;
            InitializeComponent();

            var rd = Utils.GetUserControlXAML<ResourceDictionary>("MachineView\\MVResources.xaml", Global.BuzzPath);
            this.Resources.MergedDictionaries.Add(rd);

            this.Title = title;

            tbName.Text = name;

            btOk.Click += (sender, e) =>
            {
                DialogResult = true;
                this.Close();
            };

            tbName.TextChanged += (sender, e) =>
            {
                IsInputValid = false;
            };
        }

        internal void SetStartUpLocation(int x, int y)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = x;
            Top = y;
        }
    }
}
