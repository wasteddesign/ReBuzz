using System.Windows.Controls;
using BuzzGUI.Common;

namespace ReBuzz.AppViews
{
    internal class ToolBarControl : UserControl
    {
        public ToolBarControl()
        {
            UserControl toolBarXaml = Common.Utils.GetUserControlXAML<UserControl>("ToolBar.xaml", Global.BuzzPath);
            toolBarXaml.BeginInit();

        }
    }
}
