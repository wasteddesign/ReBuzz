using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Data;

namespace BuzzGUI.ParameterWindow
{
    public class ParameterListSlicer : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            const string errormsg = "ConverterParameter must be of form 'a:b' where a and b are integers.";

            if (value == null) return null;

            if (parameter == null)
                throw new ArgumentException(errormsg);

            string s = (string)parameter;
            string[] ss = s.Split(':');
            if (ss.Length != 2)
                throw new ArgumentException(errormsg);

            int first, last;
            try
            {
                first = System.Convert.ToInt32(ss[0]);
                last = System.Convert.ToInt32(ss[1]);
            }
            catch (FormatException)
            {
                throw new ArgumentException(errormsg);
            }

            int step = (first <= last) ? +1 : -1;

            ReadOnlyCollection<ParameterVM> p = (ReadOnlyCollection<ParameterVM>)value;
            List<ParameterVM> ret = new List<ParameterVM>();
            for (int i = first; i != last + step; i += step)
            {
                if (i >= 0 && i < p.Count) ret.Add(p[i]);
            }

            return new ReadOnlyCollection<ParameterVM>(ret);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
