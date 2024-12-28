using BuzzGUI.Common.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Security.RightsManagement;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BuzzGUI.Common
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window, INotifyPropertyChanged
    {
        static readonly SortedDictionary<string, BuzzGUI.Common.Settings.Settings> settingsDictionary = new SortedDictionary<string, Settings.Settings>();

        public SettingsWindow()
        {
            this.DataContext = this;
            InitializeComponent();

            Global.GeneralSettings.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(GeneralSettings_PropertyChanged);

            this.Closed += (sender, e) =>
            {
                Global.GeneralSettings.PropertyChanged -= new System.ComponentModel.PropertyChangedEventHandler(GeneralSettings_PropertyChanged);

                foreach (var s in settingsDictionary)
                    s.Value.Save();
            };

            this.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    Close();
                }
            };

            foreach (var s in settingsDictionary)
                AddTab(s.Key, s.Value);
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

        static SettingsWindow openWindow = null;

        public static bool IsWindowVisible { get { return openWindow != null; } }
        public static void CloseWindow() { if (IsWindowVisible) openWindow.Close(); }

        public static void Show(FrameworkElement v, string activetab)
        {
            if (openWindow != null)
            {
                ActivateTab(activetab);
                openWindow.Show();
                openWindow.Activate();
                return;
            }

            SettingsWindow sw = new SettingsWindow()
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Resources = v != null ? v.Resources : null,
            };
            sw.Closed += (sender, e) =>
            {
                openWindow = null;
            };

            new WindowInteropHelper(sw).Owner = v != null ? ((HwndSource)PresentationSource.FromVisual(v)).Handle : BuzzGUI.Common.Global.MachineViewHwndSource.Handle;

            openWindow = sw;
            ActivateTab(activetab);
            sw.Show();
        }

        static void ActivateTab(string header)
        {
            var item = openWindow.tc.Items.Cast<TabItem>().FirstOrDefault(i => (string)i.Header == header);
            if (item != null) openWindow.tc.SelectedItem = item;
        }

        public static void AddSettings(string header, BuzzGUI.Common.Settings.Settings settings)
        {
            if (!settingsDictionary.ContainsKey(header))
            {
                settingsDictionary[header] = settings;

                if (openWindow != null)
                {
                    int index = 0;
                    foreach (var ditem in settingsDictionary)
                    {
                        if (ditem.Key == header)
                            break;
                        index++;
                    }

                    var selected = openWindow.tc.SelectedItem as TabItem;
                    openWindow.AddTab(header, settings, index);
                    openWindow.tc.SelectedItem = selected;
                }
            }
        }

        public static void RemoveSettings(string header)
        {
            if (settingsDictionary.ContainsKey(header))
            {
                settingsDictionary.Remove(header);

                if (openWindow != null)
                {
                    var items = openWindow.tc.Items;
                    foreach (var item in items)
                    {
                        if ((item as TabItem).Header == header)
                        {
                            openWindow.tc.Items.Remove(item);
                            break;
                        }
                    }
                }
            }
        }

        public static IEnumerable<Dictionary<string, string>> GetSettings()
        {
            return settingsDictionary.Select(sd => sd.Value.List.ToDictionary(s => sd.Key + "/" + s.Name, s => s.Value));
        }

        void AddTab(string header, BuzzGUI.Common.Settings.Settings settings, int index = -1)
        {
            var grid = new Grid() { Margin = new Thickness(8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1.0, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1.0, GridUnitType.Star) });

            int row = 0;

            foreach (var _p in settings.List)
            {
                var p = _p;
                grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

                var tb = new TextBlock() { Text = p.Name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 4), ToolTip = p.Description };
                if (p.Presets != null) tb.FontWeight = FontWeights.Bold;

                Grid.SetRow(tb, row);
                grid.Children.Add(tb);

                var cb = new ComboBox() { Margin = new Thickness(0, 0, 0, 4), ItemTemplate = Resources["ComboBoxItemTemplate"] as DataTemplate };
                Grid.SetRow(cb, row);
                Grid.SetColumn(cb, 1);
                grid.Children.Add(cb);

                foreach (var set in p.Options)
                {
                    ComboBoxItem cbi = new ComboBoxItem() { Content = set, Tag = p };
                    if (set == p.Default)
                        cbi.FontWeight = FontWeights.Bold;
                    cb.Items.Add(cbi);
                }

                Tuple<Setting, ComboBox> t = new Tuple<Setting, ComboBox>(p, cb);
                cb.SetBinding(ComboBox.SelectedItemProperty, new Binding("Value") { Source = p, Mode = BindingMode.TwoWay, Converter = SettingValueConvereter1, ConverterParameter = t });

                row++;
            }

            var sv = new ScrollViewer() { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden };
            var ti = new TabItem() { Header = header, Content = sv, Tag = settings };

            if (index == -1)
                tc.Items.Add(ti);
            else
                tc.Items.Insert(index, ti);

        }

        public SettingValueConverter SettingValueConvereter1 = new SettingValueConverter();

        public TextFormattingMode TextFormattingMode { get { return Global.GeneralSettings.WPFIdealFontMetrics ? TextFormattingMode.Ideal : TextFormattingMode.Display; } }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }

    [ValueConversion(typeof(DateTime), typeof(String))]
    public class SettingValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var t = parameter as Tuple<Setting, ComboBox>;
            var cb = t.Item2;
            var p = t.Item1;
            foreach (var item in cb.Items)
            {
                var cbi = item as ComboBoxItem;
                string a = (string)cbi.Content;
                string b = (string)value;
                if (a == b)
                {
                    return item;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ComboBoxItem)
            {
                var cbi = value as ComboBoxItem;
                return cbi.Content;
            }
            
            return null;
        }
    }
}
