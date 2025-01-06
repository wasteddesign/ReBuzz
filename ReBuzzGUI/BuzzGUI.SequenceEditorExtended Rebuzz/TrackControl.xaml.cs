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
using BuzzGUI.Interfaces;

namespace BuzzGUI.SequenceEditor
{
	/// <summary>
	/// Interaction logic for TrackControl.xaml
	/// </summary>
	public partial class TrackControl : UserControl
	{
		ViewSettings viewSettings;
		public ViewSettings ViewSettings
		{
			set
			{
				viewSettings = value;
				//backgroundElement.ViewSettings = value;
			}

		}

		ISequence sequence;
		public ISequence Sequence
		{
			get { return sequence; }
			set
			{
				if (sequence != null)
				{
					sequence.PropertyChanged -= sequence_PropertyChanged;
					sequence.EventChanged -= sequence_EventChanged;
					sequence.SpanInserted -= sequence_SpanInserted;
					sequence.SpanDeleted -= sequence_SpanDeleted;
					sequence.SpanCleared -= sequence_SpanCleared;
				}

				sequence = value;
				EventsChanged();

				if (sequence != null)
				{
					sequence.PropertyChanged += sequence_PropertyChanged;
					sequence.EventChanged += sequence_EventChanged;
					sequence.SpanInserted += sequence_SpanInserted;
					sequence.SpanDeleted += sequence_SpanDeleted;
					sequence.SpanCleared += sequence_SpanCleared;
				}
			}
		}

		void sequence_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case "Events":
					EventsChanged();
					break;
			}
		}

		void sequence_SpanInserted(int time, int span)
		{
			if (!IsVisible)
			{
				EventsChanged();
				return;
			}
			else
			{
				for (int i = 0; i < eventCanvas.Children.Count; i++)
				{
					var e = eventCanvas.Children[i] as PatternElement;
					if (e.time >= time)
					{
						e.time += span;
						Canvas.SetLeft(e, e.time * viewSettings.TickWidth);
					}
				}
			}

		}

		void sequence_SpanDeleted(int time, int span)
		{
			if (!IsVisible)
			{
				EventsChanged();
				return;
			}
			else
			{
				int rfirst = -1;
				int rcount = 0;

				for (int i = 0; i < eventCanvas.Children.Count; i++)
				{
					var e = eventCanvas.Children[i] as PatternElement;
					if (e.time >= time)
					{
						if (e.time < time + span)
						{
							if (rfirst < 0) rfirst = i;
							rcount++;
						}
						else
						{
							e.time -= span;
							Canvas.SetLeft(e, e.time * viewSettings.TickWidth);
						}
					}
				}

				if (rfirst >= 0)
					eventCanvas.Children.RemoveRange(rfirst, rcount);

			}
		}

		void sequence_SpanCleared(int time, int span)
		{
			if (!IsVisible)
			{
				EventsChanged();
				return;
			}
			else
			{
				int rfirst = -1;
				int rcount = 0;

				for (int i = 0; i < eventCanvas.Children.Count; i++)
				{
					var e = eventCanvas.Children[i] as PatternElement;
					if (e.time >= time)
					{
						if (e.time < time + span)
						{
							if (rfirst < 0) rfirst = i;
							rcount++;
						}
					}
				}

				if (rfirst >= 0)
					eventCanvas.Children.RemoveRange(rfirst, rcount);

			}
		}

		public SequenceEditor Editor { get; private set; }

		bool updatePending = false;

		public TrackControl(SequenceEditor se)
		{
			Editor = se;
			if (Editor.ResourceDictionary != null) this.Resources.MergedDictionaries.Add(Editor.ResourceDictionary);
			InitializeComponent();

			this.IsVisibleChanged += (sender, e) =>
			{
				if (IsVisible)
				{
					if (updatePending)
					{
						updatePending = false;
						EventsChanged();
					}
				}
			};

		}



		public void EventsChanged()
		{
			if (!IsVisible)
			{
				updatePending = true;
				return;
			}

			eventCanvas.Children.Clear();
			if (sequence != null)
			{
				foreach (var e in sequence.Events)
				{
					var pv = new PatternElement(this, e.Key, e.Value, viewSettings);
					Canvas.SetLeft(pv, e.Key * viewSettings.TickWidth);
					eventCanvas.Children.Add(pv);
				}
			}
		}

		void sequence_EventChanged(int time)
		{
			SequenceEvent e;
			sequence.Events.TryGetValue(time, out e);

			for (int i = 0; i < eventCanvas.Children.Count; i++)
			{
				if ((eventCanvas.Children[i] as PatternElement).time == time)
				{
					eventCanvas.Children.RemoveAt(i);
					break;
				}
			}

			if (e != null)
			{
				int i = 0;
				for (i = 0; i < eventCanvas.Children.Count; i++)
				{
					if ((eventCanvas.Children[i] as PatternElement).time > time)
						break;
				}

				var pv = new PatternElement(this, time, e, viewSettings);
				Canvas.SetLeft(pv, time * viewSettings.TickWidth);
				eventCanvas.Children.Insert(i, pv);
			}
		}

	}
}
