using BuzzGUI.Common;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BuzzGUI.MachineView
{
    /// <summary>
    /// Interaction logic for QuickNewMachineWindow.xaml
    /// </summary>
    public partial class QuickNewMachineWindow : Window
    {
        readonly MachineView view;
        public MachineList MachineList { get; private set; }

        public MachineListItemVM SelectedItem;

        public ICommand SelectCommand { get; private set; }

        readonly char firstChar;

        public QuickNewMachineWindow(MachineView view, char firstch)
        {
            this.view = view;
            MachineList = new MachineList(view);
            DataContext = MachineList;
            InitializeComponent();

            firstChar = firstch;
        }

        T EnforceInstance<T>(string partName) where T : FrameworkElement, new()
        {
            T element = GetTemplateChild(partName) as T;
            if (element == null) element = FindName(partName) as T;
            return element;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            //			var textBox = FindName("PART_TextBox") as TextBox;
            //			var listBox = FindName("PART_ListBox") as ListBox;
            var textBox = EnforceInstance<TextBox>("PART_TextBox");
            var listBox = EnforceInstance<ListBox>("PART_ListBox");

            Point p = Win32Mouse.GetScreenPosition();
            p.X /= WPFExtensions.PixelsPerDip;
            p.Y /= WPFExtensions.PixelsPerDip;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = p.X - Width / 2;
            Top = p.Y;

            this.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    this.DialogResult = false;
                    Close();
                }
                else if (e.Key == Key.Down)
                {
                    if (listBox.SelectedIndex == -1)
                        listBox.SelectedIndex = 0;
                    else
                    {
                        if (listBox.SelectedIndex < listBox.Items.Count - 1)
                        {
                            listBox.SelectedIndex++;
                            listBox.ScrollIntoView(listBox.SelectedItem);
                        }
                    }
                }
                else if (e.Key == Key.Up)
                {
                    if (listBox.SelectedIndex != -1 && listBox.SelectedIndex > 0)
                    {
                        listBox.SelectedIndex--;
                        listBox.ScrollIntoView(listBox.SelectedItem);
                    }
                }
                else if (e.Key == Key.Return)
                {
                    SelectedItem = listBox.SelectedItem as MachineListItemVM;
                    if (SelectedItem != null)
                    {
                        DialogResult = true;
                        Close();
                    }
                }

            };

            SelectCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = id =>
                {
                    SelectedItem = id as MachineListItemVM;
                    DialogResult = true;
                    Close();
                }
            };

            textBox.TextChanged += (sender, e) =>
            {
                MachineList.Filter = textBox.Text;
                if (listBox.Items.Count > 0)
                    listBox.SelectedIndex = 0;
            };

            textBox.Text = firstChar.ToString();
            textBox.Select(textBox.Text.Length, 0);
            textBox.Focus();

        }
    }
}
