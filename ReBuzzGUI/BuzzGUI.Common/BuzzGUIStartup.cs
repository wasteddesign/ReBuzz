using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace BuzzGUI.Common
{
    public static class BuzzGUIStartup
    {
        // called before anything has been initialized
        public static void PreInit()
        {
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Global.Buzz.DCWriteLine(e.Exception.Message);
                Global.Buzz.DCWriteLine(e.Exception.StackTrace);
                e.SetObserved();
            };

            SettingsWindow.AddSettings("Engine", Global.EngineSettings);
            SettingsWindow.AddSettings("General", Global.GeneralSettings);
            SettingsWindow.AddSettings("MIDI", Global.MIDISettings);

            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                if (e.Name.Contains(".XmlSerializers")) return null;

                var aneme = new AssemblyName(e.Name);

                string[] paths = new[] { "Gear\\Generators", "Gear\\Effects" };

                foreach (var p in paths)
                {
                    var rp = Path.Combine(Path.Combine(Global.BuzzPath, p), aneme.Name) + ".dll";
                    if (File.Exists(rp))
                    {
                        if (e.RequestingAssembly != null)
                            DebugConsole.WriteLine("[AssemblyResolve] Resolved: {0} by {1} -> {2}", e.Name, e.RequestingAssembly.FullName, rp);
                        else
                            DebugConsole.WriteLine("[AssemblyResolve] Resolved: {0} -> {1}", e.Name, rp);

                        return Assembly.LoadFile(rp);
                    }
                }

                if (e.RequestingAssembly != null)
                    DebugConsole.WriteLine("[AssemblyResolve] Not resolved: {0} by {1}", e.Name, e.RequestingAssembly.FullName);
                else
                    DebugConsole.WriteLine("[AssemblyResolve] Not resolved: {0}", e.Name);

                return null;
            };
        }

        // called after gui components have been created
        public static void Startup()
        {
            BuzzGUI.Common.Presets.PresetDictionary.Init();

        }
    }
}
