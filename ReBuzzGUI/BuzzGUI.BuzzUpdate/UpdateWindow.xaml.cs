using BuzzGUI.Interfaces;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text.Json.Nodes;
using System.Windows;

namespace BuzzGUI.BuzzUpdate
{
    public partial class UpdateWindow : Window
    {
        static string UserAgentString;
        static string downloadUrl;
        static string releaseNotes;
        static int currentBuild;
        static int latestBuild;
        string localFile;
        string localSignatureFile;

        static string setupUrl { get { return "https://github.com/wasteddesign/ReBuzz/releases/latest"; } }
        string setupExe { get { return "ReBuzzSetup_2024_Preview_"; } }

        static int ParseBuildNumber(string s)
        {
            s = Path.ChangeExtension(s, null);

            int x = 0;

            int lastDigitIndex = s.LastIndexOfAny("0123456789".ToCharArray());
            if (lastDigitIndex != -1)
            {
                int startIndex = lastDigitIndex;
                while (startIndex >= 0 && char.IsDigit(s[startIndex]))
                {
                    startIndex--;
                }
                startIndex++;
                string lastNumber = s.Substring(startIndex, lastDigitIndex - startIndex + 1);
                int.TryParse(lastNumber, out x);
            }

            return x;
        }

        public UpdateWindow(int latestBuild)
        {
            DataContext = this;
            InitializeComponent();

            verText.Text = "Current Build: " + currentBuild.ToString() + "   Latest Build: " + latestBuild.ToString();

            changelogBox.Text = "Downloading changelog...";
            DownloadChangelog();

        }

        public static void DownloadBuildCount(IBuzz buzz)
        {
            currentBuild = buzz.BuildNumber;
            //UserAgentString = "Buzz Update " + currentBuild.ToString();
            string urlLatestRelease = "https://api.github.com/repos/wasteddesign/ReBuzz/releases/latest";

            WebClient client = new WebClient();
            client.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:25.0) Gecko/20100101 Firefox/25.0"); 
            client.DownloadStringCompleted += (sender, e) =>
            {
                if (!e.Cancelled && e.Error == null)
                {
                    var jsonString = e.Result;
                    JsonNode releasesNode = JsonNode.Parse(jsonString)!;
                    JsonNode assetsNode = releasesNode!["assets"]!;
                    JsonNode installerAsset = assetsNode[0]!;
                    JsonNode urlNode = installerAsset!["browser_download_url"]!;
                    downloadUrl = urlNode.ToString();

                    latestBuild = ParseBuildNumber(downloadUrl);

                    releaseNotes = (releasesNode!["body"]!).ToString();

                    if (currentBuild >= latestBuild)
                    {
                        buzz.DCWriteLine("[BuzzUpdate] No updates available.");
                    }
                    else
                    {
                        UpdateWindow w = new UpdateWindow(latestBuild);
                        w.Show();
                    }
                }
                else if (e.Error != null)
                {
                    buzz.DCWriteLine("[BuzzUpdate] " + e.Error.ToString());
                }
            };
            try
            {
                client.DownloadStringAsync(new Uri(urlLatestRelease));
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "ReBuzz Update");
            }

        }


        void DownloadChangelog()
        {
            string urlLatestRelease = "https://api.github.com/repos/wasteddesign/ReBuzz/releases/latest";

            WebClient client = new WebClient();
            client.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:25.0) Gecko/20100101 Firefox/25.0");
            client.DownloadStringCompleted += (sender, e) =>
            {
                if (!e.Cancelled && e.Error == null)
                {
                    var jsonString = e.Result;
                    JsonNode releasesNode = JsonNode.Parse(jsonString)!;
                    releaseNotes = (releasesNode!["body"]!).ToString();

                    changelogBox.Text = @"Release Notes:

" + releaseNotes + @"

Go to https://github.com/wasteddesign/ReBuzz/releases/latest for more information.";
                }
            };
            try
            {
                client.DownloadStringAsync(new Uri(urlLatestRelease));
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "ReBuzz Update");
            }
        }

        void VerifySignature()
        {
            var signature = File.ReadAllBytes(localSignatureFile);
            var key = CngKey.Import(Resource1.InstallerSignKey, CngKeyBlobFormat.EccPublicBlob);
            var dsa = new ECDsaCng(key);
            using (var fs = File.OpenRead(localFile))
            {
                if (!dsa.VerifyData(fs, signature))
                    throw new Exception();
            }
        }

        void DownloadInstaller()
        {
            WebClient wc = new WebClient();
            wc.Headers.Add("user-agent", "Other");
            wc.DownloadFileCompleted += (sender, e) =>
            {
                if (!e.Cancelled && e.Error == null)
                {
                    progressBar.Value = 0;
                    progressBar.Visibility = Visibility.Collapsed;

                    button.Content = "Install...";
                    button.IsEnabled = true;
                }
                else if (e.Error != null)
                {
                    MessageBox.Show(e.Error.ToString(), "ReBuzz Update");
                }
            };
            wc.DownloadProgressChanged += (sender, e) =>
            {
                progressBar.Value = e.ProgressPercentage;
            };

            progressBar.Visibility = Visibility.Visible;

            try
            {
                string exename = Path.GetFileName(downloadUrl);
                localFile = System.IO.Path.GetTempPath() + exename;

                wc.DownloadFileAsync(new Uri(downloadUrl), localFile);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "ReBuzz Update");
            }
        }

        void DownloadSignature()
        {
            WebClient wc = new WebClient();
            wc.Headers.Add("user-agent", UserAgentString);
            wc.DownloadFileCompleted += (sender, e) =>
            {
                if (!e.Cancelled && e.Error == null)
                {
                    progressBar.Value = 0;
                    progressBar.Visibility = Visibility.Collapsed;
                    DownloadInstaller();
                }
                else if (e.Error != null)
                {
                    MessageBox.Show(e.Error.ToString(), "ReBuzz Update");
                }
            };
            wc.DownloadProgressChanged += (sender, e) =>
            {
                progressBar.Value = e.ProgressPercentage;
            };

            progressBar.Visibility = Visibility.Visible;

            try
            {
                string signaturename = setupExe + latestBuild.ToString() + ".exe.ecdsa";
                localSignatureFile = System.IO.Path.GetTempPath() + signaturename;

                wc.DownloadFileAsync(new Uri(setupUrl + "signatures/" + signaturename), localSignatureFile);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "ReBuzz Update");
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (localFile == null)
            {
                button.IsEnabled = false;
                //DownloadSignature();
                DownloadInstaller();
            }
            else
            {

                Process p = new Process();
                p.StartInfo.FileName = localFile;
                p.StartInfo.UseShellExecute = true;
                p.StartInfo.Arguments = "/silent";
                p.Start();

                Process.GetCurrentProcess().Kill();
            }
        }

    }
}
