using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
//using PropertyChanged;


namespace BuzzGUI.MachineView.CPUMonitor
{
    /// <summary>
    /// Interaction logic for CPUMonitorWindow.xaml
    /// </summary>
    public partial class CPUMonitorWindow : Window
    {
        IMachineGraph machineGraph;
        public IMachineGraph MachineGraph
        {
            get { return machineGraph; }
            set
            {
                if (machineGraph != null)
                {
                    machineGraph.MachineAdded -= new Action<IMachine>(machineGraph_MachineAdded);
                    machineGraph.MachineRemoved -= new Action<IMachine>(machineGraph_MachineRemoved);

                    ClearMachines();
                }

                machineGraph = value;
                buzzPerfData = null;

                if (machineGraph != null)
                {
                    machineGraph.MachineAdded += new Action<IMachine>(machineGraph_MachineAdded);
                    machineGraph.MachineRemoved += new Action<IMachine>(machineGraph_MachineRemoved);

                    AddAllMachines();
                }

            }
        }

        // [DoNotNotify]
        public class MachineVM : INotifyPropertyChanged
        {
            // The engine renders audio in chunks of at most this many samples.
            // Per-chunk figures are given for a full chunk so they compare across machines.
            internal const int ChunkSamples = 256;
            const double BarWidth = 150;        // matches the bar width in the XAML cell template
            const double BarScale = 0.25;       // the full bar is 25% of one chunk's time budget
            const double Smoothing = 0.3;

            const long MinCallsForPercentile = 200;

            MachinePerformanceData perfData;
            bool hasUsage;
            long[] histogram;           // the engine's live counts
            long[] histogramBaseline;   // the counts at the last reset
            bool resetPending;

            // What is currently displayed, at display resolution. A row only formats a
            // string and raises PropertyChanged when one of these changes: every
            // notification allocates inside WPF, and with a hundred machines updated
            // four times a second that adds up to work for the garbage collector.
            long shownUsageTenths = -1;
            long shownAvgMicros = -1;
            long shownPeakMicros = -1;
            long shownAllocRate = -1;
            long shownBarPixels = -1;
            long shownTickPixels = -1;
            bool shownSpiky;
            string shownState = "";

            public IMachine Machine { get; private set; }
            public string Name { get { return Machine.Name; } }
            public string State { get; private set; } = "";
            public string DisplayName { get { return State.Length > 0 ? Name + " (" + State + ")" : Name; } }
            public double CPUUsage { get; private set; }
            public string CPUUsageString { get; private set; } = "";
            public double AvgMicros { get; private set; }
            public string AvgMicrosString { get; private set; } = "";
            public double PeakMicros { get; private set; }
            public string PeakMicrosString { get; private set; } = "";
            public double AllocRate { get; private set; }       // KB/s
            public string AllocRateString { get; private set; } = "";
            public double AvgBarWidth { get; private set; }
            public Thickness PeakTickMargin { get; private set; }
            public double PeakTickOpacity { get; private set; }
            public FontWeight PeakFontWeight { get; private set; } = FontWeights.Normal;
            public double RowOpacity { get; private set; } = 1.0;

            public MachineVM(IMachine m)
            {
                Machine = m;
                Machine.PropertyChanged += Machine_PropertyChanged;
            }

            public void Release()
            {
                Machine.PropertyChanged -= Machine_PropertyChanged;
            }

            void Machine_PropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == "Name")
                {
                    PropertyChanged.Raise(this, "Name");
                    PropertyChanged.Raise(this, "DisplayName");
                }
            }

            internal void ResetPeak()
            {
                resetPending = true;
            }

            // Duration that 99% of the Work calls since the last reset stayed under,
            // in Stopwatch ticks. 0 when there are too few calls to say.
            long Percentile99Ticks()
            {
                if (histogram == null || histogramBaseline == null) return 0;

                long total = 0;
                for (int i = 0; i < histogram.Length; i++)
                    total += histogram[i] - histogramBaseline[i];
                if (total < MinCallsForPercentile) return 0;

                long above = 0;
                for (int i = histogram.Length - 1; i >= 0; i--)
                {
                    above += histogram[i] - histogramBaseline[i];
                    if (above * 100 > total)
                    {
                        // middle of the bucket
                        long lower = MachinePerformanceData.WorkTimeHistogramLowerBound(i);
                        long upper = i + 1 < histogram.Length ? MachinePerformanceData.WorkTimeHistogramLowerBound(i + 1) : lower;
                        return (lower + upper) / 2;
                    }
                }
                return 0;
            }

