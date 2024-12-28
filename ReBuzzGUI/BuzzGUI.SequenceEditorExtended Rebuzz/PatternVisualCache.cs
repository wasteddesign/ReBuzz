using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Globalization;
using BuzzGUI.Interfaces;

namespace BuzzGUI.SequenceEditor
{
	static class PatternVisualCache
	{
		static Dictionary<Tuple<string, double, int>, BitmapCacheBrush> cache = new Dictionary<Tuple<string, double, int>, BitmapCacheBrush>();

		public static void Clear()
		{
			cache.Clear();
		}

		public static BitmapCacheBrush Lookup(string text, double width, int colorindex)
		{
			BitmapCacheBrush br;
			cache.TryGetValue(Tuple.Create(text, width, colorindex), out br);
			return br;
		}

		public static void Cache(string text, double width, int colorindex, BitmapCacheBrush br)
		{
			cache[Tuple.Create(text, width, colorindex)] = br;
		}
		
	}
}
