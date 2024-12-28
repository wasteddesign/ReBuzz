using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
            MachinePerformanceData perfData;

            public IMachine Machine { get; private set; }
            public string Name { get { return Machine.Name; } }
            public double CPUUsage { get; private set; }
            public string CPUUsageString { get; private set; }
            public long CCPerSample { get; private set; }
            public long MaxLockTime { get; private set; }

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
                    PropertyChanged.Raise(this, "Name");
            }

            internal void Update(long dt)
            {
                if (perfData == null)
                {
                    perfData = Machine.PerformanceData;
                    return;
                }

                var newpd = Machine.PerformanceData;

                if (dt > 0)
                {
                    long dpc = newpd.PerformanceCount - perfData.PerformanceCount;
                    CPUUsage = 100.0 * dpc / dt;
                    CPUUsageString = string.Format("{0:F1}%", CPUUsage);
                    PropertyChanged.Raise(this, "CPUUsage");
                    PropertyChanged.Raise(this, "CPUUsageString");
                }

                long ds = newpd.SampleCount - perfData.SampleCount;

                if (ds > 0)
                {
                    long dcc = newpd.CycleCount - perfData.CycleCount;
                    CCPerSample = dcc / ds;
                    PropertyChanged.Raise(this, "CCPerSample");
                }

                if (perfData.MaxEngineLockTime > MaxLockTime)
                {
                    MaxLockTime = perfData.MaxEngineLockTime;
                    PropertyChanged.Raise(this, "MaxLockTime");
                }

                perfData = newpd;
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
            machines.Clear();
        }

        void machineGraph_MachineAdded(IMachine m)
        {
            machines.Add(new MachineVM(m));
        }

        void machineGraph_MachineRemoved(IMachine m)
        {
            var mvm = machines.First(vm => vm.Machine == m);
            mvm.Release();
            machines.Remove(mvm);
        }


        DispatcherTimer timer;
        BuzzPerformanceData buzzPerfData;

        public CPUMonitorWindow()
        {
            this.DataContext = this;
            InitializeComponent();
            listView.ItemsSource = machines;

            this.Loaded += (sender, e) =>
            {
                SetSortColumn((listView.View as GridView).Columns[0].Header as GridViewColumnHeader);
            };

            this.IsVisibleChanged += (sender, e) =>
            {
                if (IsVisible && timer == null)
                {
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
            timer.Tick += (sender, e) =>
            {
                if (machineGraph == null) return;
                machineGraph.Buzz.TakePerformanceDataSnapshot();

                if (buzzPerfData == null)
                {
                    buzzPerfData = machineGraph.Buzz.PerformanceData;
                    foreach (var m in machines)
                        m.Update(0);
                    return;
                }

                var newpd = machineGraph.Buzz.PerformanceData;
                long dt = newpd.PerformanceCount - buzzPerfData.PerformanceCount;

                foreach (var m in machines)
                    m.Update(dt);

                if (dt > 0)
                {
                    long det = newpd.EnginePerformanceCount - buzzPerfData.EnginePerformanceCount;
                    double total = 100.0 * det / dt;
                    totalText.Text = string.Format("Total: {0:F1}%", total);

                    double overhead = total - machines.Sum(m => m.CPUUsage);
                    overheadText.Text = string.Format("Overhead: {0:F1}%", overhead);
                }

                buzzPerfData = newpd;
            };
            timer.Start();
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

            if (sortColumn != null)
            {
                AdornerLayer.GetAdornerLayer(sortColumn).Remove(sortAdorner);
                listView.Items.SortDescriptions.Clear();
            }

            var newDir = ListSortDirection.Ascending;
            if (sortColumn == column && sortAdorner.Direction == newDir)
                newDir = ListSortDirection.Descending;

            sortColumn = column;
            sortAdorner = new SortAdorner(sortColumn, newDir);
            AdornerLayer.GetAdornerLayer(sortColumn).Add(sortAdorner);
            listView.Items.SortDescriptions.Add(new SortDescription(field, newDir));

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
