using BuzzGUI.Common;
using ReBuzz.Core;
using Serilog;
using Serilog.Sinks.RichTextBox.Themes;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace ReBuzz.Common
{
    /// <summary>
    /// Interaction logic for DebugWindow.xaml
    /// </summary>
    public partial class DebugWindow : Window
    {  
        readonly MemoryStream debugStream;

        string[] commands =
        {
            "clear",
            "close",
            "commands",
            "instruments",
            "machines",
            "loggertheme",
        };

        public DebugWindow()
        {   
            DataContext = this;
            InitializeComponent();

            Array.Sort(commands);

            Log.Logger = new LoggerConfiguration()
                .WriteTo.RichTextBox(tbDebug)
                .CreateLogger();

            Paragraph p = tbDebug.Document.Blocks.FirstBlock as Paragraph;
            p.LineHeight = 10;

            debugStream = new MemoryStream();

            tbDebug.TextChanged += (sender, e) =>
            {   
                tbDebug.ScrollToEnd();
            };

            tbDebugInput.IsVisibleChanged += (sender, e) =>
            {
                if (this.Visibility == Visibility.Visible)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Input,
                        new Action(delegate () {
                            tbDebugInput.Focus();
                            Keyboard.Focus(tbDebugInput);
                        }));
                }
            };

            tbDebugInput.KeyDown += (sender, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    var rb = Global.Buzz as ReBuzzCore;
                    var inputs = tbDebugInput.Text.Split(null);
                    
                    if (inputs.Length > 0)
                    {
                        switch (inputs[0])
                        {
                            case "close":
                                this.Hide();
                                break;
                            case "machines":
                                foreach (var m in rb.MachineDLLsList.Keys)
                                {
                                    Log.Information(m);
                                }
                                break;
                            case "instruments":
                                foreach (var i in rb.Instruments.Where(inst => inst.Name != "").OrderBy(i => i.Name))
                                {
                                    Log.Information(i.Name);
                                }
                                break;
                            case "commands":
                                string msg = "Available commands: ";

                                foreach (var c in commands)
                                {
                                    msg += c + ", ";
                                }
                                Log.Information(msg);
                                break;
                            case "clear":
                                tbDebug.Document.Blocks.Clear();
                                break;
                            case "loggertheme":
                                if (inputs.Length <= 1)
                                    Log.Information("Usage: loggertheme [theme]\nAvaialbe themes: None, Grayscale, Colored and Literate");
                                else
                                {
                                    var reqtheme = inputs[1].ToLower().Trim();
                                    RichTextBoxConsoleTheme theme = RichTextBoxConsoleTheme.Literate;
                                    //theme: RichTextBoxConsoleTheme.Grayscale
                                    if (reqtheme == "none")
                                        theme = (RichTextBoxConsoleTheme)RichTextBoxConsoleTheme.None;
                                    else if (reqtheme == "grayscale")
                                        theme = (RichTextBoxConsoleTheme)RichTextBoxConsoleTheme.Grayscale;
                                    else if (reqtheme == "colored")
                                        theme = (RichTextBoxConsoleTheme)RichTextBoxConsoleTheme.Colored;
                                    else if (reqtheme == "literate")
                                        theme = (RichTextBoxConsoleTheme)RichTextBoxConsoleTheme.Literate;

                                    Log.Logger = new LoggerConfiguration()
                                        .WriteTo.RichTextBox(tbDebug, theme: theme)
                                        .CreateLogger();
                                }
                                break;
                        }
                    }
                    tbDebugInput.Clear();
                }
            };

            Loaded += (sender, e) =>
            {
                var rd = Utils.GetUserControlXAML<Window>("ParameterWindowShell.xaml");
                Resources.MergedDictionaries.Add(rd.Resources);

                PreviewMouseWheel += (sender, e) =>
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        double newSize = tbDebug.FontSize + e.Delta / 120.0;
                        tbDebug.FontSize = Math.Max( Math.Min(newSize, 30), 7);
                        
                        e.Handled = true;
                    }
                };

                tbDebug.ScrollToEnd();
            };

            Closing += (sender, e) =>
            {
                Hide();
                e.Cancel = true;
            };
        }
    }
}
