using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;


namespace BuzzGUI.ParameterWindow
{
    public class ParameterWindowController
    {
        Window window;

        public Window Window { get { return window; } }

        public ParameterWindowController(IMachine machine, IntPtr parenthwnd, int x, int y, string xamlpath)
        {
            if (!Load(xamlpath)) return;

            var vm = new ParameterWindowVM() { Machine = machine };
            window.DataContext = vm;

            if (x >= 0 && y >= 0)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = x - 200;
                window.Top = y - 30;
            }

            new WindowInteropHelper(window) { Owner = parenthwnd };

        }

        bool Load(string path)
        {
            try
            {
                window = (Window)BuzzGUI.Common.XamlReaderEx.LoadHack(path);
                window.Closing += window_Closing;
                window.PreviewKeyDown += window_PreviewKeyDown;
                window.MaxHeight = SystemParameters.FullPrimaryScreenHeight;
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, path);
                return false;
            }

        }

        public void Close()
        {
            var vm = (ParameterWindowVM)window.DataContext;
            vm.Machine = null;
            vm.Release();
            window.DataContext = null;
            window.Closing -= window_Closing;
            window.PreviewKeyDown -= window_PreviewKeyDown;
            window.Close();
        }

        public void Show(bool show)
        {
            if (window == null) return;

            if (show)
            {
                window.Show();
                window.BringToTop();
            }
            else
            {
                window.Hide();
            }
        }


        void window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            window.Hide();
            e.Cancel = true;

        }

        void window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                window.Hide();
            }

        }

    }
}
