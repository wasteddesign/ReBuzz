//using Wpf.Controls;
using BuzzGUI.Common;
using BuzzGUI.FileBrowser.SplitButtonControl;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace BuzzGUI.FileBrowser
{
    /// <summary>
    /// Interaction logic for PathControl.xaml
    /// </summary>
    public partial class PathControl : UserControl
    {
        public PathControl()
        {
            InitializeComponent();

            border.MouseDown += (sender, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    if (ActivateTextBox != null)
                        ActivateTextBox();
                }
            };
        }

        string path;
        public string Path
        {
            set
            {
                path = value != null ? value : "";
                CreateButtons();
            }
        }

        public event Action<string> NavigateTo;
        public event Action ActivateTextBox;

        void InitButton(SplitButton sb, string path)
        {
            sb.Click += (sender, e) =>
            {
                if (NavigateTo != null) NavigateTo((sender as SplitButton).Tag as string);
            };

            sb.CreateMenu = () =>
            {
                sb.Items.Clear();
                foreach (var _x in FSItemVM.GetItemsFromPath(path != "" ? new DirectoryInfo(path) : null, null, false))
                {
                    var x = _x;
                    x.LoadIcon();
                    var mi = new MenuItem() { Header = x.Name, Icon = new Image() { Source = x.Icon } };
                    mi.Click += (sender, e) => { if (NavigateTo != null) NavigateTo(x.FullPath); };
                    sb.Items.Add(mi);
                }

                sb.Placement = PlacementMode.Mouse;
            };
        }

        void CreateButtons()
        {
            sp.Children.Clear();

            var icon = Win32.LoadIconAsImageSource(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            sp.Children.Add(new Image() { Source = icon, Margin = new Thickness(4, 0, 0, 0) });

            var sb = new SplitButton() { Content = "Computer", Tag = "" };
            InitButton(sb, "");
            sp.Children.Add(sb);

            var entries = path.Split(new char[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            string p = "";
            foreach (var x in entries)
            {
                p += x + "\\";
                sb = new SplitButton() { Content = x.Replace("_", "__"), Tag = p };
                InitButton(sb, p);
                sp.Children.Add(sb);
            }

        }

    }
}
