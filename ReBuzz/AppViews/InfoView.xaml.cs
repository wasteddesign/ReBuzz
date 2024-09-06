using BuzzGUI.Interfaces;
using System.Windows.Controls;

namespace ReBuzz
{
    /// <summary>
    /// Interaction logic for InfoView.xaml
    /// </summary>
    public partial class InfoView : UserControl, IActionStack
    {
        public InfoView()
        {
            InitializeComponent();
        }

        public bool CanUndo => tbInfo.CanUndo;

        public bool CanRedo => tbInfo.CanRedo;

        public void BeginActionGroup()
        {

        }

        public void Do(IAction a)
        {

        }

        public void EndActionGroup()
        {
        }

        public void Redo()
        {
            tbInfo.Redo();
        }

        public void Undo()
        {
            tbInfo.Undo();
        }
    }
}
