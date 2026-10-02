using BuzzGUI.Interfaces;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Windows;

namespace BuzzGUI.BuzzUpdate
{
    public partial class UpdateWindow : Window
    {
        static readonly HttpClient httpClient = new HttpClient(
            new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            });

        static string downloadUrl;
        static string releaseNotes;
        static int currentBuild;
        static int latestBuild;
        string localFile;
        string localSignatureFile;

        static string setupUrl { get { return "https://github.com/wasteddesign/ReBuzz/releases/latest"; } }
        string setupExe { get { return "ReBuzzSetup_Preview_"; } }

        static UpdateWindow()
        {
            // GitHub requires a User-Agent header
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ReBuzzUpdater/1.0");
        }

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

            msv.OnHyperLinkClicked += (link) =>
            {
                if (link != null)
                {
                    Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
                }
            };
            verText.Text = "Current Build: " + currentBuild.ToString() + "   Latest Build: " + latestBuild.ToString();

            msv.Markdown = "Downloading changelog...";
            DownloadChangelog();
        }

        public static void DownloadBuildCount(IBuzz buzz)
        {
            currentBuild = buzz.BuildNumber;
            _ = DownloadBuildCountAsync(buzz);
        }

        private static async Task DownloadBuildCountAsync(IBuzz buzz)
        {
            string urlLatestRelease = "https://api.github.com/repos/wasteddesign/ReBuzz/releases/latest";

            try
            {
                var jsonString = await httpClient.GetStringAsync(urlLatestRelease);
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
            catch (Exception e)
            {
                buzz.DCWriteLine("[BuzzUpdate] " + e.ToString());
            }
        }

        async void DownloadChangelog()
        {
            string urlLatestRelease = "https://api.github.com/repos/wasteddesign/ReBuzz/releases/latest";

            try
            {
                var jsonString = await httpClient.GetStringAsync(urlLatestRelease);
                JsonNode releasesNode = JsonNode.Parse(jsonString)!;
                releaseNotes = (releasesNode!["body"]!).ToString();

                msv.Markdown = releaseNotes;
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

        async void DownloadInstaller()
        {
            progressBar.Visibility = Visibility.Visible;
            button.IsEnabled = false;

            try
            {
                string exename = Path.GetFileName(downloadUrl);
                localFile = System.IO.Path.GetTempPath() + exename;

                using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var total = response.Content.Headers.ContentLength ?? -1L;
                    bool canReportProgress = total > 0;

                    using (var input = await response.Content.ReadAsStreamAsync())
                    using (var output = File.Create(localFile))
                    {
                        var buffer = new byte[81920];
                        long totalRead = 0;
                        int read;

                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await output.WriteAsync(buffer, 0, read);
                            totalRead += read;

                            if (canReportProgress)
                            {
                                progressBar.Value = (int)((totalRead * 100) / total);
                            }
                        }
                    }
                }

                progressBar.Value = 0;
                progressBar.Visibility = Visibility.Collapsed;

                button.Content = "Install...";
                button.IsEnabled = true;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "ReBuzz Update");
                progressBar.Visibility = Visibility.Collapsed;
                button.IsEnabled = true;
            }
        }

        async void DownloadSignature()
        {
            progressBar.Visibility = Visibility.Visible;
            button.IsEnabled = false;

            try
            {
                string signaturename = setupExe + latestBuild.ToString() + ".exe.ecdsa";
                localSignatureFile = System.IO.Path.GetTempPath() + signaturename;

                string url = setupUrl + "signatures/" + signaturename;

                using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var total = response.Content.Headers.ContentLength ?? -1L;
                    bool canReportProgress = total > 0;

                    using (var input = await response.Content.ReadAsStreamAsync())
                    using (var output = File.Create(localSignatureFile))
                    {
                        var buffer = new byte[81920];
                        long totalRead = 0;
                        int read;

                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await output.WriteAsync(buffer, 0, read);
                            totalRead += read;

                            if (canReportProgress)
                            {
                                progressBar.Value = (int)((totalRead * 100) / total);
                            }
                        }
                    }
                }

                progressBar.Value = 0;
                progressBar.Visibility = Visibility.Collapsed;

                DownloadInstaller();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "ReBuzz Update");
                progressBar.Visibility = Visibility.Collapsed;
                button.IsEnabled = true;
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