using BuzzGUI.Common;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace WDE.AudioBlock
{
    public partial class BpmDialog : Window
    {
        public Label question;
        public TextBox answer;

        public BpmDialog(ResourceDictionary resources, float bpm)
        {
            Resources = resources;
            Style = TryFindResource("ThemeWindowStyle") as Style;

            this.WindowStyle = WindowStyle.ToolWindow;
            this.ResizeMode = ResizeMode.NoResize;

            this.Width = 360;
            this.Height = 140;

            new WindowInteropHelper(this).Owner = Global.MachineViewHwndSource.Handle;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Grid mainGrid = new Grid() { Margin = new Thickness(8, 0, 8, 0) };
            mainGrid.RowDefinitions.Add(new RowDefinition());
            mainGrid.RowDefinitions.Add(new RowDefinition());
            mainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(50) });

            mainGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(230) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition());

            this.question = new Label() { Margin = new Thickness(4) };
            this.question.Content = "BPM (0 if not detected)";
            Grid.SetColumn(this.question, 0);
            Grid.SetRow(this.question, 0);
            mainGrid.Children.Add(this.question);

            answer = new TextBox() { IsReadOnly = true };
            answer.Text = bpm.ToString("0.00");
            Grid.SetColumn(answer, 1);
            Grid.SetRow(answer, 0);
            mainGrid.Children.Add(answer);


            Button close = new Button() { Name = "Close", Content = "Close", IsDefault = true, IsCancel = true, Margin = new Thickness(10, 0, 0, 0), Height = 30, Padding = new Thickness(10, 0, 10, 0) };

            StackPanel spb = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0) };

            spb.Children.Add(close);
            Grid.SetColumn(spb, 1);
            Grid.SetRow(spb, 3);
            mainGrid.Children.Add(spb);

            this.Content = mainGrid;
        }
    }
}
