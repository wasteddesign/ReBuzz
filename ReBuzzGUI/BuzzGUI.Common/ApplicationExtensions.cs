using System;
using System.IO;
using System.Windows;

namespace BuzzGUI.Common
{
    public static class ApplicationExtensions
    {
        public static void LoadWPFTheme(this Application app)
        {
            try
            {
                app.Resources.MergedDictionaries.Clear();

                var t = (string)Microsoft.Win32.Registry.GetValue("HKEY_CURRENT_USER\\Software\\ReBuzz\\Settings", "WPFTheme", "");

                Uri uri = null;
                string path = null;

                if (t == "Aero")
                    uri = new Uri("PresentationFramework.Aero;V3.0.0.0;31bf3856ad364e35;component\\themes/aero.normalcolor.xaml", UriKind.Relative);
                else if (t == "Aero Lite")
                    uri = new Uri("PresentationFramework.AeroLite;V3.0.0.0;31bf3856ad364e35;component\\themes/aerolite.normalcolor.xaml", UriKind.Relative);
                else if (t == "Classic")
                    uri = new Uri("PresentationFramework.Classic;V3.0.0.0;31bf3856ad364e35;component\\themes/classic.xaml", UriKind.Relative);
                else if (t == "Luna")
                    uri = new Uri("PresentationFramework.Luna;V3.0.0.0;31bf3856ad364e35;component\\themes/luna.normalcolor.xaml", UriKind.Relative);
                else if (t == "Luna Olive Green")
                    uri = new Uri("PresentationFramework.Luna;V3.0.0.0;31bf3856ad364e35;component\\themes/luna.homestead.xaml", UriKind.Relative);
                else if (t == "Luna Silver")
                    uri = new Uri("PresentationFramework.Luna;V3.0.0.0;31bf3856ad364e35;component\\themes/luna.metallic.xaml", UriKind.Relative);
                else if (t == "Royale")
                    uri = new Uri("PresentationFramework.Royale;V3.0.0.0;31bf3856ad364e35;component\\themes/royale.normalcolor.xaml", UriKind.Relative);
                else if (t.Length > 0)
                    path = Path.Combine(Global.BuzzPath, "Themes/Default/WPFThemes/" + t.Replace(" ", "") + ".xaml");

                if (uri != null)
                {
                    var rd = Application.LoadComponent(uri) as ResourceDictionary;
                    if (rd != null) app.Resources.MergedDictionaries.Add(rd);
                }
                else if (path != null)
                {
                    var rd = XamlReaderEx.LoadHack(path) as ResourceDictionary;
                    app.Resources.MergedDictionaries.Add(rd);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }

        }
    }
}
