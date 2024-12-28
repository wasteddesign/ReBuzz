using BuzzGUI.Common;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BuzzGUI.EnvelopeControl
{
    public class HandleControl : Control
    {
        static HandleControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(HandleControl), new FrameworkPropertyMetadata(typeof(HandleControl)));
        }

        readonly EnvelopeControl ec;
        readonly int index;

        public ICommand DeleteCommand { get; private set; }
        public ICommand SustainCommand { get; private set; }

        public HandleControl(EnvelopeControl ec, int index)
        {
            this.ec = ec;
            this.index = index;
            DataContext = this;

            DeleteCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { ec.DeletePoint(index); }
            };

            SustainCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { ec.SustainPoint(index); }
            };

        }



    }
}
