using System;
using System.Net;
using System.Windows;

namespace BuzzGUI.Common
{
    public class WebClientEx : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            HttpWebRequest request = base.GetWebRequest(address) as HttpWebRequest;
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            return request;
        }

        public static void Download(Uri uri, string localPath, Action<int> progress = null, Action continuation = null)
        {
            WebRequest.DefaultWebProxy = null;
            var wc = new WebClientEx();

            wc.Headers.Add("user-agent", "Buzz " + Global.Buzz.BuildNumber.ToString());
            wc.DownloadFileCompleted += (sender, e) =>
            {
                if (!e.Cancelled && e.Error == null)
                {
                    if (continuation != null)
                        continuation();
                }
                else if (e.Error != null)
                {
                    Error(uri);
                }
            };
            wc.DownloadProgressChanged += (sender, e) =>
            {
                if (progress != null)
                    progress(e.ProgressPercentage);
            };

            try
            {
                wc.DownloadFileAsync(uri, localPath);
            }
            catch (Exception)
            {
                Error(uri);
            }

        }

        static void Error(Uri uri)
        {
            MessageBox.Show(string.Format("Unable to download '{0}'", uri), "Buzz", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
