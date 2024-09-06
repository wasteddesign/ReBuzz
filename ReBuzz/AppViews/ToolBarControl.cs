using System.Windows.Controls;

namespace ReBuzz.AppViews
{
    internal class ToolBarControl : UserControl
    {
        public ToolBarControl()
        {
            UserControl toolBarXaml = Common.Utils.GetUserControlXAML<UserControl>("ToolBar.xaml");
            toolBarXaml.BeginInit();

        }
    }
}
