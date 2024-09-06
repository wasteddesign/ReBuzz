using BuzzGUI.Interfaces;
using System.Windows;

namespace ReBuzz.ManagedMachine
{
    /// <summary>
    /// Interaction logic for MachineGUIHostWindow.xaml
    /// </summary>
    public partial class MachineGUIHostWindow : Window, IMachineGUIHost
    {
        public MachineGUIHostWindow()
        {
            InitializeComponent();
        }

        public void DoAction(IAction a)
        {

        }
    }
}
