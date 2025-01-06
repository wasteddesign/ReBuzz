namespace BuzzGUI.Common
{
    public static class ReflectionEx
    {
        public static T GetPropertyOrDefault<T>(this object o, string propertyName)
        {
            var t = o.GetType();
            var p = t.GetProperty(propertyName);
            if (p == null) return default(T);
            var get = p.GetGetMethod();
            if (get == null) return default(T);
            var r = get.Invoke(o, new object[] { });
            return (T)r;
        }
    }
}
