using System;
using System.Collections.Generic;

namespace BuzzGUI.Common
{
    public class CountSet<T>
    {
        readonly Dictionary<T, int> dictionary = new Dictionary<T, int>();

        public int Increase(T key)
        {
            int value;
            if (dictionary.TryGetValue(key, out value))
            {
                dictionary[key] = value + 1;
                return value + 1;
            }
            else
            {
                dictionary[key] = 1;
                return 1;
            }
        }

        public int Decrease(T key)
        {
            int value;
            if (dictionary.TryGetValue(key, out value))
            {
                if (value > 1)
                {
                    dictionary[key] = value - 1;
                    return value - 1;
                }
                else
                {
                    dictionary.Remove(key);
                    return 0;
                }
            }
            else
            {
                throw new Exception("negative count");
            }
        }

        public int GetCount(T key)
        {
            int value;
            if (dictionary.TryGetValue(key, out value))
                return value;
            else
                return 0;
        }
    }
}
