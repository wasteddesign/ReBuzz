using BuzzGUI.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace BuzzGUI.FileBrowser
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class FileBrowser : UserControl, INotifyPropertyChanged
    {

        public static readonly DependencyProperty ExtensionFilterProperty = DependencyProperty.Register("ExtensionFilter", typeof(List<string>), typeof(FileBrowser), new FrameworkPropertyMetadata(new List<string>(), new PropertyChangedCallback(OnExtensionFilterChanged)));

        public List<string> ExtensionFilter
        {
            get { return (List<string>)GetValue(FileBrowser.ExtensionFilterProperty); }
            set { SetValue(FileBrowser.ExtensionFilterProperty, value); }
        }

        static void OnExtensionFilterChanged(DependencyObject controlInstance, DependencyPropertyChangedEventArgs args)
        {
            var x = (FileBrowser)controlInstance;
        }

        public static readonly DependencyProperty ItemDoubleClickCommandProperty = DependencyProperty.Register("ItemDoubleClickCommand", typeof(ICommand), typeof(FileBrowser), new FrameworkPropertyMetadata(null));
        public ICommand ItemDoubleClickCommand { get { return (ICommand)GetValue(FileBrowser.ItemDoubleClickCommandProperty); } set { SetValue(FileBrowser.ItemDoubleClickCommandProperty, value); } }

        public static readonly DependencyProperty ItemKeyDownCommandProperty = DependencyProperty.Register("ItemKeyDownCommand", typeof(ICommand), typeof(FileBrowser), new FrameworkPropertyMetadata(null));
        public ICommand ItemKeyDownCommand { get { return (ICommand)GetValue(FileBrowser.ItemKeyDownCommandProperty); } set { SetValue(FileBrowser.ItemKeyDownCommandProperty, value); } }

        public ListView ListView { get { return listView; } }


        ObservableCollection<FSItemVM> items;
        public ObservableCollection<FSItemVM> Items
        {
            get { return items; }
            set
            {
                items = value;
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Items"));
                if (listView.Items.Count > 0)
                {
                    listView.ScrollIntoView(listView.Items[0]);
                    listView.SelectedIndex = 0;
                    var lvi = (ListViewItem)listView.ItemContainerGenerator.ContainerFromItem(listView.Items[0]);
                    if (lvi != null) lvi.Focus();
                }
            }
        }

        string currentPath = "";
        public string CurrentPath
        {
            get
            {
                return currentPath;
            }
            set
            {
                currentPath = value;
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("CurrentPath"));
                pathControl.Path = currentPath;
            }
        }

        readonly List<FSItemVM> resultList = new List<FSItemVM>();

        void CancelSearch()
        {
            FSItemVM.CancelSearch();
            lock (resultList)
            {
                resultList.Clear();
            }

        }

        void NavigateTo(string path)
        {
            if (path == null) path = "";
            Mouse.OverrideCursor = Cursors.Wait;
            CancelSearch();
            searchBox.Text = "";
            searchText.Visibility = Visibility.Visible;

            if (path == "" || (Directory.Exists(path) && System.IO.Path.IsPathRooted(path)))
            {
                Items = FSItemVM.GetItemsFromPath(path != "" ? new DirectoryInfo(path) : null, ExtensionFilter, true);
                CurrentPath = path;
            }
            SetValue(ListViewDragBehavior.IsSourceProperty, true);
            Mouse.OverrideCursor = null;
        }

        public void NavigateToParent()
        {
            if (currentPath.Length > 0)
                NavigateTo(System.IO.Path.GetDirectoryName(CurrentPath));
        }



        public FileBrowser()
        {
            InitializeComponent();
            grid.DataContext = this;

            Global.GeneralSettings.PropertyChanged += new PropertyChangedEventHandler(GeneralSettings_PropertyChanged);

            this.Loaded += (sender, e) =>
            {
                SetSortColumn((listView.View as GridView).Columns[0].Header as GridViewColumnHeader);
                NavigateTo("");
            };

            listView.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                var item = (sender as ListView).SelectedItem;
                if (item != null)
                {
                    if (e.ClickCount == 1)
                    {
                        var fileItem = (item as FSItemVM);
                        if (!fileItem.IsFile)
                            SetValue(ListViewDragBehavior.IsItemSourceProperty, false);

                    }
                    else if (e.ClickCount == 2)
                    {
                        this.OnDoubleClickItem(item, e);
                    }
                }
            };

            listView.PreviewKeyDown += (sender, e) =>
            {
                OnPreviewKeyDown(sender, e);

            };

            textBox.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Return)
                {
                    NavigateTo(textBox.Text);
                    listView.Focus();
                }
                else if (e.Key == Key.Escape)
                {
                    listView.Focus();
                }
            };

            textBox.LostFocus += (sender, e) =>
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("CurrentPath"));
                textBox.Visibility = Visibility.Collapsed;
                pathControl.Visibility = Visibility.Visible;
            };

            pathControl.NavigateTo += (path) =>
            {
                NavigateTo(path);
            };

            pathControl.ActivateTextBox += () =>
            {
                pathControl.Visibility = Visibility.Collapsed;
                textBox.Visibility = Visibility.Visible;
                textBox.SelectAll();
                textBox.Focus();

            };

            searchBox.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Return)
                {
                    listView.Focus();
                    CancelSearch();

                    if (searchBox.Text.Length > 0)
                    {
                        var si = Items;
                        Items = new ObservableCollection<FSItemVM>();

                        FSItemVM.Search(si.Where(x => x.IsDirectory), searchBox.Text, ExtensionFilter,
                            (item) =>
                            {
                                lock (resultList)
                                {
                                    resultList.Add(item);
                                }
                            });
                    }

                }
                else if (e.Key == Key.Escape)
                {
                    searchBox.Text = null;
                    FSItemVM.CancelSearch();
                    listView.Focus();
                }
            };

            var dt = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromSeconds(1.0) };
            dt.Tick += (sender, e) =>
            {
                lock (resultList)
                {
                    foreach (var x in resultList)
                        items.Add(x);

                    resultList.Clear();
                }
            };
            dt.Start();

            this.Unloaded += (sender, e) =>
            {
                Global.GeneralSettings.PropertyChanged -= new PropertyChangedEventHandler(GeneralSettings_PropertyChanged);
                dt.Stop();
            };

            searchBox.GotFocus += (sender, e) =>
            {
                searchText.Visibility = Visibility.Collapsed;
            };

            searchBox.LostFocus += (sender, e) =>
            {
                if (searchBox.Text.Length == 0)
                    searchText.Visibility = Visibility.Visible;
            };


            this.KeyDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
                {
                    searchBox.Focus();
                    e.Handled = true;
                }
            };

            listView.MouseDown += (sender, e) =>
            {
                // click on background
                if (e.ChangedButton == MouseButton.Left)
                {
                    listView.Focus();
                    NavigateToParent();
                }
            };

        }

        void GeneralSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "WPFIdealFontMetrics":
                    PropertyChanged.Raise(this, "TextFormattingMode");
                    break;
            }
        }

        public void OnDoubleClickItem(object sender, RoutedEventArgs e)
        {
            var x = sender as FSItemVM;
            if (x.IsDirectory)
            {
                NavigateTo(x.FullPath);
            }
            else
            {
                if (ItemDoubleClickCommand != null && ItemDoubleClickCommand.CanExecute(x))
                    ItemDoubleClickCommand.Execute(x);
            }
        }

        public void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // var x = (sender as ListViewItem).DataContext as FSItemVM;
            var x = (sender as ListView).SelectedItem as FSItemVM;

            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                if (e.Key == Key.Enter || e.Key == Key.Right)
                {
                    if (x == null)
                    {
                        e.Handled = true;
                    }
                    else if (x.IsDirectory)
                    {
                        NavigateTo(x.FullPath);
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Left)
                {
                    NavigateToParent();
                    e.Handled = true;
                }

                if (!e.Handled)
                {
                    if (e.Key == Key.Space)
                    {
                        //listView.SelectedItem = listView.Items[listView.SelectedIndex + 1];
                        //e.Handled = true;
                    }


                    if (x != null && ItemKeyDownCommand != null && ItemKeyDownCommand.CanExecute(x))
                    {
                        ItemKeyDownCommand.Execute(Tuple.Create(x, e));
                    }

                }
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Sorting
        SortAdorner sortAdorner;
        GridViewColumnHeader sortColumn;

        private void SortClick(object sender, RoutedEventArgs e)
        {
            SetSortColumn(sender as GridViewColumnHeader);
        }

        void SetSortColumn(GridViewColumnHeader column)
        {
            var field = column.Tag as string;

            if (sortColumn != null)
            {
                AdornerLayer.GetAdornerLayer(sortColumn).Remove(sortAdorner);
                listView.Items.SortDescriptions.Clear();
            }

            var newDir = ListSortDirection.Ascending;
            if (sortColumn == column && sortAdorner.Direction == newDir)
                newDir = ListSortDirection.Descending;

            sortColumn = column;
            sortAdorner = new SortAdorner(sortColumn, newDir);
            AdornerLayer.GetAdornerLayer(sortColumn).Add(sortAdorner);
            listView.Items.SortDescriptions.Add(new SortDescription(field, newDir));

        }

        public class SortAdorner : Adorner
        {
            private readonly static Geometry _AscGeometry = Geometry.Parse("M 0,5 L 10,5 L 5,0 Z");
            private readonly static Geometry _DescGeometry = Geometry.Parse("M 0,0 L 10,0 L 5,5 Z");

            public ListSortDirection Direction { get; private set; }

            public SortAdorner(UIElement element, ListSortDirection dir) : base(element) { Direction = dir; }

            protected override void OnRender(DrawingContext drawingContext)
            {
                base.OnRender(drawingContext);
                if (AdornedElement.RenderSize.Width < 20) return;
                drawingContext.PushTransform(new TranslateTransform(AdornedElement.RenderSize.Width - 15, (AdornedElement.RenderSize.Height - 5) / 2));
                drawingContext.DrawGeometry(Brushes.Black, null, Direction == ListSortDirection.Ascending ? _AscGeometry : _DescGeometry);
                drawingContext.Pop();
            }
        }
        #endregion

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (currentPath != "")
            {
                System.Diagnostics.Process.Start(currentPath);
            }
        }

        public TextFormattingMode TextFormattingMode { get { return Global.GeneralSettings.WPFIdealFontMetrics ? TextFormattingMode.Ideal : TextFormattingMode.Display; } }

    }
}
