using System.Windows.Input;

namespace BuzzGUI.Common
{
    public class DragMouseGesture
    {
        public MouseButton Button { get; set; }
        public ModifierKeys Modifiers { get; set; }

        public bool Matches(MouseButtonEventArgs e) { return e.ChangedButton == Button && Keyboard.Modifiers == Modifiers; }
        public bool ButtonMatches(MouseButtonEventArgs e) { return e.ChangedButton == Button; }
    }
}
