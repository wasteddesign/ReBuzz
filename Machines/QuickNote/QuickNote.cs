using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MdXaml;
using System.Diagnostics;
using System.Windows.Input;

namespace WDE.QuickNote
{
	[MachineDecl(Name = "QuickNote", ShortName = "Note", Author = "WDE", MaxTracks = 1)]
	public class QuickNoteMachine : IBuzzMachine, INotifyPropertyChanged
	{
        public QuickNoteMachine(IBuzzMachineHost host)
		{
        }

        [ParameterDecl(Name = "Dummy", DefValue = false)]
        public bool Dummy { get; set; }


        // Control machine
        public void Work()
		{
		}
	
		public class State : INotifyPropertyChanged
		{
            string text;
			public string Text { get => text; set { text = value; PropertyChanged.Raise(this, "Text"); } }
			public State()
			{	
			}	// NOTE: parameterless constructor is required by the xml serializer

            public event PropertyChangedEventHandler PropertyChanged;
		}

		State machineState = new State();

        public State MachineState			// a property called 'MachineState' gets automatically saved in songs and presets
		{
			get { return machineState; }
			set
			{
				machineState = value;
				if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("MachineState"));
            }
		}

        public IEnumerable<IMenuItem> Commands
		{
			get
			{
				yield return new MenuItemVM()
				{
					Text = "About...",
					Command = new SimpleCommand()
					{
						CanExecuteDelegate = p => true,
						ExecuteDelegate = p => MessageBox.Show(@"QuickNote 0.1 (C) 2026 WDE")
					}
				};
			}
		}

        public event PropertyChangedEventHandler? PropertyChanged;
	}

    [MachineGUIFactoryDecl(IsGUIResizable = true, PreferWindowedGUI = true, UseThemeStyles = true, Width = 300, Height = 350)]
    public class MachineGUIFactory : IMachineGUIFactory { public IMachineGUI CreateGUI(IMachineGUIHost host) { return new QuickNoteGUI(); } }
    public class QuickNoteGUI : UserControl, IMachineGUI
    {
        IMachine machine;

        MarkdownScrollViewer markdownScrollViewer;
        TextBox textBox;
        public IMachine Machine
        {
            get => machine; set
            {
                if (machine != null)
                {
                    BindingOperations.ClearBinding(textBox, TextBox.TextProperty);
                }

                machine = value;

                if (machine != null)
                {
                    QuickNoteMachine quikNoteMachine = (QuickNoteMachine)machine.ManagedMachine;
                    textBox.SetBinding(TextBox.TextProperty, new Binding("MachineState.Text") { Source = quikNoteMachine, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                }
            }
        }

        public QuickNoteGUI()
        {
            ResourceDictionary? rd = GetBuzzThemeResources();
            if (rd != null) this.Resources.MergedDictionaries.Add(rd);

            TabControl tabControl = new TabControl() { Margin = new Thickness(4, 4, 4, 4), VerticalContentAlignment = VerticalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            TabItem tabItemNote = new TabItem() { Header = "Note" };
            markdownScrollViewer = new MarkdownScrollViewer() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

            var style = (Style)TryFindResource("MarkdownScrollViewerStyleQuickNote");
            if (style != null) markdownScrollViewer.MarkdownStyle = style;
            
            tabItemNote.Content = markdownScrollViewer;

            markdownScrollViewer.OnHyperLinkClicked += (link) =>
            {
                if (link != null)
                {
                    Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
                }
            };

            tabControl.Items.Add(tabItemNote);
            TabItem tabItemEdit = new TabItem() { Header = "Edit" };

            textBox = new TextBox() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Stretch, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

            style = (Style)TryFindResource("TextBoxStyleQuickNote");
            if (style != null) textBox.Style = style;
            
            tabItemEdit.Content = textBox;
            tabControl.Items.Add(tabItemEdit);

            textBox.TextChanged += (sender, e) =>
            {
                markdownScrollViewer.Markdown = textBox.Text;
            };

            textBox.PreviewMouseWheel += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    double fontSize = textBox.FontSize;
                    fontSize += e.Delta / 600.0;
                    if (fontSize < 8)
                        fontSize = 8;

                    textBox.FontSize = fontSize;

                    e.Handled = true;
                }
            };

            this.Content = tabControl;
        }

        internal static ResourceDictionary? GetBuzzThemeResources()
        {
            ResourceDictionary skin = new ResourceDictionary();

            try
            {
                string selectedTheme = Global.Buzz.SelectedTheme == "<default>" ? "Default" : Global.Buzz.SelectedTheme;
                string skinPath = Global.BuzzPath + "\\Themes\\" + selectedTheme + "\\Gear\\QuickInfo\\QuickInfo.xaml";
                skin.Source = new Uri(skinPath, UriKind.Absolute);
            }
            catch
            {
                return null;
            }

            return skin;
        }
    }
}
