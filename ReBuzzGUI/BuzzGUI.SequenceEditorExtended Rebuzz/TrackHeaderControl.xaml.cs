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
using System.ComponentModel;
using BuzzGUI.Interfaces;
using BuzzGUI.Common;

namespace BuzzGUI.SequenceEditor
{
	/// <summary>
	/// Interaction logic for TrackHeaderControl.xaml
	/// </summary>
	public partial class TrackHeaderControl : UserControl, INotifyPropertyChanged
	{
		ViewSettings viewSettings;
		public ViewSettings ViewSettings
		{
			set
			{
				viewSettings = value;
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
					sequence.Machine.PropertyChanged -= Machine_PropertyChanged;
				}

				sequence = value;
				DataContext = sequence;
				UpdatePatternList();

				if (sequence != null)
				{
					sequence.PropertyChanged += sequence_PropertyChanged;
					sequence.Machine.PropertyChanged += Machine_PropertyChanged;
				}
			}
		}

		void Machine_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "Patterns")
				UpdatePatternList();
		}

		void sequence_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
		}

		bool isSelected;
		public bool IsSelected 
		{ 
			get { return isSelected; } 
			set 
			{ 
				isSelected = value; 
				PropertyChanged.Raise(this, "IsSelected");
				Background = IsSelected ? backgroundSelectedBrush : backgroundBrush;
			}
 
		}

		public class PatternListItem
		{
			public IPattern Pattern { get; private set; }
			public char Char { get; private set; }

			public PatternListItem(IPattern pattern, char ch)
			{
				Pattern = pattern;
				Char = ch;
			}

			public override string ToString()
			{
				return string.Format("{0}. {1}", Char, Pattern.Name);
			}
		}

		List<PatternListItem> patternList;
		public IList<PatternListItem> PatternList { get { return patternList; } }

		void UpdatePatternList()
		{
			patternList = new List<PatternListItem>();
			if (sequence == null) return;

			char ch = '0';

			foreach (var p in sequence.Machine.Patterns.OrderBy(x => x.Name))
			{
				patternList.Add(new PatternListItem(p, ch));

				ch++;
				if (ch - 1 == '9')
					ch = 'a';

				if (ch - 1 == 'z')
					ch = 'A';
			}

			PropertyChanged.Raise(this, "PatternList");
		}

		public IPattern GetPatternByChar(char ch)
		{
			if (sequence == null) return null;
			return patternList.Where(i => i.Char == ch).Select(i => i.Pattern).FirstOrDefault();
		}

		public SequenceEditor Editor { get; private set; }

		Brush backgroundBrush;
		Brush backgroundSelectedBrush;

		public TrackHeaderControl(SequenceEditor se)
		{
			Editor = se;
			if (Editor.ResourceDictionary != null) this.Resources.MergedDictionaries.Add(se.ResourceDictionary);
			InitializeComponent();

			backgroundBrush = TryFindResource("SeqEdTrackHeaderBackgroundBrush") as Brush;
			backgroundSelectedBrush = TryFindResource("SeqEdTrackHeaderSelectedBackgroundBrush") as Brush;

			this.MouseDown += (sender, e) =>
			{
				if (e.ChangedButton == MouseButton.Left)
				{
					if (e.ClickCount == 1)
					{
						Editor.SelectRow(this); 
					}
					else if (e.ClickCount == 2)
					{
						if (sequence != null)
							sequence.Machine.DoubleClick();
					}
				}

			};
		}
	
#region INotifyPropertyChanged Members

public event PropertyChangedEventHandler  PropertyChanged;

#endregion
}
}
