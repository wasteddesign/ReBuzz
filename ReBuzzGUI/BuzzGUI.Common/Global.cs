using BuzzGUI.Interfaces;

namespace BuzzGUI.Common
{
    public static class Global
    {
        public static IBuzz Buzz { get; set; }
        public static string RegistryRoot { get { return "Software\\ReBuzz\\"; } }
        public static System.Windows.Interop.HwndSource MachineViewHwndSource { get; set; }

        public static string BuzzPath
        {
            get
            {
                return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
        }

        public static Settings.EngineSettings EngineSettings = new Settings.EngineSettings();
        public static Settings.GeneralSettings GeneralSettings = new Settings.GeneralSettings();
        public static Settings.MIDISettings MIDISettings = new Settings.MIDISettings();

    }
}