            internal void Update(long dt, int sampleRate)
            {
                var newpd = Machine.PerformanceData;
                if (newpd == null) return;

                var oldpd = perfData;
                perfData = newpd;

                var live = newpd.WorkTimeHistogram;
                if (live != null && (live != histogram || histogramBaseline == null || resetPending))
                {
                    histogram = live;
                    if (histogramBaseline == null || histogramBaseline.Length != live.Length)
                        histogramBaseline = new long[live.Length];
                    Array.Copy(live, histogramBaseline, live.Length);
                    resetPending = false;
                }

                // The first snapshot only sets the baseline.
                if (oldpd == null || dt <= 0) return;

                long dpc = newpd.PerformanceCount - oldpd.PerformanceCount;
                long ds = newpd.SampleCount - oldpd.SampleCount;

                double usage = Math.Max(0.0, 100.0 * dpc / dt);
                double cpuUsage = hasUsage ? CPUUsage + Smoothing * (usage - CPUUsage) : usage;

                double avgMicros = 0;
                if (ds > 0 && dpc >= 0)
                {
                    double micros = dpc * 1e6 * ChunkSamples / ((double)Stopwatch.Frequency * ds);
                    avgMicros = hasUsage && AvgMicros > 0 ? AvgMicros + Smoothing * (micros - AvgMicros) : micros;
                }

                double allocNow = Math.Max(0.0, (newpd.AllocatedBytes - oldpd.AllocatedBytes) / 1024.0 * Stopwatch.Frequency / dt);
                double allocRate = hasUsage ? AllocRate + Smoothing * (allocNow - AllocRate) : allocNow;
                hasUsage = true;

                long peakTicks = Percentile99Ticks();
                double peakMicros = peakTicks * 1e6 / Stopwatch.Frequency;

                double chunkMicros = sampleRate > 0 ? ChunkSamples * 1e6 / sampleRate : 0;
                double peakShare = chunkMicros > 0 ? peakMicros / chunkMicros : 0;

                // Work calls far above the average are what cause a glitch while the average still looks fine.
                bool spiky = peakShare >= 0.10 && peakMicros >= 3 * avgMicros;
                string state = Machine.IsMuted ? "muted" : Machine.IsBypassed ? "bypassed" : ds == 0 ? "idle" : "";

                // The unrounded values are kept for smoothing; the sortable properties
                // and the strings follow them at display resolution.
                CPUUsage = cpuUsage;
                AvgMicros = avgMicros;
                PeakMicros = peakMicros;
                AllocRate = allocRate;

                long usageTenths = (long)Math.Round(cpuUsage * 10.0);
                if (usageTenths != shownUsageTenths)
                {
                    shownUsageTenths = usageTenths;
                    CPUUsageString = string.Format("{0:F1}%", usageTenths / 10.0);
                    PropertyChanged.Raise(this, "CPUUsage");
                    PropertyChanged.Raise(this, "CPUUsageString");
                }

                long avgWhole = ds > 0 && dpc >= 0 ? (long)Math.Round(avgMicros) : -2;
                if (avgWhole != shownAvgMicros)
                {
                    shownAvgMicros = avgWhole;
                    AvgMicrosString = avgWhole >= 0 ? avgWhole.ToString() : "–";
                    PropertyChanged.Raise(this, "AvgMicros");
                    PropertyChanged.Raise(this, "AvgMicrosString");
                }

                long peakWhole = peakTicks > 0 ? (long)Math.Round(peakMicros) : -2;
                if (peakWhole != shownPeakMicros)
                {
                    shownPeakMicros = peakWhole;
                    PeakMicrosString = peakWhole >= 0 ? peakWhole.ToString() : "–";
                    PeakTickOpacity = peakWhole >= 0 ? 1.0 : 0.0;
                    PropertyChanged.Raise(this, "PeakMicros");
                    PropertyChanged.Raise(this, "PeakMicrosString");
                    PropertyChanged.Raise(this, "PeakTickOpacity");
                }

                long allocWhole = (long)Math.Round(allocRate);
                if (allocWhole != shownAllocRate)
                {
                    shownAllocRate = allocWhole;
                    AllocRateString = allocWhole.ToString();
                    PropertyChanged.Raise(this, "AllocRate");
                    PropertyChanged.Raise(this, "AllocRateString");
                }

                long barPixels = (long)Math.Round(BarWidth * Math.Min(1.0, cpuUsage / 100.0 / BarScale));
                if (barPixels != shownBarPixels)
                {
                    shownBarPixels = barPixels;
                    AvgBarWidth = barPixels;
                    PropertyChanged.Raise(this, "AvgBarWidth");
                }

                long tickPixels = (long)Math.Round(Math.Min(BarWidth - 2, BarWidth * peakShare / BarScale));
                if (tickPixels != shownTickPixels)
                {
                    shownTickPixels = tickPixels;
                    PeakTickMargin = new Thickness(tickPixels, -2, 0, -2);
                    PropertyChanged.Raise(this, "PeakTickMargin");
                }

                if (spiky != shownSpiky)
                {
                    shownSpiky = spiky;
                    PeakFontWeight = spiky ? FontWeights.Bold : FontWeights.Normal;
                    PropertyChanged.Raise(this, "PeakFontWeight");
                }

                if (state != shownState)
                {
                    shownState = state;
                    State = state;
                    RowOpacity = state.Length > 0 ? 0.5 : 1.0;
                    PropertyChanged.Raise(this, "DisplayName");
                    PropertyChanged.Raise(this, "RowOpacity");
                }
            }

