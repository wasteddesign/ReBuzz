namespace BuzzGUI.WavetableView
{
    public class WavetableVMViewSettings
    {
        public EditContextWT EditContext { get; set; }

        public WavetableVMViewSettings(WavetableVM wtVM)
        {
            EditContext = new EditContextWT(wtVM);
        }
    }
}
