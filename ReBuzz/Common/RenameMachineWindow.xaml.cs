using BuzzGUI.Common;
using System.ComponentModel;
using System.Windows;

namespace ReBuzz.Common
{
    public partial class RenameMachineWindow : Window, INotifyPropertyChanged
    {
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

        public RenameMachineWindow(string name)
        {
            this.oldName = name;
            DataContext = this;
            InitializeComponent();
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
    }
}
