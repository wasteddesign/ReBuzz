using Microsoft.Win32;
using System;
using System.Windows;

namespace BuzzGUI.Common
{
    public class PersistentWindowPlacement
    {
        readonly Window wnd;

        readonly string regpath = Global.RegistryRoot + "BuzzGUI\\WindowPlacement";

        public PersistentWindowPlacement(Window wnd)
        {
            this.wnd = wnd;
            wnd.Initialized += (sender, e) => { Load(); };
            wnd.Closing += (sender, e) => { Save(); };

        }

        void Save()
        {
            try
            {
                var s = wnd.WindowState.ToString() + " " + wnd.RestoreBounds.ToString();

                var key = Registry.CurrentUser.CreateSubKey(regpath);
                if (key == null) return;

                key.SetValue(wnd.GetType().FullName, s);
            }
            catch (Exception) { }

        }

        void Load()
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey(regpath);
                if (key == null) return;

                var s = key.GetValue(wnd.GetType().FullName) as string;
                if (s == null) return;

                var w = s.Split(' ');

                wnd.WindowState = (WindowState)Enum.Parse(typeof(WindowState), w[0]);

                var r = Rect.Parse(w[1]);
                if (!r.IsEmpty)
                {
                    wnd.Left = r.Left;
                    wnd.Top = r.Top;
                    wnd.Width = r.Width;
                    wnd.Height = r.Height;
                }

            }
            catch (Exception) { }
        }

    }
}
