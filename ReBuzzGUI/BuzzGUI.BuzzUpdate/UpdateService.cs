using BuzzGUI.Interfaces;
using System.Diagnostics;

namespace BuzzGUI.BuzzUpdate
{
    public class UpdateService
    {

        public static void CheckForUpdates(IBuzz buzz)
        {
            var sw = new Stopwatch();
            sw.Start();
            UpdateWindow.DownloadBuildCount(buzz);
            sw.Stop();
            buzz.DCWriteLine(string.Format("CheckForUpdates {0} ms", sw.ElapsedMilliseconds));
        }


    }
}
