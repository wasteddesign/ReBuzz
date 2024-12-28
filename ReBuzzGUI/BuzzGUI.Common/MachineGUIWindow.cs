using BuzzGUI.Interfaces;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace BuzzGUI.Common
{
    public class MachineGUIWindow : Window, IMachineGUIHost
    {
        readonly IMachine machine;
        readonly IMachineGUI gui;
        readonly EditContext editContext;

        public MachineGUIWindow(IMachine machine, IntPtr parenthwnd, ResourceDictionary resources)
        {
            this.Resources = resources;
            if (resources != null) Style = TryFindResource("ThemeWindowStyle") as Style;

            var machineWindowStyle = machine.DLL.GUIFactory.GetPropertyOrDefault<Style>("WindowStyle");

            if (machineWindowStyle != null)
                this.Style = machineWindowStyle;

            editContext = new EditContext(this);
            new WindowInteropHelper(this) { Owner = parenthwnd };
            WindowStyle = WindowStyle.ToolWindow;
            ShowInTaskbar = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = machine.DLL.GUIFactoryDecl.IsGUIResizable ? ResizeMode.CanResize : ResizeMode.NoResize;
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, Global.GeneralSettings.WPFIdealFontMetrics ? TextFormattingMode.Ideal : TextFormattingMode.Display);

            this.machine = machine;
            gui = machine.DLL.GUIFactory.CreateGUI(this);
            gui.Machine = machine;
            this.Content = gui;

            if (gui is FrameworkElement)
            {
                var fe = gui as FrameworkElement;
                DependencyPropertyDescriptor.FromProperty(FrameworkElement.MinWidthProperty, typeof(FrameworkElement)).AddValueChanged(fe, ContentSizeMinMaxChanged);
                DependencyPropertyDescriptor.FromProperty(FrameworkElement.MaxWidthProperty, typeof(FrameworkElement)).AddValueChanged(fe, ContentSizeMinMaxChanged);
                DependencyPropertyDescriptor.FromProperty(FrameworkElement.MinHeightProperty, typeof(FrameworkElement)).AddValueChanged(fe, ContentSizeMinMaxChanged);
                DependencyPropertyDescriptor.FromProperty(FrameworkElement.MaxHeightProperty, typeof(FrameworkElement)).AddValueChanged(fe, ContentSizeMinMaxChanged);
                UpdateSizeConstraints();

                this.SizeChanged += (sender, e) =>
                {
                    // user resizes window
                    var tfs = this.GetTotalNonclientAreaSize();
                    var s = e.NewSize;
                    fe.Width = s.Width - tfs.Width;
                    fe.Height = s.Height - tfs.Height;
                };

                fe.SizeChanged += (sender, e) =>
                {
                    // machine gui resizes itself
                    var tfs = this.GetTotalNonclientAreaSize();
                    var s = e.NewSize;
                    this.Width = s.Width + tfs.Width;
                    this.Height = s.Height + tfs.Height;
                };
            }

            var mb = new MultiBinding() { StringFormat = "{0} - {1}" };
            mb.Bindings.Add(new Binding("Name") { Source = machine });
            mb.Bindings.Add(new Binding("DLL.Info.Name") { Source = machine });
            SetBinding(Window.TitleProperty, mb);

            this.Closing += MachineGUIWindow_Closing;
            this.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
                {
                    e.Handled = true;
                    Hide();
                }
            };

            Show();
        }

        void MachineGUIWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Hide();
            e.Cancel = true;
        }

        public void Release()
        {
            if (gui is FrameworkElement)
            {
                var fe = gui as FrameworkElement;
                DependencyPropertyDescriptor.FromProperty(FrameworkElement.MinWidthProperty, typeof(FrameworkElement)).RemoveValueChanged(fe, ContentSizeMinMaxChanged);
                DependencyPropertyDescriptor.FromProperty(FrameworkElement.MaxWidthProperty, typeof(FrameworkElement)).RemoveValueChanged(fe, ContentSizeMinMaxChanged);
                DependencyPropertyDescriptor.FromProperty(FrameworkElement.MinHeightProperty, typeof(FrameworkElement)).RemoveValueChanged(fe, ContentSizeMinMaxChanged);
                DependencyPropertyDescriptor.FromProperty(FrameworkElement.MaxHeightProperty, typeof(FrameworkElement)).RemoveValueChanged(fe, ContentSizeMinMaxChanged);
            }

            gui.Machine = null;
            this.Closing -= MachineGUIWindow_Closing;
            this.Close();
        }

        public void Show(bool show)
        {
            if (show)
            {
                Show();
                this.BringToTop();
            }
            else
            {
                Hide();
            }
        }

        void ContentSizeMinMaxChanged(object sender, EventArgs e)
        {
            UpdateSizeConstraints();
        }

        void UpdateSizeConstraints()
        {
            var fe = gui as FrameworkElement;
            if (fe == null) return;

            var s = this.GetTotalNonclientAreaSize();
            MinWidth = fe.MinWidth + s.Width;
            MinHeight = fe.MinHeight + s.Height;
            MaxWidth = fe.MaxWidth + s.Width;
            MaxHeight = fe.MaxHeight + s.Height;
        }

        class EditContext : IEditContext
        {
            readonly MachineGUIWindow window;
            readonly ManagedActionStack actionStack = new ManagedActionStack();
            public ManagedActionStack ManagedActionStack { get { return actionStack; } }

            public EditContext(MachineGUIWindow window)
            {
                this.window = window;
            }

            public IActionStack ActionStack { get { return actionStack; } }
            public ICommand CutCommand { get { return null; } }
            public ICommand CopyCommand { get { return null; } }
            public ICommand PasteCommand { get { return null; } }

        }

        public void DoAction(IAction a)
        {
            editContext.ManagedActionStack.Do(a);
        }


    }
}
