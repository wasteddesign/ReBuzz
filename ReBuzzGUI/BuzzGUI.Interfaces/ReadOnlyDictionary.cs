using System;
using System.Collections.Generic;

namespace BuzzGUI.Interfaces
{
    public class ReadOnlyDictionary<K, V> : IDictionary<K, V>
    {
        readonly IDictionary<K, V> d;

        public IDictionary<K, V> Dictionary { get { return d; } }

        public ReadOnlyDictionary(IDictionary<K, V> d) { this.d = d; }

        #region IDictionary<K,V> Members

        public bool ContainsKey(K key) { return d.ContainsKey(key); }
        public ICollection<K> Keys { get { return d.Keys; } }
        public bool TryGetValue(K key, out V value) { return d.TryGetValue(key, out value); }
        public ICollection<V> Values { get { return d.Values; } }
        public V this[K key] { get { return d[key]; } set { throw new NotSupportedException(); } }

        public void Add(K key, V value) { throw new NotSupportedException(); }
        public bool Remove(K key) { throw new NotSupportedException(); }

        #endregion

        #region ICollection<KeyValuePair<K,V>> Members

        public bool Contains(KeyValuePair<K, V> item) { return d.Contains(item); }
        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex) { d.CopyTo(array, arrayIndex); }
        public int Count { get { return d.Count; } }
        public bool IsReadOnly { get { return true; } }

        public void Add(KeyValuePair<K, V> item) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public bool Remove(KeyValuePair<K, V> item) { throw new NotSupportedException(); }

        #endregion

        #region IEnumerable<KeyValuePair<K,V>> Members

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() { return d.GetEnumerator(); }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return d.GetEnumerator(); }

        #endregion
    }

    public static class IDictionaryExtensions
    {
        public static ReadOnlyDictionary<K, V> AsReadOnly<K, V>(this IDictionary<K, V> d)
        {
            return new ReadOnlyDictionary<K, V>(d);
        }
    }
}
