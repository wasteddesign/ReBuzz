using BuzzGUI.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WDE.ModernPatternEditor.ColumnRenderer
{
    class ColumnSetElement : FrameworkElement
    {
        const int ScrollCache = 10;

        List<BeatVisual> beatVisuals = new List<BeatVisual>();

        ScrollViewer sv;
        ScrollContentPresenter scp;

        Size extent = new Size(1, 1);

        int BeatHeight { get { return (int)Math.Ceiling(Font.LineHeight * RPB * PatternControl.Scale); } }

        int firstVisibleBeat;
        int lastVisibleBeat;

        IColumnSet columnSet;

        public static readonly DependencyProperty ColumnSetProperty = DependencyProperty.Register("ColumnSet", typeof(IColumnSet), typeof(ColumnSetElement), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnColumnSetChanged)));

        public IColumnSet ColumnSet
        {
            get { return (IColumnSet)GetValue(ColumnSetElement.ColumnSetProperty); }
            set { SetValue(ColumnSetElement.ColumnSetProperty, value); }
        }

        static void OnColumnSetChanged(DependencyObject controlInstance, DependencyPropertyChangedEventArgs args)
        {
            var e = (ColumnSetElement)controlInstance;
            e.OnColumnSetChanged(args);
        }

        void OnColumnSetChanged(DependencyPropertyChangedEventArgs args)
        {
            if (args.OldValue != null)
            {
                columnSet.PropertyChanged -= column_PropertyChanged;
                columnSet.BeatsInvalidated -= columnSet_BeatsInvalidated;
            }

            columnSet = (IColumnSet)args.NewValue;

            if (args.NewValue != null)
            {
                columnSet.PropertyChanged += column_PropertyChanged;
                columnSet.BeatsInvalidated += columnSet_BeatsInvalidated;

                ExtentHeight = columnSet.BeatCount * BeatHeight;
                ExtentWidth = ColumnWidths.Sum();
            }

            Clear();
            InvalidateMeasure();
        }

        DispatcherAction invalidateBeatsAction;
        HashSet<int> invalidBeats = new HashSet<int>();

        void columnSet_BeatsInvalidated(HashSet<int> newbeats)
        {
            invalidBeats.UnionWith(newbeats);

            if (invalidateBeatsAction == null)
            {
                invalidateBeatsAction = new DispatcherAction(() =>
                {
                    if (firstVisibleBeat == int.MaxValue) return;

                    for (int i = firstVisibleBeat; i <= lastVisibleBeat; i++)
                    {
                        if (invalidBeats.Contains(i))
                            beatVisuals[i - firstVisibleBeat].Render(this, columnSet, i, BeatHeight);
                    }

                    invalidBeats.Clear();
                });
            }

            invalidateBeatsAction.Dispatch(System.Windows.Threading.DispatcherPriority.Normal);
        }


        public IEnumerable<int> ColumnWidths { get { return columnSet.Columns.Select(c => BeatVisual.GetColumnWidth(c)); } }

        void column_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "BeatCount":
                    ExtentHeight = columnSet.BeatCount * BeatHeight;
                    Clear();
                    InvalidateMeasure();
                    break;
            }
        }

        public ColumnSetElement(int rpb)
        {
            this.RPB = rpb;
            Clear();

            this.SizeChanged += (sender, e) =>
            {
                UpdateVisuals();
            };

            this.Unloaded += (sender, e) =>
            {
                if (sv != null)
                {
                    sv.ScrollChanged -= sv_ScrollChanged;
                    scp.SizeChanged -= scp_SizeChanged;

                }
            };

            this.IsVisibleChanged += (sender, e) =>
            {
                if (IsVisible)
                {
                    UpdateVisuals();
                }
            };

        }

        void Clear()
        {
            foreach (var bv in beatVisuals)
            {
                RemoveVisualChild(bv);
                BeatVisualAllocator.Free(bv);
            }

            beatVisuals.Clear();

            firstVisibleBeat = int.MaxValue;
            lastVisibleBeat = int.MinValue;
        }

        void AddBeat(int index, bool atend)
        {
            var bv = BeatVisualAllocator.Allocate();
            bv.Render(this, columnSet, index, BeatHeight);
            SetVisualOffset(bv, index);

            if (atend)
                beatVisuals.Add(bv);
            else
                beatVisuals.Insert(0, bv);

            AddVisualChild(bv);

            DebugConsole.WriteLine("ColumnSetElement.AddBeat {0}, {1}", index, atend);
        }

        void RemoveBeats(int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                RemoveVisualChild(beatVisuals[offset + i]);
                BeatVisualAllocator.Free(beatVisuals[offset + i]);
            }

            beatVisuals.RemoveRange(offset, count);

            DebugConsole.WriteLine("ColumnSetElement.RemoveBeats {0}, {1}", offset, count);
        }

        void SetVisualOffset(BeatVisual bv, int index)
        {
            bv.Offset = new Vector(0, index * BeatHeight);
        }

        void UpdateVisuals()
        {
            if (columnSet == null) return;

            if (sv == null)
            {
                sv = this.GetAncestor<ScrollViewer>();
                scp = this.GetAncestor<ScrollContentPresenter>();
                sv.ScrollChanged += sv_ScrollChanged;
                scp.SizeChanged += scp_SizeChanged;
            }

            if (!IsVisible) return;

            var mgrid = sv.Content as Grid;

            Point offset = new Point(sv.HorizontalOffset, sv.VerticalOffset - mgrid.Margin.Top);
            Size viewport = new Size(scp.ViewportWidth, scp.ViewportHeight);

            int fvb = Math.Max(0, Math.Min(columnSet.BeatCount - 1, (int)Math.Floor(offset.Y / BeatHeight)));
            int lvb = Math.Max(0, Math.Min(columnSet.BeatCount - 1, (int)Math.Floor((offset.Y + viewport.Height) / BeatHeight)));

            if (fvb > firstVisibleBeat) fvb = Math.Max(firstVisibleBeat, fvb - ScrollCache);
            if (lvb < lastVisibleBeat) lvb = Math.Min(lastVisibleBeat, lvb + ScrollCache);

            if (lvb < lastVisibleBeat)
                RemoveBeats(beatVisuals.Count - Math.Min((lastVisibleBeat - lvb), beatVisuals.Count), Math.Min(lastVisibleBeat - lvb, beatVisuals.Count));

            if (fvb > firstVisibleBeat)
                RemoveBeats(0, Math.Min(fvb - firstVisibleBeat, beatVisuals.Count));

            if (firstVisibleBeat != int.MaxValue)
            {

                for (int i = Math.Min(firstVisibleBeat - 1, lvb); i >= fvb; i--)
                    AddBeat(i, false);

                for (int i = Math.Max(lastVisibleBeat + 1, fvb); i <= lvb; i++)
                    AddBeat(i, true);
            }
            else
            {
                for (int i = fvb; i <= lvb; i++)
                    AddBeat(i, true);
            }

            firstVisibleBeat = fvb;
            lastVisibleBeat = lvb;
        }


        void sv_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
                UpdateVisuals();
        }

        void scp_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisuals();
        }


        protected override Visual GetVisualChild(int index) { return beatVisuals[index]; }
        protected override int VisualChildrenCount { get { return beatVisuals.Count; } }

        protected override Size MeasureOverride(Size constraint)
        {
            if (constraint.Width == double.PositiveInfinity)
                constraint.Width = ExtentWidth;

            if (constraint.Height == double.PositiveInfinity || constraint.Height == 0)
                constraint.Height = ExtentHeight;

            return constraint;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            UpdateVisuals();
            return arrangeBounds;
        }

        public double ExtentWidth
        {
            get { return extent.Width; }
            set
            {
                extent.Width = value;
            }
        }

        public double ExtentHeight
        {
            get { return extent.Height; }
            set
            {
                extent.Height = value;
            }
        }

        public int RPB { get; }

        public Rect GetFieldRect(Digit d)
        {
            var cw = ColumnWidths.ToArray();

            var col = columnSet.Columns[d.Column];
            var beat = col.FetchBeat(d.Beat);

            double h = Font.LineHeight * PatternControl.Scale;
            h *= RPB / (double)beat.Rows.Count;

            double y = d.Beat * BeatHeight + d.RowInBeat * h;
            double x = cw.Take(d.Column).Sum();
            if (col.TiedToNext) x += Font.GetWidth(0) / 2;

            return new Rect(x, y, cw[d.Column], h);
        }

        public Rect GetDigitRect(Digit d)
        {
            var r = GetFieldRect(d);

            var col = columnSet.Columns[d.Column];
            var beat = col.FetchBeat(d.Beat);
            var text = beat.Rows[d.RowInBeat].ValueString;
            var widths = Font.GetAdvanceWidths(text, PatternEditor.Settings.TextFormattingMode);
            var cw = ColumnWidths.ToArray();

            r.X += widths.Take(d.Index).Sum();
            r.X += cw[d.Column] / 2.0 - widths.Sum() / 2;

            r.Width = widths[d.Index];

            return r;
        }

        public Digit GetDigitAtPoint(PatternVM patternVM, Point p)
        {
            var cw = ColumnWidths.ToArray();

            int newColumnSet = 0, newColumn, newBeat, newRowInBeat, newIndex;

            newColumn = Math.Min(cw.FindIndex(0.0, (w, sum) => w + sum, sum => p.X < sum), columnSet.Columns.Count - 1);
            var col = columnSet.Columns[newColumn];

            if (p.Y < 0)
            {
                newBeat = 0;
                newRowInBeat = 0;
            }
            else if (p.Y < ExtentHeight)
            {
                newBeat = (int)Math.Floor(p.Y / BeatHeight);
                double h = Font.LineHeight * PatternControl.Scale;
                h *= (double)RPB / (double)col.FetchBeat(newBeat).Rows.Count;
                newRowInBeat = (int)Math.Floor(p.Y % BeatHeight / h);
            }
            else
            {
                newBeat = columnSet.BeatCount - 1;
                newRowInBeat = columnSet.Columns[newColumn].FetchBeat(newBeat).Rows.Count - 1;
            }

            var beat = col.FetchBeat(newBeat);
            var text = beat.Rows[newRowInBeat].ValueString;
            var widths = Font.GetAdvanceWidths(text, PatternEditor.Settings.TextFormattingMode);

            p.X -= ColumnWidths.Take(newColumn).Sum();
            p.X -= cw[newColumn] / 2 - widths.Sum() / 2;
            if (col.TiedToNext) p.X -= Font.GetWidth(0) / 2;
            newIndex = Math.Min(widths.FindIndex(0.0, (w, sum) => w + sum, sum => p.X < sum), col.DigitCount - 1);

            return new Digit(patternVM, newColumnSet, newColumn, newBeat, newRowInBeat, newIndex);
        }

        /*
                public Rect GetDigitRect(Digit d)
                {
                    var col = columnSet.Columns[d.Column];
                    var beat = col.FetchBeat(d.Beat);
                    var text = beat.ValueStrings[d.RowInBeat];
                    var widths = Font.GetAdvanceWidths(text, PatternEditor.Settings.TextFormattingMode);
                    var cw = ColumnWidths.ToArray();

                    double y = d.Beat * BeatHeight + d.RowInBeat * Font.LineHeight;

                    double x = d.Column == 0 ? 0 : cw.Take(d.Column).Sum();
                    x += d.Index == 0 ? 0 : widths.Take(d.Index).Sum();
                    x += cw[d.Column] / 2 - widths.Sum() / 2;

                    return new Rect(x, y, widths[d.Index], Font.LineHeight);
                }
                */


    }
}
