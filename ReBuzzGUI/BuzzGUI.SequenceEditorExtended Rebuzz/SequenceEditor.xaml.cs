using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.ComponentModel;
using System.IO;
using BuzzGUI.Interfaces;
using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Common.Actions;
using BuzzGUI.Common.Actions.SongActions;
using BuzzGUI.Common.Actions.SequenceActions;
using BuzzGUI.Common.Actions.MachineActions;
using BuzzGUI.SequenceEditor.Actions;
//using WDE.Info; // Info

namespace BuzzGUI.SequenceEditor
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class SequenceEditor : UserControl, INotifyPropertyChanged
	{
		
		// public WDE.ModernSequenceEditor.SequenceEditor MSequenceEditor { get; private set; }
		// public WDE.ModernSequenceEditor.CustomSequencerWindow CustomSequencerWindow { get; private set; }

        // public WDE.Info.CustomInfoWindow CustomInfoWindow { get; private set; }

        public static SequenceEditor SequenceEditorInstance { get; set; }

		public bool TimeSignatureListInit { get; }
		public bool TimeSignatureListDelete { get; }

        public CursorElement CursorElement { get; private set; }

        ISong song;
		public ISong Song
		{
			get { return song; }
			set
			{
				if (song != null)
				{
					song.SequenceAdded -= song_SequenceAdded;
					song.SequenceRemoved -= song_SequenceRemoved;
					song.SequenceChanged -= song_SequenceChanged;
					song.PropertyChanged -= song_PropertyChanged;
					song.MachineAdded -= song_MachineAdded;
					song.MachineRemoved -= song_MachineRemoved;
					song.Buzz.PropertyChanged -= Buzz_PropertyChanged;
					song.Buzz.PatternEditorActivated -= Buzz_PatternEditorActivated;

					if (viewSettings.TimeSignatureList != null)
					{
                        viewSettings.TimeSignatureList.Changed -= TimeSignatureListChanged;
                        PropertyChanged.Raise(this, "TimeSignatureListDelete");
                    }
				}

				PatternElement.InvalidateResources();

				song = value;

				if (song != null)
				{
					song.SequenceAdded += song_SequenceAdded;
					song.SequenceRemoved += song_SequenceRemoved;
					song.SequenceChanged += song_SequenceChanged;
					song.PropertyChanged += song_PropertyChanged;
					song.MachineAdded += song_MachineAdded;
					song.MachineRemoved += song_MachineRemoved;
					song.Buzz.PropertyChanged += Buzz_PropertyChanged;
					song.Buzz.PatternEditorActivated += Buzz_PatternEditorActivated;

					if (song.Associations.ContainsKey("SequenceEditorViewSettings"))
						viewSettings = (ViewSettings)song.Associations["SequenceEditorViewSettings"];
					else
					{
						viewSettings = new ViewSettings(this);
						song.Associations["SequenceEditorViewSettings"] = viewSettings;
					}
                    viewSettings.TimeSignatureList.Changed += TimeSignatureListChanged;

                    trackSV.ScrollToHorizontalOffset(0);
					trackSV.ScrollToVerticalOffset(0);
					AddAllSequences();
					UpdateWidth();
					cursorElement.Time = 0;
					cursorElement.Row = 0;
					cursorElement.IsActive = false;
					selectionLayer.KillSelection();
					PropertyChanged.RaiseAll(this);
                    PropertyChanged.Raise(this, "TimeSignatureListInit");
                }
			}
		}

		void song_MachineAdded(IMachine m)
		{
			PropertyChanged.Raise(this, "MachineList");

			m.PropertyChanged += Machine_PropertyChanged;
		}

		void song_MachineRemoved(IMachine m)
		{
			PropertyChanged.Raise(this, "MachineList");

			m.PropertyChanged -= Machine_PropertyChanged;
			viewSettings.PatternAssociations.Remove(k => k.Machine == m);
			//viewSettings.PatternAssociationsList.Remove(m);
		}

		void song_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case "SongEnd":
					UpdateWidth();
					break;

				case "LoopStart":
					UpdateWidth();
					break;

				case "LoopEnd":
					UpdateWidth();
					break;

				case "PlayPosition":
					PlayPositionChanged();
					break;

			}
		}

		void Buzz_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case "ActiveView":
					if (Buzz.ActiveView != BuzzView.PatternView)
					{
						ClearEditContext();
					}
					else if (Buzz.ActiveView == BuzzView.PatternView)
					{
                        UpdateWidth();
                        cursorElement.BringIntoView();
                        UpdateSelectedRow();

                        Global.Buzz.ActivatePatternEditor(); // Always focus pattern editor
                    }
					break;
			}
		}

		void Machine_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var m = sender as IMachine;

			if (e.PropertyName == "Patterns")
			{
				viewSettings.PatternAssociations.Remove(k => k.Machine == m && !m.Patterns.Contains(k));
				//viewSettings.PatternAssociationsList.RemovedPatterns(m);
			}
		}

		void Buzz_PatternEditorActivated()
		{
			ClearEditContext();
		}

		void AddAllSequences()
		{
			trackStack.Children.Clear();
			trackHeaderStack.Children.Clear();
			for (int i = 0; i < song.Sequences.Count; i++) AddSequence(i);
			TrackCountChanged();
		}

		void AddSequence(int i)
		{
			trackStack.Children.Insert(i, new TrackControl(this) { ViewSettings = viewSettings, Height = viewSettings.TrackHeight, HorizontalAlignment = HorizontalAlignment.Left, Sequence = song.Sequences[i] });
			trackHeaderStack.Children.Insert(i, new TrackHeaderControl(this) { ViewSettings = viewSettings, Height = viewSettings.TrackHeight, HorizontalAlignment = HorizontalAlignment.Left, Resources = this.Resources, Sequence = song.Sequences[i] });

		}

		void song_SequenceAdded(int i, ISequence seq)
		{
			AddSequence(i);
			TrackCountChanged();
		}

		void song_SequenceRemoved(int i, ISequence seq)
		{
			trackStack.Children.RemoveAt(i);
			(trackHeaderStack.Children[i] as TrackHeaderControl).Sequence = null;
			trackHeaderStack.Children.RemoveAt(i);
			TrackCountChanged();
		}

		void song_SequenceChanged(int i, ISequence seq)
		{
            (trackStack.Children[i] as TrackControl).Sequence = seq;
			(trackHeaderStack.Children[i] as TrackHeaderControl).Sequence = seq;
		}

		void TrackCountChanged()
		{
			viewSettings.TrackCount = trackStack.Children.Count;
			int tc = trackStack.Children.Count;
			tc = Math.Min(tc, (int)(SystemParameters.FullPrimaryScreenHeight / viewSettings.TrackHeight * 2 / 3));		// limit to 2/3 of screen height

			double mh = viewSettings.TrackHeight * tc + SystemParameters.HorizontalScrollBarHeight + 3;
			double th = mh + 20 + 5;

			resizeGrid.RowDefinitions[0].MaxHeight = th;

			if (resizeGrid.RowDefinitions[0].Height.Value != 0)		// resize if view is not hidden by the user
				resizeGrid.RowDefinitions[0].Height = new GridLength(th);

			double vh = viewSettings.TrackHeight * trackStack.Children.Count + 1;
			trackViewGrid.Height = vh;
			trackHeaderStack.Height = vh;

			selectionLayer.KillSelection();
			clipboard.Clear();

			if (cursorElement.Row >= viewSettings.TrackCount)
				cursorElement.Row = viewSettings.TrackCount - 1;
		}

		void SetMarkerPosition(MarkerControl m, int p)
		{
			Canvas.SetLeft(m, p * viewSettings.TickWidth - Math.Floor(playPosMarker.ActualWidth / 2));
		}

		void PlayPositionChanged()
		{
			SetMarkerPosition(playPosMarker, song.PlayPosition);
		}

		void UpdateWidth()
		{
			viewSettings.SongEnd = song.SongEnd;
			SetMarkerPosition(songEndMarker, song.SongEnd);

			SetMarkerPosition(loopStartMarker, song.LoopStart);
			loopStartMarker.Visibility = song.LoopStart != 0 ? Visibility.Visible : Visibility.Hidden;

			SetMarkerPosition(loopEndMarker, song.LoopEnd);

			int maxt = Math.Max(song.SongEnd, CursorRightTime);
			double w = viewSettings.TickWidth * maxt + 1;

			timeLineElement.Width = w;
			trackStack.Width = w;
			markerCanvas.Width = w;

			selectionLayer.UpdateVisual();

			timeLineElement.InvalidateVisual();
		}

		TrackHeaderControl SelectedTrackHeader
		{
			get
			{
				if (trackHeaderStack.Children.Count == 0 || cursorElement.Row < 0 || cursorElement.Row >= trackHeaderStack.Children.Count) return null;
				return trackHeaderStack.Children[cursorElement.Row] as TrackHeaderControl;
			}
		}

		ISequence SelectedSequence { get { return SelectedTrackHeader != null ? SelectedTrackHeader.Sequence : null; } }

		int CursorRow { get { return cursorElement.Row; } }
		int CursorTime { get { return cursorElement.Time; } }
		int CursorSpan { get { return viewSettings.TimeSignatureList.GetBarLengthAt(cursorElement.Time); } }
		int CursorRightTime { get { return CursorTime + CursorSpan; } }

		IPattern GetPatternAt(int time, int row)
		{
			if (row >= song.Sequences.Count) return null;
			return song.Sequences[row].Events.Where(e => time >= e.Key && time < e.Key + e.Value.Span).Select(e => e.Value.Pattern).FirstOrDefault();
		}

		IPattern CursorPattern { get { return GetPatternAt(cursorElement.Time, cursorElement.Row); } }

		static ViewSettings viewSettings;
		public static ViewSettings ViewSettings
		{
			get 
			{
				return viewSettings; 
			}
		}

		public void SetVisibility(bool visible)
		{
			// only hide the grid that contains PatternElements to keep layout working
			trackViewGrid.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
		}

		public void PlayCursor()
		{
			song.PlayPosition = CursorTime;
			song.Buzz.Playing = true;
		}

		public IBuzz Buzz { get; set; }

		public ResourceDictionary ResourceDictionary { get; private set; }
		SequenceClipboard clipboard = new SequenceClipboard();

		void GeneralSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case "WPFIdealFontMetrics":
					PropertyChanged.Raise(this, "TextFormattingMode");
					break;
			}

		}

		public static SequenceEditorSettings Settings = new SequenceEditorSettings();
		public SequenceEditorSettings BindableSettings { get { return Settings; } }

		void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case "PatternBoxLook":
					PatternElement.InvalidateResources();
					foreach (TrackControl tc in trackStack.Children) tc.EventsChanged();
					break;

				case "PatternBoxColors":
					PatternElement.InvalidateResources();
					foreach (TrackControl tc in trackStack.Children) tc.EventsChanged();
					break;

				case "TimelineNumbers":
					timeLineElement.InvalidateVisual();
					break;

				case "HideEditor":
					if (Settings.HideEditor)
						resizeGrid.RowDefinitions[0].Height = new GridLength(0);
					else if (resizeGrid.RowDefinitions[0].Height.Value == 0)
						resizeGrid.RowDefinitions[0].Height = new GridLength(resizeGrid.RowDefinitions[0].MaxHeight);

					break;
			}
		}

		public ICommand AddTrackCommand { get; private set; }
		public ICommand DeleteTrackCommand { get; private set; }
		public ICommand MoveTrackUpCommand { get; private set; }
		public ICommand MoveTrackDownCommand { get; private set; }
		public ICommand SetStartCommand { get; private set; }
		public ICommand SetEndCommand { get; private set; }
		public ICommand SetTimeSignatureCommand { get; private set; }
		public ICommand SelectPatternCommand { get; private set; }
		public ICommand InsertAllCommand { get; private set; }
		public ICommand DeleteAllCommand { get; private set; }
		public ICommand CutCommand { get; private set; }
		public ICommand CopyCommand { get; private set; }
		public ICommand PasteCommand { get; private set; }
		public ICommand UndoCommand { get; private set; }
		public ICommand RedoCommand { get; private set; }
		public ICommand SettingsCommand { get; private set; }
		public ICommand SetPatternColorCommand { get; private set; }
		public ICommand ExportTrackMIDICommand { get; private set; }
		public ICommand ExportSongMIDICommand { get; private set; }
        public ICommand ImportMIDITrackCommand { get; private set; }


        public SequenceEditor(IBuzz buzz, ResourceDictionary rd)
		{
			Buzz = buzz;
			Global.GeneralSettings.PropertyChanged += new PropertyChangedEventHandler(GeneralSettings_PropertyChanged);
			Settings.PropertyChanged += Settings_PropertyChanged;
			SettingsWindow.AddSettings("Sequence Editor", Settings);
			Buzz.OpenSong += Buzz_OpenSong;
			Buzz.SaveSong += Buzz_SaveSong;
			ResourceDictionary = rd;
			this.DataContext = this;
			if (rd != null) this.Resources.MergedDictionaries.Add(rd);
			InitializeComponent();

			SequenceEditorInstance = this;
			CursorElement = cursorElement;

            loopStartMarker.rectangle.Style = TryFindResource("LoopStartMarkerRectangleStyle") as Style;
			loopEndMarker.rectangle.Style = TryFindResource("LoopEndMarkerRectangleStyle") as Style;
			songEndMarker.rectangle.Style = TryFindResource("SongEndMarkerRectangleStyle") as Style;
			playPosMarker.rectangle.Style = TryFindResource("PlayPositionMarkerRectangleStyle") as Style;

			trackViewGrid.Visibility = Visibility.Collapsed;

			trackSV.ScrollChanged += (sender, e) => 
			{
				timelineSV.ScrollToHorizontalOffset(e.HorizontalOffset);
				markerSV.ScrollToHorizontalOffset(e.HorizontalOffset);
				trackHeaderSV.ScrollToVerticalOffset(e.VerticalOffset);
			};

			this.GotKeyboardFocus += (sender, e) =>
			{
				Buzz.EditContext = viewSettings.EditContext;
				buzz.NewSequenceEditorActivated();
				cursorElement.IsActive = true;
				e.Handled = true;
			};

			this.LostKeyboardFocus += (sender, e) =>
			{
				//Buzz.EditContext = null;
				cursorElement.IsActive = false;
				e.Handled = true;
			};

			this.PreviewKeyDown += (sender, e) =>
			{
				if (SelectedSequence == null) return;

				if (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Shift)
				{
					if (e.Key == Key.Right)
					{
						MoveCursorDelta(1, 0, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);
						e.Handled = true;
					}
					else if (e.Key == Key.Left)
					{
						MoveCursorDelta(-1, 0, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);
						e.Handled = true;
					}
					else if (e.Key == Key.Down)
					{
						MoveCursorDelta(0, 1, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);
						e.Handled = true;
					}
					else if (e.Key == Key.Up)
					{
						MoveCursorDelta(0, -1, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);
						e.Handled = true;
					}
					else if (e.Key == Key.PageDown)
					{
						MoveCursorDelta(16, 0, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);
						e.Handled = true;
					}
					else if (e.Key == Key.PageUp)
					{
						MoveCursorDelta(-16, 0, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);
						e.Handled = true;
					}
					else if (e.Key == Key.Home)
					{
						if (CursorTime > 0)
							MoveCursorDelta(-int.MaxValue, 0, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);
						else
							MoveCursorDelta(0, -int.MaxValue, int.MaxValue, Keyboard.Modifiers == ModifierKeys.Shift);

						e.Handled = true;
					}
					else if (e.Key == Key.End)
					{
						if (CursorTime != viewSettings.LastCellTime)
							MoveCursorDelta(int.MaxValue, 0, song.SongEnd, Keyboard.Modifiers == ModifierKeys.Shift);
						else
							MoveCursorDelta(0, int.MaxValue, song.SongEnd, Keyboard.Modifiers == ModifierKeys.Shift);

						e.Handled = true;
					}

				}

				if (Keyboard.Modifiers == ModifierKeys.None)
				{
					if (e.Key == Key.Insert)
					{
						Do(new InsertOrDeleteAction(SelectedSequence, CursorTime, CursorSpan, true));
						e.Handled = true;
					}
					else if (e.Key == Key.Delete)
					{
						Do(new InsertOrDeleteAction(SelectedSequence, CursorTime, CursorSpan, false));
						e.Handled = true;
					}
					else if (e.Key == Key.Return)
					{
						if (SelectPatternCommand.CanExecute(null)) SelectPatternCommand.Execute(null);
						e.Handled = true;
					}
					else if (e.Key == Key.Back)
					{
						int oldct = CursorTime;
						MoveCursorDelta(-1, 0, int.MaxValue, false);
						if (CursorTime < oldct)	Do(new ClearAction(SelectedSequence, CursorTime, CursorSpan));
						e.Handled = true;
					}
				}
				else if (Keyboard.Modifiers == ModifierKeys.Shift)
				{
					if (e.Key == Key.Return)
					{
						song.Buzz.ActiveView = BuzzView.SequenceView;
						e.Handled = true;
					}
				}
				else if (Keyboard.Modifiers == ModifierKeys.Control)
				{
					if (e.Key == Key.M)
					{
						SelectedSequence.Machine.IsMuted ^= true;
						e.Handled = true;
					}
					else if (e.Key == Key.L)
					{
						SelectedSequence.Machine.IsSoloed ^= true;
						e.Handled = true;
					}
					else if (e.Key == Key.Return)
					{
						mainGrid.ContextMenu.DataContext = this;
						mainGrid.ContextMenu.IsOpen = true;
						e.Handled = true;
					}
					else if (e.Key == Key.Up)
					{
						if (MoveTrackUpCommand.CanExecute(null)) MoveTrackUpCommand.Execute(null);
						e.Handled = true;
					}
					else if (e.Key == Key.Down)
					{
						if (MoveTrackDownCommand.CanExecute(null)) MoveTrackDownCommand.Execute(null);
						e.Handled = true;
					}
				}
			};

			this.PreviewTextInput += (sender, e) =>
			{
				if (e.Text.Length == 1 && SelectedTrackHeader != null)
				{
					var p = SelectedTrackHeader.GetPatternByChar(e.Text[0]);
					if (p != null)
					{
						Do(new SetEventAction(SelectedSequence, CursorTime, new SequenceEvent(SequenceEventType.PlayPattern, p)));
						MoveCursor(CursorTime + CursorPattern.Length, CursorRow);
					}
					else if (e.Text[0] == ',')
					{
						Do(new SetEventAction(SelectedSequence, CursorTime, new SequenceEvent(SequenceEventType.Break)));
						MoveCursorDelta(1, 0, int.MaxValue, false);
					}
					else if (e.Text[0] == '-')
					{
						Do(new SetEventAction(SelectedSequence, CursorTime, new SequenceEvent(SequenceEventType.Mute)));
						MoveCursorDelta(1, 0, int.MaxValue, false);
					}
					else if (e.Text[0] == '_')
					{
						Do(new SetEventAction(SelectedSequence, CursorTime, new SequenceEvent(SequenceEventType.Thru)));
						MoveCursorDelta(1, 0, int.MaxValue, false);
					}
					else if (e.Text[0] == '.')
					{
						Do(new ClearAction(SelectedSequence, CursorTime, CursorSpan));
						MoveCursorDelta(1, 0, int.MaxValue, false);
					}
				}
			};

			AddTrackCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => true,
				ExecuteDelegate = machine => { Do(new AddSequenceAction(machine as IMachine)); }
			};

			DeleteTrackCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => SelectedSequence != null,
				ExecuteDelegate = x => 
				{
					//if (MessageBox.Show("Sure?", "Delete Track " + SelectedSequence.Machine.Name, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
					{
						Do(new DeleteSequenceAction(SelectedSequence));
						MoveCursor(CursorTime, 0);
					}
					Focus();
				}
			};

			MoveTrackUpCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => SelectedSequence != null && CursorRow > 0,
				ExecuteDelegate = x =>
				{
					Do(new SwapSequencesAction(SelectedSequence, song.Sequences[CursorRow - 1]));
					MoveCursorDelta(0, -1, int.MaxValue, false);
				}
			};

			MoveTrackDownCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => song != null && SelectedSequence != null && CursorRow < song.Sequences.Count - 1,
				ExecuteDelegate = x =>
				{
					Do(new SwapSequencesAction(SelectedSequence, song.Sequences[CursorRow + 1]));
					MoveCursorDelta(0, 1, int.MaxValue, false);
				}
			};

			SetStartCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => true,
				ExecuteDelegate = x =>
				{
					Do(new SetMarkerAction(song, SongMarkers.LoopStart, CursorTime));
				}
			};

			SetEndCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => true,
				ExecuteDelegate = x =>
				{
					int t = CursorRightTime;

					using (new ActionGroup(viewSettings.EditContext.ActionStack))
					{
						if (song.LoopEnd != t)
							Do(new SetMarkerAction(song, SongMarkers.LoopEnd, t));
						else
							Do(new SetMarkerAction(song, SongMarkers.SongEnd, t));
					}

					timeLineElement.InvalidateVisual();

				}
			};

			SetTimeSignatureCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => CursorTime < viewSettings.SongEnd,
				ExecuteDelegate = x =>
				{
					Point p = cursorCanvas.PointToScreen(new Point(Canvas.GetLeft(cursorElement), Canvas.GetTop(cursorElement)));

					StepEditWindow hw = new StepEditWindow(CursorSpan)
					{
						WindowStartupLocation = WindowStartupLocation.Manual,
						Left = p.X,
						Top = p.Y
					};

					new WindowInteropHelper(hw).Owner = ((HwndSource)PresentationSource.FromVisual(this)).Handle;

					if ((bool)hw.ShowDialog())
					{
						Do(new SetTimeSignatureAction(viewSettings.TimeSignatureList, CursorTime, hw.Step));
						cursorElement.Update();
						cursorElement.SetBlinkAnimation(true, false);
					}

					this.Focus();
				}
			};

			SelectPatternCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => SelectedSequence != null,
				ExecuteDelegate = noswitch =>
				{
                    //Don't change the pattern editor if the setting is turned off
                    if (Settings.AutoSelectPattern)
                    {
                        var p = CursorPattern;
                        if (p != null)
                            song.Buzz.SetPatternEditorPattern(p);
                        else if (SelectedSequence != null)
                            song.Buzz.SetPatternEditorMachine(SelectedSequence.Machine);
                    }

					if (noswitch == null || !(bool)noswitch)
						song.Buzz.ActivatePatternEditor();
					else
						this.Focus();

				}
			};

			InsertAllCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => SelectedSequence != null,
				ExecuteDelegate = x =>
				{
					using (new ActionGroup(viewSettings.EditContext.ActionStack))
					{
						Do(new TSLInsertOrDeleteAction(viewSettings.TimeSignatureList, CursorTime, CursorSpan, true));

						foreach (var s in song.Sequences)
							Do(new InsertOrDeleteAction(s, CursorTime, CursorSpan, true));

						if (song.SongEnd >= CursorRightTime) Do(new SetMarkerAction(song, SongMarkers.SongEnd, song.SongEnd + CursorSpan));
						if (song.LoopEnd >= CursorRightTime) Do(new SetMarkerAction(song, SongMarkers.LoopEnd, song.LoopEnd + CursorSpan));
						if (song.LoopStart >= CursorRightTime) Do(new SetMarkerAction(song, SongMarkers.LoopStart, song.LoopStart + CursorSpan));

					}


					cursorElement.Update();
				}
			};

			DeleteAllCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => SelectedSequence != null,
				ExecuteDelegate = x =>
				{
					using (new ActionGroup(viewSettings.EditContext.ActionStack))
					{
						Do(new TSLInsertOrDeleteAction(viewSettings.TimeSignatureList, CursorTime, CursorSpan, false));

						foreach (var s in song.Sequences)
							Do(new InsertOrDeleteAction(s, CursorTime, CursorSpan, false));

						if (song.LoopEnd > CursorRightTime) Do(new SetMarkerAction(song, SongMarkers.LoopEnd, song.LoopEnd - CursorSpan));
						if (song.SongEnd > CursorRightTime) Do(new SetMarkerAction(song, SongMarkers.SongEnd, song.SongEnd - CursorSpan));
						if (song.LoopStart > CursorRightTime) Do(new SetMarkerAction(song, SongMarkers.LoopStart, song.LoopStart - CursorSpan));
					}

					cursorElement.Update();
				}
			};

			CutCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => selectionLayer.SelectionNotEmpty,
				ExecuteDelegate = x =>
				{
					Do(new CutOrCopySequenceEventsAction(song, selectionLayer.Rect, clipboard, true));
				}
			};

			CopyCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => selectionLayer.SelectionNotEmpty,
				ExecuteDelegate = x =>
				{
					Do(new CutOrCopySequenceEventsAction(song, selectionLayer.Rect, clipboard, false));
				}
			};

			PasteCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => clipboard.ContainsData && CursorRow == clipboard.FirstTrack,
				ExecuteDelegate = x =>
				{
					Do(new PasteSequenceEventsAction(song, CursorTime, clipboard));
				}
			};

			UndoCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => viewSettings.EditContext.ActionStack.CanUndo,
				ExecuteDelegate = x => viewSettings.EditContext.ActionStack.Undo()
			};

			RedoCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => viewSettings.EditContext.ActionStack.CanRedo,
				ExecuteDelegate = x => viewSettings.EditContext.ActionStack.Redo()
			};

			SettingsCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => true,
				ExecuteDelegate = x =>
				{
					SettingsWindow.Show(this, "Sequence Editor");
				}
			};

			SetPatternColorCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => true,
				ExecuteDelegate = index =>
				{
					if (CursorPattern == null) return;

					if (!viewSettings.PatternAssociations.ContainsKey(CursorPattern))
						viewSettings.PatternAssociations[CursorPattern] = new PatternEx();

					//if (!viewSettings.PatternAssociationsList.ContainsKey(CursorPattern))
					//	viewSettings.PatternAssociationsList.Add(CursorPattern, new PatternEx());

					viewSettings.PatternAssociations[CursorPattern].ColorIndex = (int)index;
					//viewSettings.PatternAssociationsList.SetColorIndex(CursorPattern, (int)index);

					foreach (TrackControl tc in trackStack.Children) tc.EventsChanged();

					PropertyChanged.Raise(this, "PatternAssociations");
				}
			};

			ExportTrackMIDICommand = new SimpleCommand
			{
				CanExecuteDelegate = x => SelectedSequence != null,
				ExecuteDelegate = x => MIDIExporter.ExportMIDI(SelectedSequence)
			};

			ExportSongMIDICommand = new SimpleCommand
			{
				CanExecuteDelegate = x => Song != null,
				ExecuteDelegate = x => MIDIExporter.ExportMIDI(Song)
			};

			ImportMIDITrackCommand = new SimpleCommand
			{
				CanExecuteDelegate = x => SelectedSequence != null,
				ExecuteDelegate = x =>
				{
                    using (new ActionGroup(viewSettings.EditContext.ActionStack))
                    {
                        string pname = SelectedSequence.Machine.GetNewPatternName();
                        Do(new CreatePatternAction(SelectedSequence.Machine, pname, 16));
						var pattern = SelectedSequence.Machine.Patterns.First(p => p.Name == pname);
                        Do(new SetEventAction(SelectedSequence, CursorTime, new SequenceEvent(SequenceEventType.PlayPattern, pattern)));
                        MIDIImporter.ImportMIDISequence(SelectedSequence, pattern);
                    }
                }
			};


            this.InputBindings.Add(new InputBinding(DeleteTrackCommand, new KeyGesture(Key.Delete, ModifierKeys.Control)));
			this.InputBindings.Add(new InputBinding(SetStartCommand, new KeyGesture(Key.B, ModifierKeys.Control)));
			this.InputBindings.Add(new InputBinding(SetEndCommand, new KeyGesture(Key.E, ModifierKeys.Control)));
			this.InputBindings.Add(new InputBinding(SetTimeSignatureCommand, new KeyGesture(Key.T, ModifierKeys.Control)));
			this.InputBindings.Add(new InputBinding(InsertAllCommand, new KeyGesture(Key.I, ModifierKeys.Control)));
			this.InputBindings.Add(new InputBinding(DeleteAllCommand, new KeyGesture(Key.D, ModifierKeys.Control)));
			this.InputBindings.Add(new InputBinding(CutCommand, new KeyGesture(Key.X, ModifierKeys.Control)));
			this.InputBindings.Add(new InputBinding(CopyCommand, new KeyGesture(Key.C, ModifierKeys.Control)));
			this.InputBindings.Add(new InputBinding(PasteCommand, new KeyGesture(Key.V, ModifierKeys.Control)));
			this.InputBindings.Add(new InputBinding(UndoCommand, new KeyGesture(Key.Z, ModifierKeys.Control)));
			this.InputBindings.Add(new InputBinding(RedoCommand, new KeyGesture(Key.Y, ModifierKeys.Control)));

			new Dragger
			{
				Element = trackViewGrid,
				Gesture = new DragMouseGesture { Button = MouseButton.Left },
				Mode = DraggerMode.Absolute,
				BeginDrag = (p, alt, cc) =>
				{
					trackViewGrid.Focus();
					int row = GetRowAt(p.Y);
					int time = GetTimeAt(p.X);
					MoveCursor(time, row);
					selectionLayer.BeginSelect(new Point(time, row));
				},
				Drag = p =>
				{
					int row = GetRowAt(p.Y);
					int time = GetTimeAt(p.X);
					selectionLayer.UpdateSelect(new Point(time, row));
				},
				EndDrag = p =>
				{
					selectionLayer.EndSelect();
				}
			};

			trackViewGrid.MouseLeftButtonDown += (sender, e) =>
			{
				if (e.ClickCount == 2)
				{
					if (CursorPattern == null)
					{
						using (new ActionGroup(viewSettings.EditContext.ActionStack))
						{
							string pname = SelectedSequence.Machine.GetNewPatternName();
							Do(new CreatePatternAction(SelectedSequence.Machine, pname, 16));
							Do(new SetEventAction(SelectedSequence, CursorTime, new SequenceEvent(SequenceEventType.PlayPattern, SelectedSequence.Machine.Patterns.First(p => p.Name == pname))));
						}
					}
					else if (!Settings.AutoSelectPattern)
					{
						if (SelectPatternCommand.CanExecute(null)) SelectPatternCommand.Execute(null);
					}

					e.Handled = true;
				}
			};

			trackViewGrid.MouseRightButtonDown += (sender, e) =>
			{
				var p = e.GetPosition(trackViewGrid);
				trackViewGrid.Focus();
				int row = GetRowAt(p.Y);
				int time = GetTimeAt(p.X);
				MoveCursor(time, row);

				PropertyChanged.Raise(this, "EnableColorMenu");
			};

			this.SizeChanged += (sender, e) =>
			{
				double mh = Math.Max(0, mainGrid.RowDefinitions[0].ActualHeight + mainGrid.RowDefinitions[1].ActualHeight - SystemParameters.HorizontalScrollBarHeight - 6);
				markerSV.Height = mh;
				songEndMarker.Height = mh;
				loopStartMarker.Height = mh;
				loopEndMarker.Height = mh;
				playPosMarker.Height = mh;

				double mw = Math.Max(0, mainGrid.ColumnDefinitions[1].ActualWidth - SystemParameters.VerticalScrollBarWidth - 10);
				markerSV.Width = mw;

			};

			resizeGrid.SizeChanged += (sender, e) =>
			{
				Settings.Set("HideEditor", resizeGrid.RowDefinitions[0].Height.Value == 0);
				Settings.Save();
			};

			if (Settings.HideEditor)
				resizeGrid.RowDefinitions[0].Height = new GridLength(0);

			zoomSlider.ValueChanged += (sender, e) =>
			{
				UpdateZoomSlider();
			};

			//timeLineElement.MouseLeftButtonDown += (sender, e) =>
			//{
			//	int t = (int)(e.GetPosition(timeLineElement).X / viewSettings.TickWidth);
			//	song.PlayPosition = viewSettings.TimeSignatureList.Snap(t, int.MaxValue);
			//};

			timelineSV.PreviewMouseLeftButtonDown += (sender, e) =>
			{
				int t = (int)(e.GetPosition(timeLineElement).X / viewSettings.TickWidth);
				song.PlayPosition = viewSettings.TimeSignatureList.Snap(t, int.MaxValue);
			};

			// Init GUI Extensions
			// MSequenceEditor = new WDE.ModernSequenceEditor.SequenceEditor(buzz, rd, true);
			// CustomSequencerWindow = new CustomSequencerWindow(MSequenceEditor);
            
            // Info
            // CustomInfoWindow = new CustomInfoWindow(true);
        }

		public void Release()
		{
			Global.GeneralSettings.PropertyChanged -= GeneralSettings_PropertyChanged;
			Settings.PropertyChanged -= Settings_PropertyChanged;

			// Info
			//CustomInfoWindow.Dispose();
			// CustomSequencerWindow.Dispose();

			Buzz.OpenSong -= Buzz_OpenSong;
			Buzz.SaveSong -= Buzz_SaveSong;
		}

		int DataVersion = 2;

		void Buzz_OpenSong(IOpenSong os)
		{
			Stream s = os.GetSubSection("SequenceEditor");
			if (s == null) return;
			var br = new BinaryReader(s);
			int ver = br.ReadInt32();
			if (ver > DataVersion) return;

			zoomSlider.Value = br.ReadDouble();
			if (viewSettings.TimeSignatureList != null)
			{
                viewSettings.TimeSignatureList.Changed -= TimeSignatureListChanged;
                PropertyChanged.Raise(this, "TimeSignatureListDelete");
            }
			viewSettings.TimeSignatureList = new TimeSignatureList(br);
			viewSettings.TimeSignatureList.Changed += TimeSignatureListChanged;

			PropertyChanged.Raise(this, "TimeSignatureListInit");

            if (ver >= 2)
			{
				int count = br.ReadInt32();
				for (int i = 0; i < count; i++)
				{
					string mname = br.ReadString();
					string pname = br.ReadString();
					int ci = br.ReadInt32();

					var mac = song.Machines.Where(m => m.Name == mname).FirstOrDefault();
					if (mac != null)
					{
						var pat = mac.Patterns.FirstOrDefault(p => p.Name == pname);
						//if (pat != null) viewSettings.PatternAssociationsList.PatternAssociations[pat] = new PatternEx() { ColorIndex = ci };
						if (pat != null) viewSettings.PatternAssociations[pat] = new PatternEx() { ColorIndex = ci };
					}
				}
			}

			UpdateZoomSlider();
			timeLineElement.InvalidateVisual();
			UpdateWidth();

		}

		void Buzz_SaveSong(ISaveSong ss)
		{
			Stream s = ss.CreateSubSection("SequenceEditor");
			var bw = new BinaryWriter(s);
			bw.Write(DataVersion);
			bw.Write(zoomSlider.Value);
			viewSettings.TimeSignatureList.Write(bw);

			//bw.Write(viewSettings.PatternAssociationsList.PatternAssociations.Count());
			bw.Write(viewSettings.PatternAssociations.Count());
			//foreach (var pa in viewSettings.PatternAssociationsList.PatternAssociations)
			foreach (var pa in viewSettings.PatternAssociations)
			{
				bw.Write(pa.Key.Machine.Name);
				bw.Write(pa.Key.Name);
				bw.Write(pa.Value.ColorIndex);
			}
		}

		void UpdateZoomSlider()
		{
			ViewSettings.TickWidth = zoomSlider.Value;
			UpdateWidth();
			foreach (TrackControl tc in trackStack.Children)
				tc.EventsChanged();

			cursorElement.Update();
		}

		public bool CursorMoved { get; }
        public void MoveCursorMain(int time, int row)
        {
            cursorElement.Row = Math.Min(row, trackHeaderStack.Children.Count - 1);
            cursorElement.Time = viewSettings.TimeSignatureList.Snap(time, int.MaxValue);

            PropertyChanged.Raise(this, "CursorMoved");
        }

        void MoveCursor(int time, int row)
		{
			cursorElement.Row = Math.Min(row, trackHeaderStack.Children.Count - 1);
			cursorElement.Time = viewSettings.TimeSignatureList.Snap(time, int.MaxValue);
			UpdateWidth();
			cursorElement.BringIntoView();

			UpdateSelectedRow();
			PropertyChanged.Raise(this, "CursorMoved");
        }

		void MoveCursorDelta(int dx, int dy, int maxx, bool select)
		{
			if (select)
			{
				if (!selectionLayer.Selecting)
					selectionLayer.BeginSelect(new Point(CursorTime, CursorRow));
			}
			else
			{
				selectionLayer.KillSelection();
			}

			cursorElement.Move(dx, dy, maxx);
			UpdateWidth();
			UpdateSelectedRow();

			if (select)
				selectionLayer.UpdateSelect(new Point(CursorTime, CursorRow));

            PropertyChanged.Raise(this, "MoveCursor");
        }

		public void UpdateSelectedRow()
		{
			if (cursorElement.Row < 0 || cursorElement.Row >= trackHeaderStack.Children.Count) return;

			for (int i = 0; i < trackHeaderStack.Children.Count; i++)
				(trackHeaderStack.Children[i] as TrackHeaderControl).IsSelected = i == cursorElement.Row;

			patternListBox.SetBinding(ListBox.ItemsSourceProperty, new Binding("PatternList") { Source = SelectedTrackHeader });

			var m = SelectedSequence.Machine;

			if (m != null && m.DLL.Info.Type == MachineType.Generator || m.IsControlMachine)
				Buzz.MIDIFocusMachine = m;

			if (Settings.AutoSelectPattern && SelectPatternCommand.CanExecute(null))
				SelectPatternCommand.Execute(true);

		}

		int GetTimeAt(double x)
		{
			return Math.Max(0, (int)(x / viewSettings.TickWidth));
		}

		int GetRowAt(double y)
		{
			return Math.Max(0, Math.Min(trackStack.Children.Count - 1, (int)(y / viewSettings.TrackHeight)));
		}

		internal void SelectRow(TrackHeaderControl tc)
		{
			MoveCursor(CursorTime, trackHeaderStack.Children.IndexOf(tc));
		}

		public IEnumerable<MenuItemVM> MachineList { get { return song.Machines.Select(m => new MenuItemVM() { Text = m.Name, Command = AddTrackCommand, CommandParameter = m }).OrderBy(m => m.Text); } }

		public double ZoomLevel
		{
			get { return viewSettings != null ? viewSettings.TickWidth : 2; }
			set
			{
				viewSettings.TickWidth = value;
				PropertyChanged.Raise(this, "ZoomLevel");
			}
		}

		void Do(IAction a)
		{
			ViewSettings.EditContext.ActionStack.Do(a);
		}

		void TimeSignatureListChanged()
		{
			timeLineElement.InvalidateVisual();
		}

		void ClearEditContext()
		{
			if (Buzz.EditContext == viewSettings.EditContext)
			{
				Buzz.EditContext = null;
			}
		}

		public class ColorMenuVM
		{
			public string Name { get; set; }
			public Brush Brush { get; set; }
			public ICommand Command { get; set; }
			public object CommandParameter { get; set; }
			public bool IsSeparator { get; set; }
		}

		List<ColorMenuVM> colorMenuItems;
		public IEnumerable<ColorMenuVM> ColorMenuItems
		{
			get
			{
				if (colorMenuItems == null)
				{
					colorMenuItems = new List<ColorMenuVM>();
					if (PatternElement.PatternBrushes != null)
					{
						colorMenuItems = PatternElement.PatternBrushes.Select((b, index) => new ColorMenuVM() 
						{ 
							Name = b.Item1, Brush = b.Item2.Brush, 
							Command = SetPatternColorCommand, CommandParameter = index,
						}).Concat(new []
						{
							new ColorMenuVM() { IsSeparator = true },
							new ColorMenuVM() { Name = "Default", Command = SetPatternColorCommand, CommandParameter = -1 }
						}).ToList();
					}

				}

				return colorMenuItems;
			}
		}

		public bool EnableColorMenu { get { return CursorPattern != null; } }


		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion

		private void AddTrackButtonClick(object sender, RoutedEventArgs e)
		{
			addTrackButtonContextMenu.PlacementTarget = this;
			addTrackButtonContextMenu.IsOpen = true;
		}

		public void UpdatePatternBoxes()
		{
            foreach (TrackControl tc in trackStack.Children) tc.EventsChanged();
			PropertyChanged.Raise(this, "PatternAssociations");
        }

		public TextFormattingMode TextFormattingMode { get { return Global.GeneralSettings.WPFIdealFontMetrics ? TextFormattingMode.Ideal : TextFormattingMode.Display;	} }

    }
}
