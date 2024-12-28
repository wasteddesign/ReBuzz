using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.IO;

namespace BuzzGUI.Common
{
    public static class ZipFileEx
    {
        public static string UnzipString(string zipfile, string filename)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var zf = new ZipFile(zipfile);
            var ze = new ZipEntry(filename);
            using (var sr = new StreamReader(zf.GetInputStream(ze)))
            {
                return sr.ReadToEnd();
            }

        }

        public static void UnzipToPath(string zipfile, string path)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var zf = new ZipFile(zipfile);
            byte[] buffer = new byte[4096];     // 4K is optimum

            foreach (ZipEntry ze in zf)
            {
                if (!ze.IsFile)
                    continue;

                var entryFileName = ze.Name;
                var zipStream = zf.GetInputStream(ze);

                String fullZipToPath = Path.Combine(path, entryFileName);
                string directoryName = Path.GetDirectoryName(fullZipToPath);
                if (directoryName.Length > 0)
                    Directory.CreateDirectory(directoryName);

                using (FileStream streamWriter = File.Create(fullZipToPath))
                    StreamUtils.Copy(zipStream, streamWriter, buffer);
            }
        }


    }
}
