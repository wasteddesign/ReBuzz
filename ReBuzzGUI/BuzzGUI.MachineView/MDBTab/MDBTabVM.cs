using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
//using PropertyChanged;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace BuzzGUI.MachineView.MDBTab
{
    public class MDBTabVM : INotifyPropertyChanged
    {
        readonly MachineView view;
        static readonly string mdbRoot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Buzz\\MDB");
        static readonly string jsonPath = System.IO.Path.Combine(mdbRoot, "mdb.json.zip");
        static readonly string machinesPath = System.IO.Path.Combine(mdbRoot, "mdb_machines.zip");
        static readonly string gearPath = System.IO.Path.Combine(mdbRoot, "Gear");
        static MDB.Database database;

        HashSet<string> InstalledMachineSet { get; set; }

        public ICommand DownloadMDBCommand { get; private set; }

        Visibility downloadVisibility;
        public Visibility DownloadVisibility { get => downloadVisibility; set { downloadVisibility = value; PropertyChanged.Raise(this, "DownloadVisibility"); } }
        public int ProgressBarValue { get; set; }
        public Visibility ProgressBarVisibility { get { return (ProgressBarValue != 0 && ProgressBarValue != 100) ? Visibility.Visible : Visibility.Collapsed; } }
        public bool IsDownloadButtonEnabled { get; set; }

        private bool isListBoxEnabled;
        public bool IsListBoxEnabled { get => isListBoxEnabled; set { isListBoxEnabled = value; PropertyChanged.Raise(this, "IsListBoxEnabled"); } }

        public enum MachineFilterGroups { All, Generator, Effect, Control };

        MachineFilterGroups visibleMachines = MachineFilterGroups.All;
        public MachineFilterGroups VisibleMachines
        {
            get
            {
                return visibleMachines;
            }
            set
            {
                visibleMachines = value;
                PropertyChanged.Raise(this, "VisibleMachines");
                PropertyChanged.Raise(this, "Machines");
            }
        }

        string filter;
        public string Filter { get => filter; set { filter = value; PropertyChanged.Raise(this, "Filter"); PropertyChanged.Raise(this, "Machines"); } }

        Task loadDatabaseTask;
        bool initialized;
        bool unzip;
        bool failed;

        bool Match(InstrumentType t, MachineFilterGroups g)
        {
            if (g == MachineFilterGroups.All) return true;
            switch (t)
            {
                case InstrumentType.Generator: return g == MachineFilterGroups.Generator;
                case InstrumentType.Effect: return g == MachineFilterGroups.Effect;
                case InstrumentType.Control: return g == MachineFilterGroups.Control;
                default: return false;
            }
        }

        public IEnumerable<MachineListItemVM> Machines
        {
            get
            {
                if (failed) return null;

                if (!File.Exists(jsonPath) || new FileInfo(jsonPath).Length == 0)
                {
                    DownloadVisibility = Visibility.Visible;
                    return null;
                }

                if (database == null)
                {
                    DownloadVisibility = Visibility.Collapsed;
                    LoadDatabase();
                    return null;
                }

                if (!initialized)
                    return null;

                IsListBoxEnabled = true;

                return database.MachineDLLs
                    .Where(d => !InstalledMachineSet.Contains(d.Filename.ToLowerInvariant())
                        && (Filter.Length == 0 || (d.Filename.IndexOf(Filter, 0, StringComparison.OrdinalIgnoreCase) != -1))
                        && Global.Buzz.MachineDLLs.ContainsKey(d.Filename))
                    .OrderBy(d => d.Filename.ToLowerInvariant())
                    .Select(d => new MachineListItemVM(Global.Buzz.MachineDLLs[d.Filename], d, gearPath))
                    .Where(i => Match(i.Instrument.Type, VisibleMachines));
            }
        }

        public MDBTabVM(MachineView view)
        {
            this.view = view;
            view.PropertyChanged += view_PropertyChanged;
            IsDownloadButtonEnabled = true;
            IsListBoxEnabled = false;
            DownloadVisibility = Visibility.Collapsed;
            VisibleMachines = MachineFilterGroups.All;
            Filter = "";
            InstalledMachineSet = new HashSet<string>();

            DownloadMDBCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    Download();
                }
            };

            if (Global.Buzz.Instruments.Count > 0)
                Update();
        }

        public void Update()
        {
            InstalledMachineSet = Global.Buzz.Instruments.Where(i => i.Name.Length == 0).Select(i => i.MachineDLL.Name.ToLowerInvariant()).ToHashSet();

            if (loadDatabaseTask != null)
                loadDatabaseTask.Wait();

            AddDLLs();

        }

        void AddDLLs()
        {
            if (database == null)
                return;

            database.MachineDLLs.Where(d => !InstalledMachineSet.Contains(d.Filename.ToLowerInvariant())).Run(d =>
            {
                Global.Buzz.AddMachineDLL(
                    System.IO.Path.Combine(gearPath, System.IO.Path.Combine(d.GearDirectory, d.Filename + ".dll")),
                    d.GearDirectory == "Generators" ? MachineType.Generator : MachineType.Effect);
            });

            initialized = true;
            PropertyChanged.Raise(this, "Machines");
        }

        void Download()
        {
            IsDownloadButtonEnabled = false;

            System.IO.Directory.CreateDirectory(mdbRoot);

            WebClientEx.Download(new Uri("http://jeskola.net/buzz/mdb/mdb.json.zip"), jsonPath, p => ProgressBarValue = p, () =>
            {
                DebugConsole.WriteLine("[MDB] Downloaded '{0}'", jsonPath);

                WebClientEx.Download(new Uri("http://jeskola.net/buzz/mdb/mdb_machines.zip"), machinesPath, p => ProgressBarValue = p, () =>
                {
                    DebugConsole.WriteLine("[MDB] Downloaded '{0}'", machinesPath);
                    unzip = true;
                    PropertyChanged.Raise(this, "Machines");
                });
            });

        }

        void LoadDatabase()
        {
            loadDatabaseTask = Task.Factory.StartNew(() =>
            {
                var sw = new Stopwatch();
                sw.Start();
                try
                {
                    database = MDB.Database.Load(jsonPath);
                }
                catch (Exception e)
                {
                    DebugConsole.WriteLine("[MDB] Database load failed ({0})", e.Message);
                    failed = true;
                    return;
                }

                sw.Stop();
                DebugConsole.WriteLine("[MDB] Database load {0}ms", sw.ElapsedMilliseconds);

                if (unzip)
                {
                    UnzipMachines();
                    unzip = false;
                }
            }
            ).ContinueWith(_ =>
            {
                AddDLLs();
                PropertyChanged.Raise(this, "Machines");
                IsListBoxEnabled = true;
                loadDatabaseTask = null;
            }, TaskScheduler.FromCurrentSynchronizationContext());

        }

        void UnzipMachines()
        {
            var sw = new Stopwatch();
            sw.Start();
            ZipFileEx.UnzipToPath(machinesPath, gearPath);
            sw.Stop();

            DebugConsole.WriteLine("[MDB] Unzip machines {0}ms", sw.ElapsedMilliseconds);
        }



        public void Release()
        {
            view.PropertyChanged -= view_PropertyChanged;
        }

        void view_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {

        }


        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
