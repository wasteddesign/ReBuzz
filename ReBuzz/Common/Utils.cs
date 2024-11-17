using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ReBuzz.Common
{
    public class Utils
    {
        public static ResourceDictionary GetBuzzThemeResources(string theme)
        {
            ResourceDictionary res = new ResourceDictionary();

            try
            {
                string selectedTheme = Global.Buzz.SelectedTheme == "<default>" ? "Default" : Global.Buzz.SelectedTheme;
                string skinPath = Global.BuzzPath + "\\Themes\\" + selectedTheme + "\\" + theme;

                res = XamlReaderEx.LoadHack(skinPath) as ResourceDictionary;
            }
            catch (Exception)
            {
                string skinPath = Global.BuzzPath + "\\Themes\\Default\\" + theme;
                res = XamlReaderEx.LoadHack(skinPath) as ResourceDictionary;
            }

            return res;
        }

        public static T GetUserControlXAML<T>(string xaml)
        {
            T rootObject;

            try
            {
                string selectedTheme = Global.Buzz.SelectedTheme == "<default>" ? "Default" : Global.Buzz.SelectedTheme;
                string skinPath = Global.BuzzPath + "\\Themes\\" + selectedTheme + "\\" + xaml;

                rootObject = (T)XamlReaderEx.LoadHack(skinPath);

            }
            catch (Exception)
            {
                string skinPath = Global.BuzzPath + "\\Themes\\Default\\" + xaml;
                StreamReader mysr = new StreamReader(skinPath);
                rootObject = (T)XamlReaderEx.LoadHack(skinPath);
            }

            return rootObject;
        }

        public class ColIndex : IIndex<string, Color>
        {
            public Dictionary<string, Color> Colors { get; }
            public ColIndex() { Colors = new Dictionary<string, Color>(); }

            public void Add(string key, Color val)
            {
                Colors[key] = val;
            }
            public Color this[string index] => Colors.ContainsKey(index) ? Colors[index] : System.Windows.Media.Colors.Black;
        }

        public static IIndex<string, Color> GetThemeColors()
        {
            ColIndex colIndex = new ColIndex();

            // Add defaults?
            InitThemeColors(colIndex);

            string selectedTheme = Global.Buzz.SelectedTheme == "<default>" ? "Default" : Global.Buzz.SelectedTheme + ".col";

            if (selectedTheme != "Default")
            {
                string skinPath = Global.BuzzPath + "\\Themes\\" + selectedTheme;

                string[] readText = File.ReadAllLines(skinPath);
                string[] delimiterChars = {
                            "\t",
                            "  "
                          };
                foreach (string s in readText)
                {
                    try
                    {
                        if (!s.Trim().StartsWith("#") && s.Trim().Length > 0)
                        {
                            string[] items = s.Trim().Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                            string key = items[0].Trim();
                            Color value = (Color)ColorConverter.ConvertFromString("#ff" + items.Last().Trim());
                            colIndex.Add(key, value);
                        }
                    }
                    catch { }
                }
            }

            return colIndex;
        }

        static readonly (string Name, uint Color)[] defaultColors =
        [
            ("DC BG", 0xFF000000),
            ("DC Text", 0xFFC0C0C0),
            ("IV BG", 0xFF000000),
            ("IV Text", 0xFF000000),
            ("MV Amp BG", 0xFFFFFFFF),
            ("MV Amp Handle", 0xFF000000),
            ("MV Arrow", 0xFFFFFFFF),
            ("MV Background", 0xFFDAD6C9),
            ("MV Control", 0xFFADADAD),
            ("MV Effect", 0xFFC7ADA9),
            ("MV Effect LED Border", 0xFF000000),
            ("MV Effect LED Off", 0xFF641E1E),
            ("MV Effect LED On", 0xFFFF6464),
            ("MV Effect Mute", 0xFF9F8A87),
            ("MV Effect Pan BG", 0xFF92655F),
            ("MV Generator", 0xFFA9AEC7),
            ("MV Generator LED Border", 0xFF000000),
            ("MV Generator LED Off", 0xFF28288C),
            ("MV Generator LED On", 0xFF6464FF),
            ("MV Generator Mute", 0xFF878B9F),
            ("MV Generator Pan BG", 0xFF5F6792),
            ("MV Line", 0xFF000000),
            ("MV Machine Border", 0xFF000000),
            ("MV Machine Select 1", 0xFFFFFFFF),
            ("MV Machine Select 2", 0xFF000000),
            ("MV Machine Text", 0xFF000000),
            ("MV Master", 0xFFC6BEAA),
            ("MV Master LED Border", 0xFF000000),
            ("MV Master LED Off", 0xFF595922),
            ("MV Master LED On", 0xFFE8E8C1),
            ("MV Pan Handle", 0xFF000000),
            ("PE BG", 0xFFDAD6C9),
            ("PE BG Dark", 0xFFBDB59F),
            ("PE BG Very Dark", 0xFF9F9373),
            ("PE Sel BG", 0xFFF7F7F4),
            ("PE Text", 0xFF303021),
            ("SA Amp BG", 0xFF000000),
            ("SA Amp Line", 0xFF00C800),
            ("SA Freq BG", 0xFF000000),
            ("SA Freq Line", 0xFF00C800),
            ("SE BG", 0xFFDAD6C9),
            ("SE BG Dark", 0xFFBDB59F),
            ("SE BG Very Dark", 0xFF9F9373),
            ("SE Break Box", 0xFFE0B06D),
            ("SE Line", 0xFF000000),
            ("SE Mute Box", 0xFFDD816C),
            ("SE Pattern Box", 0xFFC6BEA9),
            ("SE Sel BG", 0xFFF7F7F4),
            ("SE Song Position", 0xFFFFFF00),
            ("SE Text", 0xFF303021),
            ("black", 0xFF000000)
        ];

        static void InitThemeColors(ColIndex colIndex)
        {
            foreach (var col in defaultColors)
            {
                byte[] bytes = BitConverter.GetBytes(col.Color);
                colIndex.Add(col.Name, Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]));
            }
        }

        public static IntPtr GetObjectHandle(object obj)
        {
            GCHandle handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
            IntPtr ptr = GCHandle.ToIntPtr(handle);
            handle.Free();
            return ptr;
        }

        public static object GetObjectFromHandle(IntPtr ptr)
        {
            return GCHandle.FromIntPtr(ptr).Target;
        }

        internal static List<string> GetThemes()
        {
            List<string> themes = new List<string>();
            themes.Add("<default>");

            foreach (var file in Directory.GetFiles(Global.BuzzPath + "\\Themes", "*.col"))
            {
                themes.Add(Path.GetFileNameWithoutExtension(file));
            }

            return themes;
        }


        internal static void SetProcessorAffinityMask(bool ideal)
        {
            var process = Process.GetCurrentProcess();
            long processorAffinityMask = RegistryEx.Read("ProcessorAffinity", 0xFFFFFFFF, "Settings");
            try
            {
                if (processorAffinityMask < process.ProcessorAffinity)
                    process.ProcessorAffinity = (IntPtr)processorAffinityMask;
            }
            catch (Exception ex)
            {
                Utils.MessageBox(ex.Message);
            }

            /*
            if (ideal)
            {
                processorAffinityMask = (long)process.ProcessorAffinity;
                int processorCount = Environment.ProcessorCount;// >= 32 ? 31 : Environment.ProcessorCount;
                var threads = process.Threads;
                for (int i = 0; i < threads.Count; i++)
                {
                    var pt = threads[i];
                    int targetCpu = i;
                    // Find next cpu
                    for (int j = 0; j < processorCount; j++)
                    {
                        int target = (j + targetCpu) % processorCount;
                        bool cpuEnabled = (processorAffinityMask & (1L << target)) != 0;
                        if (cpuEnabled)
                        {
                            try
                            {
                                threads[i].IdealProcessor = target;
                            }
                            catch { }
                            break;
                        }
                    }
                }
            }
            */
        }

        static Window mainWindow = null;

        public static void InitUtils(Window appWindow)
        {
            Utils.mainWindow = appWindow;
        }

        public static MessageBoxResult MessageBox(string message, string caption = null, MessageBoxButton messageBoxButton = MessageBoxButton.OK)
        {
            var handle = new WindowInteropHelper(Application.Current.MainWindow).Handle;

            Utils.mainWindow.Topmost = true;
            MessageBoxResult result = System.Windows.MessageBox.Show(message, caption, messageBoxButton);
            Utils.mainWindow.Topmost = false;

            return result;
        }

        public static unsafe byte[] SerializeValueType<T>(in T value) where T : unmanaged
        {
            byte[] result = new byte[sizeof(T)];
            fixed (byte* dst = result)
                *(T*)dst = value;
            return result;
        }

        public static unsafe byte[] SerializeValueTypeChangePointer<T>(in T value, ref byte[] result) where T : unmanaged
        {
            fixed (byte* dst = result)
                *(T*)dst = value;
            return result;
        }

        public static void AllowUIToUpdate()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new DispatcherOperationCallback(delegate (object parameter)
            {
                frame.Continue = false;
                return null;

            }), null);
            Dispatcher.PushFrame(frame);
        }

        //static float k_DENORMAL_DC = 1e-18f; // Flush to Zero
        static float k_DENORMAL_DC = 1e-15f; // DC
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FlushDenormalToZero(Sample[] samples)
        {
            /*
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i].L += k_DENORMAL_DC;
                //samples[i].L -= k_DENORMAL_DC;

                samples[i].R += k_DENORMAL_DC;
                //samples[i].R -= k_DENORMAL_DC;
            }
            */
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FlushDenormalToZero(float f)
        {
            f += k_DENORMAL_DC;
            //f -= k_DENORMAL_DC;
            return f;
        }

        internal static void FlipDenormalDC()
        {
            k_DENORMAL_DC = -k_DENORMAL_DC;
        }
    }
}
