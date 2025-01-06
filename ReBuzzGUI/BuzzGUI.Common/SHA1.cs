using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace BuzzGUI.Common
{
    public static class SHA1
    {
        static SHA1CryptoServiceProvider sha;

        public static Task<string> ComputeForFileTaskAsync(string path)
        {
            if (sha == null)
                sha = new SHA1CryptoServiceProvider();

            return Task.Factory.StartNew(() =>
            {
                using (var fs = File.OpenRead(path))
                    return string.Join("", sha.ComputeHash(fs).Select(b => b.ToString("X2")));
            });
        }
    }
}
