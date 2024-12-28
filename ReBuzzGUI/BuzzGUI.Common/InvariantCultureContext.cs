using System;
using System.Globalization;
using System.Threading;

namespace BuzzGUI.Common
{
    public class InvariantCultureContext : IDisposable
    {
        readonly CultureInfo oldCulture;

        public InvariantCultureContext()
        {
            oldCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        }

        public void Dispose()
        {
            Thread.CurrentThread.CurrentCulture = oldCulture;
        }
    }
}