            internal string ToTextLine()
            {
                return string.Format("{0,-32} {1,8} {2,10} {3,10} {4,11}", DisplayName, CPUUsageString, AvgMicrosString, PeakMicrosString, AllocRateString);
            }

            #region INotifyPropertyChanged Members

            public event PropertyChangedEventHandler PropertyChanged;

            #endregion
        }

        readonly ObservableCollection<MachineVM> machines = new ObservableCollection<MachineVM>();

        void AddAllMachines()
        {
            foreach (var m in machineGraph.Machines)
                machines.Add(new MachineVM(m));
        }

        void ClearMachines()
        {
            foreach (var m in machines)
                m.Release();
            machines.Clear();
        }

        void machineGraph_MachineAdded(IMachine m)
        {
            machines.Add(new MachineVM(m));
        }

        void machineGraph_MachineRemoved(IMachine m)
        {
            var mvm = machines.FirstOrDefault(vm => vm.Machine == m);
            if (mvm == null) return;
            mvm.Release();
            machines.Remove(mvm);
        }


        const double TotalSmoothing = 0.3;

        DispatcherTimer timer;
        BuzzPerformanceData buzzPerfData;

        double totalUsage;
        bool hasTotalUsage;
        double machinesUsage;
        double overheadUsage;
        bool overheadValid;

        // Readings kept since the last reset
        long peakCallbackTicks;
        long peakCallbackDeadlineTicks;
        long worstOverrunTicks;
        long longestWorkTicks;
        int gcCountBaseline;
        int gcFullCountBaseline;
        TimeSpan gcPauseBaseline;
        long lastTotalAllocated = -1;
        double totalAllocRate;      // MB/s, whole process
        bool hasTotalAllocRate;
        long missBaseline;
        long missCount;
        long resetTimestamp = Stopwatch.GetTimestamp();

        public CPUMonitorWindow()
        {
            this.DataContext = this;
            InitializeComponent();
            listView.ItemsSource = machines;

            this.Loaded += (sender, e) =>
            {
                // Heaviest machines first
                SetSortColumn((listView.View as GridView).Columns[2].Header as GridViewColumnHeader);
            };

            this.IsVisibleChanged += (sender, e) =>
            {
                if (IsVisible && timer == null)
                {
                    // Readings are not collected while hidden, so start from a fresh baseline.
                    buzzPerfData = null;
                    SetTimer();
                }
                else if (!IsVisible && timer != null)
                {
                    timer.Stop();
                    timer = null;
                }
            };

        }

        void SetTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(250);
            timer.Tick += (sender, e) => UpdateReadings();
            timer.Start();
        }

        void UpdateReadings()
        {
            if (machineGraph == null) return;

            var buzz = machineGraph.Buzz;
            buzz.TakePerformanceDataSnapshot();

            var newpd = buzz.PerformanceData;
            if (newpd == null) return;

            int sampleRate = buzz.SelectedAudioDriverSampleRate;

            if (buzzPerfData == null)
            {
                // Baseline only. The peaks in this snapshot cover an unknown stretch of time.
                buzzPerfData = newpd;
                foreach (var m in machines)
                    m.Update(0, sampleRate);
                ResetPeaks(newpd.DeadlineMissCount);
                UpdateContext(buzz, sampleRate);
                return;
            }

            long dt = newpd.PerformanceCount - buzzPerfData.PerformanceCount;

            foreach (var m in machines)
                m.Update(dt, sampleRate);

            if (dt > 0)
            {
                long det = newpd.EnginePerformanceCount - buzzPerfData.EnginePerformanceCount;
                double total = Math.Max(0.0, 100.0 * det / dt);
                totalUsage = hasTotalUsage ? totalUsage + TotalSmoothing * (total - totalUsage) : total;
                hasTotalUsage = true;

                machinesUsage = machines.Sum(m => m.CPUUsage);

                long totalAllocated = GC.GetTotalAllocatedBytes(false);
                if (lastTotalAllocated >= 0)
                {
                    double rate = Math.Max(0.0, (totalAllocated - lastTotalAllocated) / (1024.0 * 1024.0) * Stopwatch.Frequency / dt);
                    totalAllocRate = hasTotalAllocRate ? totalAllocRate + TotalSmoothing * (rate - totalAllocRate) : rate;
                    hasTotalAllocRate = true;
                }
                lastTotalAllocated = totalAllocated;

                // With multithreading on, machines run in parallel and their times add up to
                // more than the engine's own wall-clock time, so the difference means nothing.
                overheadValid = !Global.EngineSettings.Multithreading;
                overheadUsage = Math.Max(0.0, totalUsage - machinesUsage);
            }

            if (newpd.MaxCallbackPerformanceCount > 0 && newpd.MaxCallbackDeadlinePerformanceCount > 0)
            {
                // Keep the callback with the highest load (time / own block period)
                if (peakCallbackDeadlineTicks == 0 ||
                    (double)newpd.MaxCallbackPerformanceCount / newpd.MaxCallbackDeadlinePerformanceCount >
                    (double)peakCallbackTicks / peakCallbackDeadlineTicks)
                {
                    peakCallbackTicks = newpd.MaxCallbackPerformanceCount;
                    peakCallbackDeadlineTicks = newpd.MaxCallbackDeadlinePerformanceCount;
                }

                long overrun = newpd.MaxCallbackPerformanceCount - newpd.MaxCallbackDeadlinePerformanceCount;
                if (overrun > worstOverrunTicks)
                    worstOverrunTicks = overrun;
            }

            if (newpd.MaxWorkPerformanceCount > longestWorkTicks)
                longestWorkTicks = newpd.MaxWorkPerformanceCount;

            missCount = newpd.DeadlineMissCount;
            buzzPerfData = newpd;

            UpdateContext(buzz, sampleRate);
            UpdateSummary();
        }

        static double TicksToMs(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }

        string SinceResetString()
        {
            var since = TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - resetTimestamp) / (double)Stopwatch.Frequency);
            return string.Format("{0:D2}:{1:D2}", (int)since.TotalMinutes, since.Seconds);
        }

        void UpdateContext(IBuzz buzz, int sampleRate)
        {
            contextText.Text = string.Format("{0} Hz · multithreading {1} · fill thread {2} · build {3}",
                sampleRate,
                Global.EngineSettings.Multithreading ? "on" : "off",
                Global.EngineSettings.AudioBufferFillThread ? "on" : "off",
                buzz.BuildNumber);
        }

        void UpdateSummary()
        {
            engineLoadText.Text = hasTotalUsage ? string.Format("{0:F1}%", totalUsage) : "–";

            double peakLoad = 0;
            if (peakCallbackDeadlineTicks > 0)
            {
                peakLoad = (double)peakCallbackTicks / peakCallbackDeadlineTicks;
                peakCallbackText.Text = string.Format("{0:F2} ms", TicksToMs(peakCallbackTicks));
                peakCallbackSubText.Text = string.Format("of {0:F2} ms deadline", TicksToMs(peakCallbackDeadlineTicks));
                headroomText.Text = peakLoad < 1.0
                    ? string.Format("{0:F0}% free", 100.0 * (1.0 - peakLoad))
                    : "over the deadline";
            }
            else
            {
                peakCallbackText.Text = "–";
                peakCallbackSubText.Text = "waiting for audio";
                headroomText.Text = "";
            }

            long misses = Math.Max(0, missCount - missBaseline);
            missesText.Text = misses.ToString();
            missesSubText.Text = worstOverrunTicks > 0
                ? string.Format("worst overrun: {0:F2} ms", TicksToMs(worstOverrunTicks))
                : "worst overrun: none";

            stallText.Text = longestWorkTicks > 0 ? string.Format("{0:F1} ms", TicksToMs(longestWorkTicks)) : "–";

            int gcCount = GC.CollectionCount(0) - gcCountBaseline;
            double gcPausedMs = (GC.GetTotalPauseDuration() - gcPauseBaseline).TotalMilliseconds;
            double sinceResetMs = TicksToMs(Stopwatch.GetTimestamp() - resetTimestamp);
            gcText.Text = string.Format("Garbage collection since reset: {0} collections ({1} full), {2:F0} ms paused ({3:F1}% of the time, {4:F1} ms average)",
                gcCount,
                GC.CollectionCount(2) - gcFullCountBaseline,
                gcPausedMs,
                sinceResetMs > 0 ? 100.0 * gcPausedMs / sinceResetMs : 0.0,
                gcCount > 0 ? gcPausedMs / gcCount : 0.0);

            allocText.Text = hasTotalAllocRate
                ? string.Format("Allocation: {0:F1} MB/s in the whole process, {1:F1} MB/s inside machines' Work calls",
                    totalAllocRate, machines.Sum(m => m.AllocRate) / 1024.0)
                : "Allocation: –";

            double trackWidth = headroomTrack.ActualWidth;
            headroomFill.Width = Math.Max(0.0, trackWidth * Math.Min(1.0, totalUsage / 100.0));
            if (peakCallbackDeadlineTicks > 0 && trackWidth > 2)
            {
                headroomTick.Margin = new Thickness((trackWidth - 2) * Math.Min(1.0, peakLoad), -2, 0, -2);
                headroomTick.Visibility = Visibility.Visible;
            }
            else
            {
                headroomTick.Visibility = Visibility.Hidden;
            }

            machinesText.Text = string.Format("Machines: {0:F1}%", machinesUsage);
            totalText.Text = string.Format("Engine: {0:F1}%", totalUsage);
            overheadText.Text = overheadValid
                ? string.Format("Host overhead: {0:F1}%", overheadUsage)
                : totalUsage > 0.05 ? string.Format("Parallelism: {0:F1}×", machinesUsage / totalUsage) : "Parallelism: –";
            sinceText.Text = "since reset " + SinceResetString();
        }

        void ResetPeaks(long currentMissCount)
        {
            peakCallbackTicks = 0;
            peakCallbackDeadlineTicks = 0;
            worstOverrunTicks = 0;
            longestWorkTicks = 0;
            gcCountBaseline = GC.CollectionCount(0);
            gcFullCountBaseline = GC.CollectionCount(2);
            gcPauseBaseline = GC.GetTotalPauseDuration();
            missBaseline = currentMissCount;
            missCount = currentMissCount;
            resetTimestamp = Stopwatch.GetTimestamp();

            foreach (var m in machines)
                m.ResetPeak();
        }

        private void ResetClick(object sender, RoutedEventArgs e)
        {
            ResetPeaks(missCount);
            UpdateSummary();
        }

        private void CopyClick(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ReBuzz CPU Monitor — " + contextText.Text);
            sb.AppendLine(string.Format("{0}   {1}   {2}", totalText.Text, machinesText.Text, overheadText.Text));
            sb.AppendLine(string.Format("Peak callback: {0} {1}   Headroom at peak: {2}",
                peakCallbackText.Text, peakCallbackSubText.Text, headroomText.Text.Length > 0 ? headroomText.Text : "–"));
            sb.AppendLine(string.Format("Deadline misses: {0} ({1})   Longest stall: {2}   {3}", missesText.Text, missesSubText.Text, stallText.Text, sinceText.Text));
            sb.AppendLine(gcText.Text);
            sb.AppendLine(allocText.Text);
            sb.AppendLine();
            sb.AppendLine(string.Format("{0,-32} {1,8} {2,10} {3,10} {4,11}", "Machine", "Avg %", "Avg µs", "99% µs", "Alloc KB/s"));
            foreach (var m in machines.OrderByDescending(m => m.CPUUsage))
                sb.AppendLine(m.ToTextLine());

            try
            {
                Clipboard.SetText(sb.ToString());
            }
            catch (Exception)
            {
                // The clipboard can be held by another application; nothing useful to do here.
            }
        }

        #region Sorting
        SortAdorner sortAdorner;
        GridViewColumnHeader sortColumn;

        private void SortClick(object sender, RoutedEventArgs e)
        {
            SetSortColumn(sender as GridViewColumnHeader);
        }

        void SetSortColumn(GridViewColumnHeader column)
        {
            var field = column.Tag as string;

            // Names sort A to Z first, readings sort highest first
            var newDir = field == "Name" ? ListSortDirection.Ascending : ListSortDirection.Descending;
            if (sortColumn == column)
                newDir = sortAdorner.Direction == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;

            if (sortColumn != null)
            {
                AdornerLayer.GetAdornerLayer(sortColumn).Remove(sortAdorner);
                listView.Items.SortDescriptions.Clear();
            }

            sortColumn = column;
            sortAdorner = new SortAdorner(sortColumn, newDir);
            AdornerLayer.GetAdornerLayer(sortColumn).Add(sortAdorner);
            listView.Items.SortDescriptions.Add(new SortDescription(field, newDir));

            // Keep the order current as the readings change
            if (listView.Items.CanChangeLiveSorting)
            {
                listView.Items.LiveSortingProperties.Clear();
                listView.Items.LiveSortingProperties.Add(field);
                listView.Items.IsLiveSorting = true;
            }
        }

        public class SortAdorner : Adorner
        {
            private readonly static Geometry _AscGeometry = Geometry.Parse("M 0,5 L 10,5 L 5,0 Z");
            private readonly static Geometry _DescGeometry = Geometry.Parse("M 0,0 L 10,0 L 5,5 Z");

            public ListSortDirection Direction { get; private set; }

            public SortAdorner(UIElement element, ListSortDirection dir) : base(element) { Direction = dir; }

            protected override void OnRender(DrawingContext drawingContext)
            {
                base.OnRender(drawingContext);
                if (AdornedElement.RenderSize.Width < 20) return;
                drawingContext.PushTransform(new TranslateTransform(AdornedElement.RenderSize.Width - 15, (AdornedElement.RenderSize.Height - 5) / 2));
                drawingContext.DrawGeometry(this.GetValue(Control.ForegroundProperty) as Brush, null, Direction == ListSortDirection.Ascending ? _AscGeometry : _DescGeometry);
                drawingContext.Pop();
            }
        }
        #endregion

        public TextFormattingMode TextFormattingMode { get { return Global.GeneralSettings.WPFIdealFontMetrics ? TextFormattingMode.Ideal : TextFormattingMode.Display; } }

    }
}
