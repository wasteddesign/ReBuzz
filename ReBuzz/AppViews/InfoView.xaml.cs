using BuzzGUI.Interfaces;
using ReBuzz.Core;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Xml.Linq;

namespace ReBuzz
{
    /// <summary>
    /// Interaction logic for InfoView.xaml
    /// </summary>
    public partial class InfoView : UserControl, IActionStack
    {
        public ReBuzzCore ReBuzz { get; }

        public InfoView(ReBuzzCore rb)
        {
            this.ReBuzz = rb;
            InitializeComponent();

            DataContext = this;

            tbInfo.SetBinding(TextBox.TextProperty, new Binding("ReBuzz.InfoText") { Source = this, Mode = BindingMode.TwoWay });

            Unloaded += (s, e) =>
            {
                BindingOperations.ClearBinding(tbInfo, TextBox.TextProperty);
            };
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
