using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace BuzzGUI.FileBrowser.SplitButtonControl
{
    /// <summary>
    /// Interaction logic for SplitButtonControl.xaml
    /// </summary>
    public partial class SplitButton : UserControl
    {

        public SplitButton()
        {
            InitializeComponent();

            btFolder.Click += (sender, e) =>
            {
                Click.Invoke(this, e);
                e.Handled = true;
            };

            btFolderMenu.Click += (sender, e) =>
            {
                CreateMenu.Invoke();
                //btFolderMenu.ContextMenu.IsOpen = true;                

                // Bug in .NET. Workaround to apply correct respource tp ContextMenu
                // https://github.com/dotnet/wpf/issues/5656
                InputManager.Current.ProcessInput(
                new KeyEventArgs(
                    Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(this),
                    Environment.TickCount,
                    Key.Apps)
                {
                    RoutedEvent = KeyUpEvent,
                    Source = btFolderMenu
                });
            };
        }

        public ItemCollection Items
        {
            get
            {
                return btFolderMenu.ContextMenu.Items;
            }
        }
        public PlacementMode Placement { get => btFolderMenu.ContextMenu.Placement; set => btFolderMenu.ContextMenu.Placement = value; }

        new public object Content { get => btFolder.Content; set => btFolder.Content = value; }

        public Action<object, object> Click { get; internal set; }
        public Action CreateMenu { get; internal set; }
    }
}
