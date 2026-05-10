using System.IO;
using System.Linq;
using Cryptography = System.Security.Cryptography;
using System.Threading.Tasks;

namespace BuzzGUI.Common
{
    public static class SHA1
    {
        static Cryptography.SHA1? sha;

        public static Task<string> ComputeForFileTaskAsync(string path)
        {
            if (sha == null)
                sha = Cryptography.SHA1.Create();

            return Task.Factory.StartNew(() =>
            {
                using (var fs = File.OpenRead(path))
                    return string.Join("", sha.ComputeHash(fs).Select(b => b.ToString("X2")));
            });
        }
    }
}
