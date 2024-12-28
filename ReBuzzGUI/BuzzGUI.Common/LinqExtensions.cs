using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace BuzzGUI.Common
{
    public static class LinqExtensions
    {
        public static IEnumerable<T> Traverse<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> fnRecurse)
        {
            foreach (T item in source)
            {
                yield return item;

                IEnumerable<T> seqRecurse = fnRecurse(item);

                if (seqRecurse != null)
                {
                    foreach (T itemRecurse in Traverse(seqRecurse, fnRecurse))
                        yield return itemRecurse;

                }
            }

        }

        public static void Remove<K, V>(this IDictionary<K, V> d, Func<K, bool> p)
        {
            foreach (var k in d.Keys.Where(k => p(k)).ToArray())
                d.Remove(k);
        }

        public static ReadOnlyCollection<T> ToReadOnlyCollection<T>(this IEnumerable<T> source)
        {
            return source.ToList().AsReadOnly();
        }

        public static IEnumerable<R> SelectTwo<T, R>(this IEnumerable<T> source, Func<T, R> firstselector, Func<T, R> secondselector)
        {
            using (var i = source.GetEnumerator())
            {
                while (i.MoveNext())
                {
                    yield return firstselector(i.Current);
                    yield return secondselector(i.Current);
                }
            }
        }

        public static IEnumerable<R> SelectFromTwo<T, R>(this IEnumerable<T> source, Func<T, T, R> selector)
        {
            using (var i = source.GetEnumerator())
            {
                while (i.MoveNext())
                {
                    var first = i.Current;
                    if (!i.MoveNext()) break;
                    yield return selector(first, i.Current);
                }
            }
        }

        public static int FindIndex<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> condition)
        {
            int index = 0;

            foreach (var e in source)
            {
                if (condition(e)) break;
                index++;
            }

            return index;
        }

        public static int FindIndex<TSource, TState>(this IEnumerable<TSource> source, TState seed, Func<TState, TSource, TState> select, Func<TState, bool> condition) where TState : struct
        {
            int index = 0;

            TState state = seed;

            foreach (var e in source)
            {
                state = select(state, e);
                if (condition(state)) break;
                index++;
            }

            return index;
        }


        public static void FindAndExecute<TSource, TState>(this IEnumerable<TSource> source, TState seed, Func<TState, TSource, TState> select, Func<TState, bool> condition, Action<TSource> action) where TState : struct
        {
            TState state = seed;

            foreach (var e in source)
            {
                state = select(state, e);
                if (condition(state))
                {
                    action(e);
                    break;
                }
            }

        }

        public static HashSet<T> ToHashSetE<T>(this IEnumerable<T> source)
        {
            return new HashSet<T>(source);
        }

        // like SingleOrDefault but also returns default when there's more than one element instead of throwing
        public static T OnlyOrDefault<T>(this IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException("source");

            IList<T> list = source as IList<T>;
            if (list != null)
            {
                switch (list.Count)
                {
                    case 0: return default(T);
                    case 1: return list[0];
                }
            }
            else
            {
                using (IEnumerator<T> e = source.GetEnumerator())
                {
                    if (!e.MoveNext()) return default(T);
                    T result = e.Current;
                    if (!e.MoveNext()) return result;
                }
            }

            return default(T);
        }

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            return source.Shuffle(new Random());
        }

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Random rng)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (rng == null) throw new ArgumentNullException("rng");

            return source.ShuffleIterator(rng);
        }

        private static IEnumerable<T> ShuffleIterator<T>(this IEnumerable<T> source, Random rng)
        {
            List<T> buffer = source.ToList();
            for (int i = 0; i < buffer.Count; i++)
            {
                int j = rng.Next(i, buffer.Count);
                yield return buffer[j];

                buffer[j] = buffer[i];
            }
        }

        public static IEnumerable<T> Return<T>(T value)
        {
            yield return value;
        }

        public static IEnumerable<T> Do<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var i in source)
            {
                action(i);
                yield return i;
            }
        }

        public static IEnumerable<T> Do<T>(this IEnumerable<T> source, Action<T, int> action)
        {
            int index = 0;

            foreach (var i in source)
            {
                action(i, index++);
                yield return i;
            }
        }

        public static void Run<T>(this IEnumerable<T> source)
        {
            foreach (var i in source) ;
        }

        public static void Run<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var i in source)
                action(i);
        }

        public static void Run<T>(this IEnumerable<T> source, Action<T, int> action)
        {
            int index = 0;

            foreach (var i in source)
                action(i, index++);
        }

        public static void Run<T, U>(this IEnumerable<T> source, Action<U> action) where U : T
        {
            foreach (var i in source)
            {
                if (i is U)
                    action((U)i);
            }
        }

        public static IEnumerable<int> RangeExcludingEnd(int start, int end, int step)
        {
            for (int i = start; i < end; i += step)
                yield return i;
        }

        public static IEnumerable<int> RangeIncludingEnd(int start, int end, int step)
        {
            for (int i = start; i <= end; i += step)
                yield return i;
        }

        public static IEnumerable<double> RangeExcludingEnd(double start, double end, double step)
        {
            if (step == 0) throw new ArgumentException("step");

            int n = (int)Math.Ceiling((end - start) / step);

            for (int i = 0; i < n; i++)
                yield return start + i * step;
        }

        public static IEnumerable<double> RangeIncludingEnd(double start, double end, double step)
        {
            return RangeExcludingEnd(start, end, step).Concat(Return(end));
        }

        // ignores empty lists
        public static IEnumerable<IEnumerable<T>> Split<T>(this IEnumerable<T> source, T delimiter)
        {
            var list = new List<T>();

            foreach (var i in source)
            {
                if ((i == null && delimiter == null) || (i != null && i.Equals(delimiter)))
                {
                    if (list.Count > 0)
                    {
                        yield return list;
                        list.Clear();
                    }
                }
                else
                {
                    list.Add(i);
                }
            }

            if (list.Count > 0)
                yield return list;
        }

        public static int IndexOfMinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector)
        {
            int index = 0;
            TKey min = default(TKey);
            bool first = true;
            int minIndex = 0;

            foreach (var e in source)
            {
                var k = selector(e);
                if (first || Comparer<TKey>.Default.Compare(k, min) < 0)
                {
                    min = k;
                    minIndex = index;
                    first = false;
                }
                index++;
            }

            return minIndex;
        }


    }
}
