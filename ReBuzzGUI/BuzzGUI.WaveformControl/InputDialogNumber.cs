using BuzzGUI.Common;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace BuzzGUI.WaveformControl
{
    public partial class InputDialogNumber : Window
    {
        public Label question;
        public NumericUpDown answer;
        public NumericUpDown numSequence;
        public NumericUpDown numSeekWindow;
        public NumericUpDown numOverlap;

        public InputDialogNumber(ResourceDictionary resources, string question, decimal defaultValue, decimal min, decimal max, int decimalPlaces, decimal change)
        {
            Resources = resources;
            Style = TryFindResource("ThemeWindowStyle") as Style;

            this.WindowStyle = WindowStyle.ToolWindow;
            this.ResizeMode = ResizeMode.NoResize;

            this.Width = 360;
            this.Height = 240;

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
            this.question.Content = question;
            Grid.SetColumn(this.question, 0);
            Grid.SetRow(this.question, 0);
            mainGrid.Children.Add(this.question);

            answer = new NumericUpDown() { Minimum = min, Maximum = max, DecimalPlaces = decimalPlaces, Change = change, Margin = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right };
            answer.Value = defaultValue;
            Grid.SetColumn(answer, 1);
            Grid.SetRow(answer, 0);
            mainGrid.Children.Add(answer);

            Label labelSequence = new Label() { Margin = new Thickness(2, 6, 0, 0) };
            labelSequence.Content = "Sequence (ms, 0 = automatic)";
            labelSequence.ToolTip = "Larger value is usually better for slowing down tempo. Growing the value decelerates the \"echoing\" artifact when slowing down the tempo.\n" +
                "Smaller value might be better for speeding up tempo. Reducing the value accelerates the \"echoing\" artifact when slowing down the tempo";
            ToolTipService.SetShowDuration(labelSequence, 60000);
            Grid.SetColumn(labelSequence, 0);
            Grid.SetRow(labelSequence, 2);
            mainGrid.Children.Add(labelSequence);

            Label labelSeekWindow = new Label() { Margin = new Thickness(2, 6, 0, 0) };
            labelSeekWindow.Content = "Seek Window (ms, 0 = automatic)";
            labelSeekWindow.ToolTip = "Larger value eases finding a good mixing position, but may cause a \"drifting\" artifact\n" +
                "Smaller reduce possibility to find a good mixing position, but reduce the \"drifting\" artifact.";
            ToolTipService.SetShowDuration(labelSeekWindow, 60000);
            Grid.SetColumn(labelSeekWindow, 0);
            Grid.SetRow(labelSeekWindow, 3);
            mainGrid.Children.Add(labelSeekWindow);

            Label labelOverlap = new Label() { Margin = new Thickness(2, 6, 0, 0) };
            labelOverlap.Content = "Overlap (ms)";
            labelOverlap.ToolTip = "If you reduce the \"sequence ms\" setting, you might wish to try a smaller value.";
            ToolTipService.SetShowDuration(labelOverlap, 60000);
            Grid.SetColumn(labelOverlap, 0);
            Grid.SetRow(labelOverlap, 4);
            mainGrid.Children.Add(labelOverlap);

            numSequence = new NumericUpDown() { Minimum = 0, Maximum = 1000, DecimalPlaces = 0, Change = 1, Margin = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right };
            numSequence.Value = 0;
            Grid.SetColumn(numSequence, 1);
            Grid.SetRow(numSequence, 2);
            mainGrid.Children.Add(numSequence);

            numSeekWindow = new NumericUpDown() { Minimum = 0, Maximum = 1000, DecimalPlaces = 0, Change = 1, Margin = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right };
            numSeekWindow.Value = 0;
            Grid.SetColumn(numSeekWindow, 1);
            Grid.SetRow(numSeekWindow, 3);
            mainGrid.Children.Add(numSeekWindow);

            numOverlap = new NumericUpDown() { Minimum = 0, Maximum = 1000, DecimalPlaces = 0, Change = 1, Margin = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right };
            numOverlap.Value = 8;
            Grid.SetColumn(numOverlap, 1);
            Grid.SetRow(numOverlap, 4);
            mainGrid.Children.Add(numOverlap);

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
            return answer.Value;
        }
    }
}
