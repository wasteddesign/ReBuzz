using BuzzGUI.Common;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace WDE.AudioBlock
{
    public partial class InputDialogSpectrogram : Window
    {
        public Label question;
        public NumericUpDown numLowFreq;
        public NumericUpDown numHighFreq;
        public NumericUpDown numIntensity;
        public ComboBox cbColorMap;

        public InputDialogSpectrogram(ResourceDictionary resources, int freqLow, int freqHigh, int intensity, int colorMap)
        {   
            if (resources != null)
            {
                Resources.MergedDictionaries.Add(resources);
            }
            else
            {
                ResourceDictionary rd = Utils.GetBuzzWindowTheme();
                if (rd != null)
                    Resources.MergedDictionaries.Add(rd);
            }

            Style = TryFindResource("ThemeWindowStyle") as Style;

            this.WindowStyle = WindowStyle.ToolWindow;
            this.ResizeMode = ResizeMode.NoResize;

            this.Width = 360;
            this.Height = 260;

            new WindowInteropHelper(this).Owner = BuzzGUI.Common.Global.MachineViewHwndSource.Handle;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Grid mainGrid = new Grid() { Margin = new Thickness(8, 0, 8, 0) };
            mainGrid.RowDefinitions.Add(new RowDefinition());
            mainGrid.RowDefinitions.Add(new RowDefinition());
            mainGrid.RowDefinitions.Add(new RowDefinition());
            mainGrid.RowDefinitions.Add(new RowDefinition());
            mainGrid.RowDefinitions.Add(new RowDefinition());
            mainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(50) });

            mainGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(230) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition());

            this.question = new Label() { Margin = new Thickness(4) };
            this.question.Content = "Spectrogram Settings";
            Grid.SetColumn(this.question, 0);
            Grid.SetRow(this.question, 0);
            mainGrid.Children.Add(this.question);


            Label labelHighFreq = new Label() { Margin = new Thickness(2, 6, 0, 0) };
            labelHighFreq.Content = "Freq High (Hz)";
            // labelSequence.ToolTip = "";
            // ToolTipService.SetShowDuration(labelSequence, 60000);
            Grid.SetColumn(labelHighFreq, 0);
            Grid.SetRow(labelHighFreq, 1);
            mainGrid.Children.Add(labelHighFreq);

            numHighFreq = new NumericUpDown() { Minimum = 0, Maximum = 44100, DecimalPlaces = 0, Change = 1, Margin = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right };
            numHighFreq.Value = freqHigh;
            Grid.SetColumn(numHighFreq, 1);
            Grid.SetRow(numHighFreq, 1);
            mainGrid.Children.Add(numHighFreq);

            Label labelLowFreq = new Label() { Margin = new Thickness(2, 6, 0, 0) };
            labelLowFreq.Content = "Freq Low (Hz)";
            // labelSequence.ToolTip = "";
            // ToolTipService.SetShowDuration(labelSequence, 60000);
            Grid.SetColumn(labelLowFreq, 0);
            Grid.SetRow(labelLowFreq, 2);
            mainGrid.Children.Add(labelLowFreq);

            numLowFreq = new NumericUpDown() { Minimum = 0, Maximum = 44100, DecimalPlaces = 0, Change = 1, Margin = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right };
            numLowFreq.Value = freqLow;
            Grid.SetColumn(numLowFreq, 1);
            Grid.SetRow(numLowFreq, 2);
            mainGrid.Children.Add(numLowFreq);

            Label labelIntensity = new Label() { Margin = new Thickness(2, 6, 0, 0) };
            labelIntensity.Content = "Intensity";
            // labelSequence.ToolTip = "";
            // ToolTipService.SetShowDuration(labelSequence, 60000);
            Grid.SetColumn(labelIntensity, 0);
            Grid.SetRow(labelIntensity, 3);
            mainGrid.Children.Add(labelIntensity);

            numIntensity = new NumericUpDown() { Minimum = 1, Maximum = 40, DecimalPlaces = 0, Change = 1, Margin = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right };
            numIntensity.Value = intensity;
            Grid.SetColumn(numIntensity, 1);
            Grid.SetRow(numIntensity, 3);
            mainGrid.Children.Add(numIntensity);

            Label labelColormap = new Label() { Margin = new Thickness(2, 6, 0, 0) };
            labelColormap.Content = "Color Map";
            // labelSequence.ToolTip = "";
            // ToolTipService.SetShowDuration(labelSequence, 60000);
            Grid.SetColumn(labelColormap, 0);
            Grid.SetRow(labelColormap, 4);
            mainGrid.Children.Add(labelColormap);

            cbColorMap = new ComboBox() { Margin = new Thickness(0), Width = 140, HorizontalContentAlignment = HorizontalAlignment.Right, VerticalContentAlignment = VerticalAlignment.Center, Height = 22 };
            // From Spectrogram ColorMap
            cbColorMap.Items.Add("Grayscale");
            cbColorMap.Items.Add("Grayscale Inverted");
            cbColorMap.Items.Add("Viridis");
            cbColorMap.Items.Add("Green");
            cbColorMap.Items.Add("Blue");
            cbColorMap.SelectedIndex = colorMap;
            Grid.SetColumn(cbColorMap, 1);
            Grid.SetRow(cbColorMap, 4);
            mainGrid.Children.Add(cbColorMap);

            Button yes = new Button() { Name = "Ok", Content = "Ok", IsDefault = true, Margin = new Thickness(0, 0, 0, 0), Height = 30, Padding = new Thickness(10, 0, 10, 0) };
            yes.Click += Yes_Click;
            Button cancel = new Button() { Name = "Cancel", Content = "Cancel", IsCancel = true, Margin = new Thickness(10, 0, 0, 0), Height = 30, Padding = new Thickness(10, 0, 10, 0) };

            StackPanel spb = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0) };
            spb.Children.Add(yes);
            spb.Children.Add(cancel);
            Grid.SetColumn(spb, 1);
            Grid.SetRow(spb, 5);
            mainGrid.Children.Add(spb);

            this.Content = mainGrid;

        }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        public decimal GetAnswer()
        {
            return numLowFreq.Value;
        }
    }
}
