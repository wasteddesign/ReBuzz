using System.ComponentModel;
using System.Reflection;

namespace BuzzGUI.Common
{
    public static class PropertyChangedRaiser
    {
        public static void Raise(this PropertyChangedEventHandler p, INotifyPropertyChanged x, string propertyname)
        {
            /*
#if DEBUG
            if (x.GetType().GetProperty(propertyname, BindingFlags.Public | BindingFlags.Instance) == null)
                Debug.Fail(string.Format("PropertyChangedRaiser: Invalid Property Name '{0}' in '{1}'.", propertyname, x.GetType().Name));
#endif
            */
            if (p != null)
                p(x, new PropertyChangedEventArgs(propertyname));

        }

        public static void RaiseAll(this PropertyChangedEventHandler p, INotifyPropertyChanged x)
        {
            if (p == null) return;

            foreach (var pi in x.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                p(x, new PropertyChangedEventArgs(pi.Name));
        }

    }
}
