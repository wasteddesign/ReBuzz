using System;
using System.Windows.Controls;

namespace BuzzGUI.Common
{
    public class ComboBoxEx : ComboBox
    {
        public ComboBoxEx()
        {
            this.DropDownOpened += new EventHandler(ComboBoxEx_DropDownOpened);
            this.DropDownClosed += new EventHandler(ComboBoxEx_DropDownClosed);
        }

        string oldText;

        void ComboBoxEx_DropDownOpened(object sender, EventArgs e)
        {
            oldText = Text;
        }

        void ComboBoxEx_DropDownClosed(object sender, EventArgs e)
        {
            if (Text == oldText)
                Text = oldText;
        }
    }
}
