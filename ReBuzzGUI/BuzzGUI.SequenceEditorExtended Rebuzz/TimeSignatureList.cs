using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace BuzzGUI.SequenceEditor
{
	public class TimeSignatureList
	{
		SortedDictionary<int, int> data = new SortedDictionary<int, int>();
		public event Action Changed;

		public TimeSignatureList()
		{
			data[0] = 16;
		}

		public TimeSignatureList(TimeSignatureList x)
		{
			Clone(x);
		}

		public void Clone(TimeSignatureList tsl)
		{
			data.Clear();
			foreach (var x in tsl.data)
				data[x.Key] = x.Value;

			if (Changed != null) Changed();
		}

		public TimeSignatureList(BinaryReader br)
		{
			int count = br.ReadInt32();
			for (int i = 0; i < count; i++)
			{
				int key = br.ReadInt32();
				int value = br.ReadInt32();
				data[key] = value;
			}
		}

		public void Write(BinaryWriter bw)
		{
			bw.Write(data.Count);
			foreach (var e in data)
			{
				bw.Write(e.Key);
				bw.Write(e.Value);
			}
		}

		public void Set(int time, int step)
		{
			data[time] = step;
			Cleanup();
			if (Changed != null) Changed();
		}

		public void Insert(int time, int span)
		{
			var newdata = new SortedDictionary<int, int>();
			foreach (var e in data)
			{
				if (e.Key <= time)
					newdata[e.Key] = e.Value;
				else
					newdata[e.Key + span] = e.Value;
			}

			data = newdata;
			if (Changed != null) Changed();
		}

		public void Delete(int time, int span)
		{
			var newdata = new SortedDictionary<int, int>();
			foreach (var e in data)
			{
				if (e.Key <= time)
					newdata[e.Key] = e.Value;
				else if (e.Key >= time + span)
					newdata[e.Key - span] = e.Value;
			}

			data = newdata;
			Cleanup();
			if (Changed != null) Changed();
		}

		void Cleanup()
		{
			var newdata = new SortedDictionary<int, int>();
			newdata[0] = data[0];

			int t = 0;
			int step = data[0];

			foreach (var e in data)
			{
				if (e.Key > 0)
				{
					int lastspan = (e.Key - t) % step;
					if (lastspan != 0)
					{
						t = e.Key - lastspan;
						step = lastspan;
						newdata[t] = step;
					}

					if (e.Value != step)
					{
						t = e.Key;
						step = e.Value;
						newdata[t] = step;
					}
				}
			}

			data = newdata;
		}

		public bool TimeSignatureChangesAt(int time)
		{
			return data.ContainsKey(time);
		}

		public IEnumerable<Tuple<int, int>> GetBars(int end)
		{
			var e = data.GetEnumerator();
			e.MoveNext();
			int nextt;

			do
			{
				var current = e.Current;
				nextt = e.MoveNext() ? e.Current.Key : end;
				for (int t = current.Key; t < nextt; t += current.Value) yield return Tuple.Create(t, Math.Min(current.Value, nextt - t));
			} while (nextt < end);

		}

		public int GetBarLengthAt(int time)
		{
			foreach (var x in GetBars(int.MaxValue))
			{
				if (time >= x.Item1 && time < x.Item1 + x.Item2)
					return x.Item2;
			}

			return 16;
		}

		public int Snap(long time, int end)
		{
			if (time < 0) return 0;

			int last = 0;

			foreach (var x in GetBars(end))
			{
				if (time >= x.Item1 && time < x.Item1 + x.Item2)
					return x.Item1;

				last = x.Item1;
			}

			return last;
		}

	}
}
