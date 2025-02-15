using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
//using PropertyChanged;

namespace BuzzGUI.MachineView
{
    /// <summary>
    /// Interaction logic for MachineView.xaml
    /// </summary>
    /// 


    public partial class MachineView : UserControl, INotifyPropertyChanged
    {
        IMachineGraph machineGraph;

        public IMachineGraph MachineGraph
        {
            get { return machineGraph; }
            set
            {
                if (machineGraph != null)
                {
                    machineGraph.PropertyChanged -= new System.ComponentModel.PropertyChangedEventHandler(machineGraph_PropertyChanged);
                    machineGraph.MachineAdded -= new Action<IMachine>(machineGraph_MachineAdded);
                    machineGraph.MachineRemoved -= new Action<IMachine>(machineGraph_MachineRemoved);
                    machineGraph.ConnectionAdded -= new Action<IMachineConnection>(machineGraph_ConnectionAdded);
                    machineGraph.ConnectionRemoved -= new Action<IMachineConnection>(machineGraph_ConnectionRemoved);

                    ClearMachines();
                }

                machineGraph = value;

                if (machineGraph != null)
                {
                    machineGraph.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(machineGraph_PropertyChanged);
                    machineGraph.MachineAdded += new Action<IMachine>(machineGraph_MachineAdded);
                    machineGraph.MachineRemoved += new Action<IMachine>(machineGraph_MachineRemoved);
                    machineGraph.ConnectionAdded += new Action<IMachineConnection>(machineGraph_ConnectionAdded);
                    machineGraph.ConnectionRemoved += new Action<IMachineConnection>(machineGraph_ConnectionRemoved);

                    AddAllMachines();
                }

                if (cpuMonitorWindow != null)
                    cpuMonitorWindow.MachineGraph = value;

                if (hdRecorderWindow != null)
                    hdRecorderWindow.MachineGraph = value;
            }
        }

        void machineGraph_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
        }

        void AddAllMachines()
        {
            machineCanvas.Children.Clear();

            foreach (IMachine i in machineGraph.Machines)
                machineGraph_MachineAdded(i);

            foreach (IMachine i in machineGraph.Machines)
                foreach (var mc in i.Inputs)
                    machineGraph_ConnectionAdded(mc);

            PropertyChanged.Raise(this, "Machines");
            PropertyChanged.Raise(this, "SelectedMachines");
        }

        void ClearMachines()
        {
            foreach (MachineControl mc in machineCanvas.Children)
                mc.Release();

            machineCanvas.Children.Clear();

            PropertyChanged.Raise(this, "Machines");
            PropertyChanged.Raise(this, "SelectedMachines");
        }

        void machineGraph_MachineAdded(IMachine m)
        {
            machineCanvas.Children.Add(new MachineControl(this) { Machine = m });
            PropertyChanged.Raise(this, "Machines");
            PropertyChanged.Raise(this, "SelectedMachines");
        }

        void machineGraph_MachineRemoved(IMachine m)
        {
            var mc = GetMachineControl(m);
            mc.Release();
            machineCanvas.Children.Remove(mc);
            PropertyChanged.Raise(this, "Machines");
            PropertyChanged.Raise(this, "SelectedMachines");
        }

        void machineGraph_ConnectionAdded(IMachineConnection mc)
        {
            MachineControl src = GetMachineControl(mc.Source);
            MachineControl dst = GetMachineControl(mc.Destination);

            Connection c = new Connection(mc, this, src, dst, new Point(0, 0));
            dst.inputs.Add(c);
            src.outputs.Add(c);

            if (dst.Machine.IsWireless)
                src.UpdateWirelessOutputs();
        }

        void machineGraph_ConnectionRemoved(IMachineConnection mc)
        {
            MachineControl src = GetMachineControl(mc.Source);
            MachineControl dst = GetMachineControl(mc.Destination);

            var c = dst.inputs.First(x => x.MachineConnection == mc);
            c.RemoveVisuals();
            dst.inputs.Remove(c);
            src.outputs.Remove(c);

            if (dst.Machine.IsWireless)
                src.UpdateWirelessOutputs();
        }

        public IEnumerable<MachineControl> Machines { get { return machineCanvas.Children.Cast<MachineControl>(); } }
        public IEnumerable<MachineControl> SelectedMachines { get { return Machines.Where(x => x.IsSelected); } }
        public MachineControl GetMachineControl(IMachine m) { return Machines.FirstOrDefault(x => x.Machine == m); }
        public IEnumerable<Connection> Connections { get { return Machines.SelectMany(m => m.inputs); } }
        public bool IsSoloActive { get { return Machines.Any(m => m.Machine.IsSoloed); } }

        internal AmpControl ampControl;
        public IBuzz Buzz { get; set; }
        public static MachineViewSettings StaticSettings = new MachineViewSettings();
        public MachineViewSettings Settings { get { return StaticSettings; } }
        Zoomer zoomer;
        public Zoomer Zoomer { get { return zoomer; } set { zoomer = value; PropertyChanged.Raise(this, "Zoomer"); } }
        internal Point contextMenuPoint = new Point(-1, -1);
        readonly ListBoxItemDragSource machineListDragSource;
        readonly ListBoxItemDragSource templateListDragSource;
        readonly ListBoxItemDragSource mdbListDragSource;


        public ParametersTab.ParametersTabVM ParametersTabVM { get; private set; }
        public MDBTab.MDBTabVM MDBTabVM { get; private set; }
        public MachineList MachineList { get; private set; }

        void GeneralSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "WPFIdealFontMetrics":
                    PropertyChanged.Raise(this, "TextFormattingMode");
                    break;
            }
        }

        void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "ZoomMouseGesture":
                    zoomer.Ctrl = Settings.ZoomMouseGesture == ZoomMouseGestures.CtrlMouseWheel;
                    break;
                case "ShowCurvedWires":
                    foreach (var c in Connections)
                    {
                        if (Settings.ShowCurvedWires)
                        {
                            c.PlugSnapping = 4;
                            c.CurvedWires = true;
                        }
                        else
                        {
                            c.PlugSnapping = 0;
                            c.CurvedWires = false;
                        }

                        c.Invalidate();
                    }
                    break;
                case "ChannelOnWire":
                    foreach (var c in Connections)
                        c.Invalidate();
                    break;

                case "DefaultZoomLevel":
                    Zoomer.Level = Settings.DefaultZoomLevel;
                    break;
            }

        }

        SimpleCommand NewMachineCommand { get; set; }
        SimpleCommand ReplaceMachineCommand { get; set; }
        SimpleCommand InsertMachineCommand { get; set; }
        public ICommand ImportSongCommand { get; private set; }
        public ICommand UnmuteAllMachinesCommand { get; private set; }
        public ICommand SettingsCommand { get; private set; }
        public SimpleCommand SelectAllCommand { get; private set; }
        public ICommand DeselectCommand { get; private set; }
        public ICommand DeleteSelectedCommand { get; private set; }
        public ICommand CutCommand { get; private set; }
        public ICommand CopyCommand { get; private set; }
        public ICommand PasteCommand { get; private set; }
        public ICommand CreateTemplateCommand { get; private set; }
        public ICommand TemplateListParentCommand { get; private set; }
        public ICommand ArrangeCommand { get; private set; }

        void Commands()
        {
            NewMachineCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = id =>
                {
                    var p = CommandCanvasPosition;
                    machineGraph.CreateMachine((int)id, (float)p.X, (float)p.Y);
                }
            };

            ReplaceMachineCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = id =>
                {
                    var p = CommandCanvasPosition;
                    if (SelectedMachines.Count() == 1)
                        machineGraph.ReplaceMachine(SelectedMachines.FirstOrDefault().Machine, (int)id, (float)p.X, (float)p.Y);
                }
            };

            InsertMachineCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = id =>
                {
                    var p1 = insertMachineConnection.Source.Position;
                    var p2 = insertMachineConnection.Destination.Position;

                    machineGraph.InsertMachine(insertMachineConnection, (int)id, 0.5f * (p1.Item1 + p2.Item1), 0.5f * (p1.Item2 + p2.Item2));
                }
            };

            ImportSongCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = id =>
                {
                    var p = CommandCanvasPosition;
                    machineGraph.ImportSong((float)p.X, (float)p.Y);
                }
            };

            UnmuteAllMachinesCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { foreach (var m in Machines) { m.Machine.IsMuted = false; m.Machine.IsSoloed = false; } }
            };

            SelectAllCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { foreach (var m in Machines) m.IsSelected = true; }
            };

            DeselectCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedMachines.Count() > 0,
                ExecuteDelegate = x => { ClearSelection(); }
            };

            DeleteSelectedCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedMachines.Count() > 0,
                ExecuteDelegate = x => { DeleteMachines(SelectedMachines.Select(m => m.Machine)); }
            };


            SettingsCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    SettingsWindow.Show(this, "Machine View");
                }
            };

            CutCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedMachines.Count() > 0,
                ExecuteDelegate = x =>
                {
                    using (new ActionGroup(machineGraph))
                    {
                        CopySelectedMachines();
                        DeleteMachines(SelectedMachines.Select(m => m.Machine));
                    }
                }
            };

            CopyCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedMachines.Count() > 0,
                ExecuteDelegate = x =>
                {
                    CopySelectedMachines();
                }
            };

            PasteCommand = new SimpleCommand
            {
                CanExecuteDelegate = x =>
                {
                    var s = ClipboardEx.GetText();
                    return BuzzGUI.Common.Templates.Template.IsValidTemplateString(s) || BuzzGUI.Common.Presets.Preset.IsValidPresetString(s);
                },
                ExecuteDelegate = p =>
                {
                    if (p is Point)
                        PasteMachines((Point)p);
                    else
                        PasteMachines(CommandCanvasPosition);
                }
            };

            CreateTemplateCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedMachines.Count() > 0,
                ExecuteDelegate = x => { CreateTemplate(); }
            };

            TemplateListParentCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => templateList != null && !templateList.IsAtRoot,
                ExecuteDelegate = x => { templateList.SetDirectory(".."); }
            };

            ArrangeCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    var snapSize = Settings.SnapGridSize * 10;
                    ArrangeView(snapSize);
                }
            };

            this.InputBindings.Add(new InputBinding(SettingsCommand, new KeyGesture(Key.E, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(SelectAllCommand, new KeyGesture(Key.A, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(DeselectCommand, new KeyGesture(Key.D, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(DeleteSelectedCommand, new KeyGesture(Key.Delete)));
            this.InputBindings.Add(new InputBinding(UnmuteAllMachinesCommand, new KeyGesture(Key.U, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(CutCommand, new KeyGesture(Key.X, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(CopyCommand, new KeyGesture(Key.C, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(PasteCommand, new KeyGesture(Key.V, ModifierKeys.Control)));
        }

        private void ArrangeView(float snapSize)
        {
            List<IMachine> machinesToArrange = SelectedMachines.Count() > 0 ? SelectedMachines.Select(x => x.Machine).ToList() : MachineGraph.Machines.ToList();

            float stepX = snapSize;
            float stepY = snapSize;

            // Draw the given node according to given position
            foreach (IMachine m in machinesToArrange)
            {
                float x = m.Position.Item1 * 1000.0f;
                float y = m.Position.Item2 * 1000.0f;
                float newX = stepX * (float)Math.Floor((x / stepX) + 0.5) / 1000.0f;
                float newY = stepY * (float)Math.Floor((y / stepY) + 0.5) / 1000.0f;

                var coords = new Tuple<float, float>(newX, newY);
                var info = new Tuple<IMachine, Tuple<float, float>>(m, coords);

                Collection<Tuple<IMachine, Tuple<float, float>>> dirtyMachines = new Collection<Tuple<IMachine, Tuple<float, float>>>();
                dirtyMachines.Add(info);
                MachineGraph.MoveMachines(dirtyMachines);
            }
        }

        public MachineView(IBuzz buzz, ResourceDictionary rd)
        {
            Global.GeneralSettings.PropertyChanged += new PropertyChangedEventHandler(GeneralSettings_PropertyChanged);
            Settings.PropertyChanged += Settings_PropertyChanged;
            SettingsWindow.AddSettings("Machine View", Settings);
            Buzz = buzz;
            this.DataContext = this;
            if (rd != null) this.Resources.MergedDictionaries.Add(rd);
            Commands();

            ParametersTabVM = new ParametersTab.ParametersTabVM(this);
            MachineList = new MachineList(this);
            MDBTabVM = new MDBTab.MDBTabVM(this);

            InitializeComponent();

            ampControl = new AmpControl(rd)
            {
                Visibility = Visibility.Hidden
            };
            ampCanvas.Children.Add(ampControl);

            this.Loaded += new RoutedEventHandler(MachineView_Loaded);
            viewGrid.SizeChanged += new SizeChangedEventHandler(MachineView_SizeChanged);
            grid.GotFocus += new RoutedEventHandler(grid_GotFocus);

            containerCanvas.DataContext = this;
            containerCanvas.Style = FindResource("ContainerCanvasStyle") as Style;

            this.TextInput += (sender, e) =>
            {
                if (e.Text.Length == 1 && ((e.Text[0] >= 'a' && e.Text[0] <= 'z') || (e.Text[0] >= 'A' && e.Text[0] <= 'Z') || (e.Text[0] >= '0' && e.Text[0] <= '9')))
                {
                    if (!viewGrid.IsPointInside(Win32Mouse.GetPosition(viewGrid))) return;
                    var pos = machineCanvas.GetPositionAtPoint(Win32Mouse.GetPosition(machineCanvas));

                    var moc = MouseOverConnection;

                    var w = new QuickNewMachineWindow(this, e.Text[0])
                    {
                        Resources = this.Resources.MergedDictionaries[0]
                    };

                    new WindowInteropHelper(w).Owner = ((HwndSource)PresentationSource.FromVisual(this)).Handle;
                    if ((bool)w.ShowDialog() && w.SelectedItem != null)
                    {
                        if (moc != null && w.SelectedItem.Instrument.Type == InstrumentType.Effect)
                        {
                            machineGraph.InsertMachine(moc.MachineConnection,
                                w.SelectedItem.Instrument.MachineDLL.Name, w.SelectedItem.Instrument.Name,
                                0.5f * (moc.Source.Machine.Position.Item1 + moc.Destination.Machine.Position.Item1),
                                0.5f * (moc.Source.Machine.Position.Item2 + moc.Destination.Machine.Position.Item2));
                        }
                        else
                        {
                            CreateInstrument(w.SelectedItem.Instrument, (float)pos.X, (float)pos.Y);
                        }
                    }

                }
            };

            machineListDragSource = new ListBoxItemDragSource(machineListBox);
            mdbListDragSource = new ListBoxItemDragSource(mdbListBox);
            machineListSearchTextBox.Search += (sender, e) => { MachineList.Filter = machineListSearchTextBox.Text; };
            mdbListSearchTextBox.Search += (sender, e) => { MDBTabVM.Filter = mdbListSearchTextBox.Text; };

            machineListSearchTextBox.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Down)
                {
                    if (machineListBox.SelectedIndex == -1)
                        machineListBox.SelectedIndex = 0;
                    else
                    {
                        if (machineListBox.SelectedIndex < machineListBox.Items.Count - 1)
                        {
                            machineListBox.SelectedIndex++;
                            machineListBox.ScrollIntoView(machineListBox.SelectedItem);
                        }
                    }
                }
                else if (e.Key == Key.Up)
                {
                    if (machineListBox.SelectedIndex != -1 && machineListBox.SelectedIndex > 0)
                    {
                        machineListBox.SelectedIndex--;
                        machineListBox.ScrollIntoView(machineListBox.SelectedItem);
                    }
                }
                else if (e.Key == Key.Return)
                {
                    if (machineListBox.SelectedItem != null)
                    {
                        CreateInstrument((machineListBox.SelectedItem as MachineListItemVM).Instrument, 0, 0);
                    }
                }
            };

            templateListDragSource = new ListBoxItemDragSource(templateListBox);
            templateListSearchTextBox.Search += (sender, e) => { TemplateList.Filter = templateListSearchTextBox.Text; };

        }

        public void Release()
        {
            Global.GeneralSettings.PropertyChanged -= GeneralSettings_PropertyChanged;
            Settings.PropertyChanged -= Settings_PropertyChanged;
            ParametersTabVM.Release();
            MDBTabVM.Release();
            templateList.Release();
        }

        void grid_GotFocus(object sender, RoutedEventArgs e)
        {
            ClearSelection();
            //machineGraph.Buzz.MIDIFocusMachine = null;		// useless?
            e.Handled = true;
        }

        void MachineView_Loaded(object l_sender, RoutedEventArgs l_e)
        {

            new Dragger
            {
                Element = this,
                Gesture = new DragMouseGesture { Button = MouseButton.Middle },
                AlternativeGesture = new DragMouseGesture { Button = MouseButton.Left, Modifiers = ModifierKeys.Shift },
                DragCursor = Cursors.ScrollAll,
                Drag = delta =>
                {
                    sv.ScrollToHorizontalOffset(sv.HorizontalOffset - delta.X);
                    sv.ScrollToVerticalOffset(sv.VerticalOffset - delta.Y);
                }
            };

            Zoomer = new Zoomer
            {
                Element = grid,
                LevelCount = 8,
                Ctrl = Settings.ZoomMouseGesture == ZoomMouseGestures.CtrlMouseWheel,
                Reset = () =>
                {
                    sv.ScrollToHorizontalOffset(machineCanvas.Width / 2 - sv.ViewportWidth / 2);
                    sv.ScrollToVerticalOffset(machineCanvas.Height / 2 - sv.ViewportHeight / 2);
                },
                Level = Settings.DefaultZoomLevel
            };

            List<MachineControl> oldSelection = new List<MachineControl>();

            new Dragger
            {
                Element = viewGrid,
                Gesture = new DragMouseGesture { Button = MouseButton.Left },
                AlternativeGesture = new DragMouseGesture { Button = MouseButton.Left, Modifiers = ModifierKeys.Control },
                Mode = DraggerMode.Absolute,
                BeginDrag = (p, alt, cc) =>
                {
                    if (!alt)
                    {
                        grid.Focus();
                        oldSelection = new List<MachineControl>();
                    }
                    else
                    {
                        oldSelection = new List<MachineControl>(SelectedMachines);
                    }

                    selectionLayer.BeginSelect(p);

                    foreach (var m in Machines) m.IsSelected = (m.GetBounds(viewGrid).Contains(p) || oldSelection.Contains(m));


                },
                Drag = p =>
                {
                    Rect r = selectionLayer.UpdateSelect(p);
                    foreach (var m in Machines) m.IsSelected = (m.GetBounds(viewGrid).IntersectsWith(r) || oldSelection.Contains(m));

                },
                EndDrag = p =>
                {
                    selectionLayer.EndSelect(p);
                    PropertyChanged.Raise(this, "SelectedMachines");
                }
            };

            viewGrid.MouseLeftButtonDown += (sender, e) =>
            {
                if (e.ClickCount == 2)
                {
                    Point p = e.GetPosition(this);
                    machineGraph.DoubleClick((int)p.X, (int)p.Y);
                    e.Handled = true;
                }
            };

            mainGrid.ContextMenuOpening += (sender, e) =>
            {
                contextMenuPoint.X = e.CursorLeft;
                contextMenuPoint.Y = e.CursorTop;
            };

            mainGrid.ContextMenuClosing += (sender, e) =>
            {
                contextMenuPoint = new Point(-1, -1);
            };

            grid.DragEnter += (sender, e) => { UpdateDragEffect(e); };
            grid.DragOver += (sender, e) => { UpdateDragEffect(e); };
            grid.Drop += (sender, e) =>
            {
                if (e.Data.GetDataPresent(typeof(MachineListItemVM)))
                {
                    var mli = e.Data.GetData(typeof(MachineListItemVM)) as MachineListItemVM;

                    var p = machineCanvas.GetPositionAtPoint(e.GetPosition(machineCanvas));
                    CreateInstrument(mli.Instrument, (float)p.X, (float)p.Y);
                    e.Handled = true;

                }
                if (e.Data.GetDataPresent(typeof(MDBTab.MachineListItemVM)))
                {
                    var mli = e.Data.GetData(typeof(MDBTab.MachineListItemVM)) as MDBTab.MachineListItemVM;

                    var p = machineCanvas.GetPositionAtPoint(e.GetPosition(machineCanvas));
                    CreateInstrument(mli.Instrument, (float)p.X, (float)p.Y);
                    e.Handled = true;

                }
                else if (e.Data.GetDataPresent(typeof(TemplateListItemVM)))
                {
                    var tli = e.Data.GetData(typeof(TemplateListItemVM)) as TemplateListItemVM;

                    var p = machineCanvas.GetPositionAtPoint(e.GetPosition(machineCanvas));
                    PasteTemplate(tli.Path, p);
                    e.Handled = true;

                }
                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
                    PasteTemplate(filenames[0], CommandCanvasPosition);
                }

            };

            mainGrid.ColumnDefinitions[1].Width = new GridLength(RegistryEx.Read("SidebarWidth", 300.0));

            sidebar.SizeChanged += (sender, e) =>
            {
                if (e.WidthChanged)
                    RegistryEx.Write("SidebarWidth", mainGrid.ColumnDefinitions[1].Width);
            };

            loaded = true;

            UpdateCanvasSize(new Size(ActualWidth, ActualHeight));
        }

        public void OnBuzzInitComplete()
        {
            // instrument list is now ready
            MachineList.Update();
            MDBTabVM.Update();
        }

        bool loaded = false;

        void MachineView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (loaded)
                UpdateCanvasSize(e.NewSize);
        }

        void UpdateCanvasSize(Size size)
        {
            size.Height = Math.Min(1000000, size.Height);

            double w = Math.Max(0, size.Width - SystemParameters.VerticalScrollBarWidth);
            double h = Math.Max(0, size.Height - SystemParameters.HorizontalScrollBarHeight);

            machineCanvas.Width = Math.Max(0, w * machineCanvas.CanvasSize);
            machineCanvas.Height = Math.Max(0, h * machineCanvas.CanvasSize);

            double s = Math.Pow(2.0, Zoomer.Level / Zoomer.LevelCount);

            sv.ScrollToHorizontalOffset(machineCanvas.Width / 2 * s - w / 2);
            sv.ScrollToVerticalOffset(machineCanvas.Height / 2 * s - h / 2);

            containerCanvas.Width = machineCanvas.Width;
            containerCanvas.Height = machineCanvas.Height;
        }

        void sv_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            int ix = (int)sv.HorizontalOffset;
            if (ix != sv.HorizontalOffset)
                sv.ScrollToHorizontalOffset(ix);
            else
                sv.ScrollToHorizontalOffset(ix);

            int iy = (int)sv.VerticalOffset;
            if (iy != sv.VerticalOffset)
                sv.ScrollToVerticalOffset(iy);
            else
                sv.ScrollToVerticalOffset(iy);

        }

        public void ClearSelection()
        {
            foreach (var m in Machines)
                m.IsSelected = false;

            PropertyChanged.Raise(this, "SelectedMachines");
        }

        public void SetSelection(MachineControl s, bool add)
        {
            foreach (var m in Machines)
                m.IsSelected = m == s ? true : (add ? m.IsSelected : false);

            PropertyChanged.Raise(this, "SelectedMachines");
        }

        public void SetSelection(IEnumerable<IMachine> s, bool add)
        {
            foreach (var m in Machines)
                m.IsSelected = s.Contains(m.Machine) ? true : (add ? m.IsSelected : false);

            PropertyChanged.Raise(this, "SelectedMachines");
        }

        public void BeginMoveSelectedMachines()
        {
            foreach (var m in Machines)
                m.BeginDragPosition = MachineCanvas.GetPosition(m);

        }

        public void MoveSelectedMachines(Point delta)
        {
            foreach (var m in SelectedMachines)
                machineCanvas.Drag(m, delta);
        }

        public void MoveSelectedMachines(IEnumerable<Point> deltas)
        {
            foreach (var x in SelectedMachines.Zip(deltas, (m, d) => new { m, d }))
                machineCanvas.Drag(x.m, x.d);
        }

        public void EndMoveSelectedMachines()
        {
            List<Tuple<IMachine, Tuple<float, float>>> mm = new List<Tuple<IMachine, Tuple<float, float>>>();

            foreach (var m in SelectedMachines)
            {
                Point p = MachineCanvas.GetPosition(m);
                mm.Add(new Tuple<IMachine, Tuple<float, float>>(m.Machine, new Tuple<float, float>((float)p.X, (float)p.Y)));
            }

            machineGraph.MoveMachines(mm);
        }

        public void BringToTop(IEnumerable<IMachine> m)
        {
            foreach (var x in Machines.Where(x => m.Contains(x.Machine)))
                x.BringToTop();
        }

        #region New Machine Menu
        List<MenuItemVM> BuildMachineIndexMenuItems(IMenuItem mie)
        {
            List<MenuItemVM> l = new List<MenuItemVM>();

            foreach (var e in mie.Children)
            {
                var vm = new MenuItemVM() { Text = e.Text, Command = NewMachineCommand, CommandParameter = e.ID, IsEnabled = e.IsEnabled, IsSeparator = e.IsSeparator, IsLabel = e.IsLabel };
                if (mie.Children.Count() > 0) vm.Children = BuildMachineIndexMenuItems(e);
                l.Add(vm);
            }

            return l;
        }

        IList<MenuItemVM> machineIndex;

        public IList<MenuItemVM> MachineIndex
        {
            get
            {
                if (machineIndex == null)
                    machineIndex = BuildMachineIndexMenuItems(machineGraph.Buzz.MachineIndex);

                return machineIndex;
            }

        }
        #endregion

        #region Replace Machine Menu
        List<MenuItemVM> BuildReplaceMachineIndexMenuItems(IMenuItem mie)
        {
            List<MenuItemVM> l = new List<MenuItemVM>();

            foreach (var e in mie.Children)
            {
                var vm = new MenuItemVM() { Text = e.Text, Command = ReplaceMachineCommand, CommandParameter = e.ID, IsEnabled = e.IsEnabled, IsSeparator = e.IsSeparator, IsLabel = e.IsLabel };
                if (mie.Children.Count() > 0) vm.Children = BuildReplaceMachineIndexMenuItems(e);
                l.Add(vm);
            }

            return l;
        }

        List<MenuItemVM> replaceMachineIndex;
        public List<MenuItemVM> ReplaceMachineIndex
        {
            get
            {
                if (replaceMachineIndex == null)
                    replaceMachineIndex = BuildReplaceMachineIndexMenuItems(machineGraph.Buzz.MachineIndex);

                return replaceMachineIndex;
            }

        }
        #endregion

        #region InsertMachineMenu
        List<MenuItemVM> BuildInsertMachineIndexMenuItems(IMenuItem mie)
        {
            List<MenuItemVM> l = new List<MenuItemVM>();

            foreach (var e in mie.Children)
            {
                var vm = new MenuItemVM() { Text = e.Text, Command = InsertMachineCommand, CommandParameter = e.ID, IsEnabled = e.IsEnabled, IsSeparator = e.IsSeparator, IsLabel = e.IsLabel };
                if (mie.Children.Count() > 0) vm.Children = BuildInsertMachineIndexMenuItems(e);
                l.Add(vm);
            }

            return l;
        }

        List<MenuItemVM> insertMachineIndex;
        public List<MenuItemVM> InsertMachineIndex
        {
            get
            {
                if (insertMachineIndex == null)
                    insertMachineIndex = BuildInsertMachineIndexMenuItems(machineGraph.Buzz.MachineIndex);

                return insertMachineIndex;
            }

        }

        ContextMenu insertMenu;
        IMachineConnection insertMachineConnection;

        public void InsertMachinePopup(IMachineConnection mc)
        {
            if (insertMenu == null)
            {
                insertMenu = new ContextMenu()
                {
                    Resources = this.Resources.MergedDictionaries[0],
                    ItemContainerStyle = TryFindResource("ContextMenuItemStyle") as Style
                };

                TextOptions.SetTextFormattingMode(insertMenu, Global.GeneralSettings.WPFIdealFontMetrics ? TextFormattingMode.Ideal : TextFormattingMode.Display);
                insertMenu.ItemsSource = InsertMachineIndex;
            }

            insertMachineConnection = mc;
            insertMenu.IsOpen = true;


        }
        #endregion


        public void LPToDP(ref float x, ref float y)
        {
            Point p = machineCanvas.TranslatePoint(machineCanvas.GetPointAtPosition(new Point(x, y)), this);
            x = (float)p.X;
            y = (float)p.Y;
        }

        public void DPToLP(ref float x, ref float y)
        {
            Point p = machineCanvas.GetPositionAtPoint(this.TranslatePoint(new Point(x, y), machineCanvas));
            x = (float)p.X;
            y = (float)p.Y;
        }

        void UpdateDragEffect(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(MachineListItemVM)) || e.Data.GetDataPresent(typeof(MDBTab.MachineListItemVM)) || e.Data.GetDataPresent(typeof(TemplateListItemVM)))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var filenames = (string[])e.Data.GetData(DataFormats.FileDrop);

                // allow drop of single .xml files only
                if (filenames.Length == 1 && System.IO.Path.GetExtension(filenames[0]).ToLower() == ".xml")
                    e.Effects = DragDropEffects.Copy;
                else
                    e.Effects = DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        internal void DeleteMachines(IEnumerable<IMachine> machines)
        {
            if (machines.Count() == 1)
            {
                var m = machines.First();

                if (m.DLL.Info.Type == MachineType.Effect && !m.IsControlMachine && m.Inputs.Count >= 1 || m.Outputs.Count >= 1)
                {
                    using (new ActionGroup(machineGraph))
                    {
                        var oldinputs = m.Inputs.Select(x => new { Source = x.Source, Channel = x.SourceChannel, Amp = (double)x.Amp / 0x4000, Pan = x.Pan }).ToArray();
                        var oldoutputs = m.Outputs.Select(x => new { Destination = x.Destination, Channel = x.DestinationChannel, Amp = (double)x.Amp / 0x4000, Pan = x.Pan }).ToArray();

                        machineGraph.DeleteMachines(machines);

                        foreach (var oi in oldinputs)
                        {
                            foreach (var oo in oldoutputs)
                            {
                                if (machineGraph.CanConnectMachines(oi.Source, oo.Destination))
                                {
                                    var amp = Math.Min(Math.Max((int)((oi.Amp * oo.Amp) * 0x4000), 0), 0xfffe);
                                    var pan = Math.Min(Math.Max(oi.Pan + oo.Pan - 0x4000, 0), 0x8000);
                                    machineGraph.ConnectMachines(oi.Source, oo.Destination, oi.Channel, oo.Channel, amp, pan);
                                }
                            }
                        }
                    }

                }
                else
                {
                    machineGraph.DeleteMachines(machines);
                }
            }
            else
            {
                machineGraph.DeleteMachines(machines);
            }
        }

        internal void DisconnectMachine(IMachine m)
        {
            if (m.Inputs.Count == 0 && m.Outputs.Count == 0) return;


            using (new ActionGroup(machineGraph))
            {

                var oldinputs = m.Inputs.Select(x => new { Source = x.Source, Channel = x.SourceChannel, Amp = (double)x.Amp / 0x4000, Pan = x.Pan }).ToArray();
                var oldoutputs = m.Outputs.Select(x => new { Destination = x.Destination, Channel = x.DestinationChannel, Amp = (double)x.Amp / 0x4000, Pan = x.Pan }).ToArray();

                var connections = m.Outputs.Concat(m.Inputs).ToArray();

                foreach (var mc in connections)
                    m.Graph.DisconnectMachines(mc);

                foreach (var oi in oldinputs)
                {
                    foreach (var oo in oldoutputs)
                    {
                        var amp = Math.Min(Math.Max((int)((oi.Amp * oo.Amp) * 0x4000), 0), 0xfffe);
                        var pan = Math.Min(Math.Max(oi.Pan + oo.Pan - 0x4000, 0), 0x8000);
                        machineGraph.ConnectMachines(oi.Source, oo.Destination, oi.Channel, oo.Channel, amp, pan);
                    }
                }
            }

        }

        void CopySelectedMachines()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var t = new BuzzGUI.Common.Templates.Template(SelectedMachines.Select(m => m.Machine), BuzzGUI.Common.Templates.TemplatePatternMode.PatternsAndSequences, BuzzGUI.Common.Templates.TemplateWavetableMode.NoWavetable);

                var ms = new MemoryStream();
                t.Save(ms);

                ms.Position = 0;
                var sr = new StreamReader(ms);
                ClipboardEx.SetText(sr.ReadToEnd());
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(string.Format("Copy failed ({0})", e.Message));
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

        }

        void PasteMachines(Point pos)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var s = ClipboardEx.GetText();

                if (BuzzGUI.Common.Templates.Template.IsValidTemplateString(s))
                {
                    var t = BuzzGUI.Common.Templates.Template.LoadFromString(s);
                    var newmachines = t.Paste(machineGraph, pos);
                    SetSelection(newmachines, false);
                    BringToTop(newmachines);
                }
                else if (BuzzGUI.Common.Presets.Preset.IsValidPresetString(s))
                {
                    var p = BuzzGUI.Common.Presets.Preset.FromString(s);
                    var instr = Buzz.Instruments.First(i => i.MachineDLL.Name == p.Machine && string.IsNullOrEmpty(i.Name));
                    var m = CreateInstrument(instr, (float)pos.X, (float)pos.Y);
                    p.Apply(m, true);
                    SetSelection(Enumerable.Repeat(m, 1), false);
                    BringToTop(Enumerable.Repeat(m, 1));
                }
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(string.Format("Paste failed ({0})", e.Message));
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

        }

        internal void CloneSelectedMachines(bool includepatandseq)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var t = new BuzzGUI.Common.Templates.Template(SelectedMachines.Select(m => m.Machine),
                    includepatandseq ? BuzzGUI.Common.Templates.TemplatePatternMode.PatternsAndSequences : BuzzGUI.Common.Templates.TemplatePatternMode.NoPatterns,
                    BuzzGUI.Common.Templates.TemplateWavetableMode.NoWavetable);

                var newmachines = t.Paste(machineGraph, new Point(double.NaN, double.NaN));
                SetSelection(newmachines, false);
                BringToTop(newmachines);
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(string.Format("Clone failed ({0})", e.Message));
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }


        }

        // NOTE: this is used to create both instruments and machines
        IMachine CreateInstrument(IInstrument instrument, float x, float y)
        {
            using (new ActionGroup(machineGraph))
            {
                bool songwasempty = machineGraph.Machines.Count == 1;

                machineGraph.CreateMachine(instrument.MachineDLL.Name, instrument.Name, null, null, null, null, -1, x, y);

                var machine = machineGraph.Machines.Last();

                if (machine.AllNonInputParameters().Any())
                    machine.CreatePattern("00", 16);

                if (machine.DLL.Info.Type == MachineType.Generator)
                {
                    // automatically connect generators to master if there are no effects
                    if (!machine.IsControlMachine && !machineGraph.Machines.Any(m => m.DLL.Info.Type == MachineType.Effect))
                    {
                        machineGraph.ConnectMachines(machine, machineGraph.Machines.First(m => m.Name == "Master"), 0, 0, 0x4000, 0x4000);
                    }

                    Buzz.Song.AddSequence(machine, Buzz.Song.Sequences.Count);

                    if (songwasempty)
                    {
                        Buzz.Song.Sequences.First(s => s.Machine == machine).SetEvent(0, new SequenceEvent(SequenceEventType.PlayPattern, machine.Patterns[0]));
                    }
                }

                return machine;
            }
        }

        TemplateList templateList;
        public TemplateList TemplateList
        {
            get
            {
                if (templateList == null)
                    templateList = new TemplateList(this);

                return templateList;
            }
        }

        internal void CreateTemplate()
        {
            var sm = SelectedMachines.Select(m => m.Machine);

            CreateTemplateWindow w = new CreateTemplateWindow(sm, TemplateList.Items != null ? TemplateList.Items.Select(t => t.DisplayName) : new string[] { })
            {
                Resources = this.Resources.MergedDictionaries[0],
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Style = TryFindResource("ThemeWindowStyle") as Style
            };
            new WindowInteropHelper(w).Owner = ((HwndSource)PresentationSource.FromVisual(this)).Handle;
            if ((bool)w.ShowDialog())
            {
                var t = new BuzzGUI.Common.Templates.Template(sm, CreateTemplateWindow.PatternMode, CreateTemplateWindow.WavetableMode);
                TemplateList.SaveTemplate(w.TemplateName, t, CreateTemplateWindow.WavetableMode == Common.Templates.TemplateWavetableMode.WaveFiles ? MachineGraph.Buzz.Song.Wavetable : null);
            }

        }

        internal void PasteTemplate(string filename, Point pos, IMachine machineToReplace = null)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                if (machineToReplace != null)
                {
                    if (MachineControl.CachedTemplate.Path != filename)
                        throw new Exception("wrong cached template");

                    using (new ActionGroup(machineGraph))
                    {
                        var t = MachineControl.CachedTemplate;
                        var connected = machineToReplace.Outputs.Select(mc => mc.Destination)
                            .Traverse(m => m.Outputs.Select(mc => mc.Destination));

                        DeleteMachines(connected);
                        DisconnectMachine(machineToReplace);

                        var newmachines = t.Paste(machineGraph, pos, machineToReplace).Concat(Enumerable.Repeat(machineToReplace, 1));
                        SetSelection(newmachines, false);
                        BringToTop(newmachines);

                    }


                }
                else
                {
                    var t = BuzzGUI.Common.Templates.Template.Load(filename);
                    var newmachines = t.Paste(machineGraph, pos);
                    SetSelection(newmachines, false);
                    BringToTop(newmachines);
                }
            }

            catch (Exception e)
            {
                System.Windows.MessageBox.Show(string.Format("PasteTemplate failed ({0})", e.Message));
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

        }


        public void TemplateListPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var tli = (sender as ListBoxItem).DataContext as TemplateListItemVM;
            if (e.Key == Key.Delete && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Shift))
            {
                tli.Delete(Keyboard.Modifiers == ModifierKeys.Shift);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.None)
            {
                if ((e.Key == Key.Return || e.Key == Key.Right) && tli.Type == TemplateListItemVM.Types.Directory)
                {
                    templateList.SetDirectory(tli.Path);
                    templateListBox.SelectedIndex = 0;
                    e.Handled = true;
                }
                if (e.Key == Key.Back || e.Key == Key.Left)
                {
                    templateList.SetDirectory("..");
                    templateListBox.SelectedIndex = 0;
                    e.Handled = true;
                }

            }

        }

        public void TemplateListMouseDoubleClick(object sender, MouseEventArgs e)
        {
            var tli = (sender as ListBoxItem).DataContext as TemplateListItemVM;
            if (tli.Type == TemplateListItemVM.Types.Directory)
            {
                templateList.SetDirectory(tli.Path);
                templateListBox.SelectedIndex = 0;
            }
            /*
			if (e.Key == Key.Delete && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Shift))
			{
				tli.Delete(Keyboard.Modifiers == ModifierKeys.Shift);
				e.Handled = true;
			}
			*/
        }

        internal Point CommandPoint
        {
            get
            {
                if (contextMenuPoint.X >= 0)
                    return contextMenuPoint;
                else
                    return Win32Mouse.GetPosition(machineCanvas);
            }

        }

        internal Point CommandCanvasPosition
        {
            get
            {
                if (contextMenuPoint.X >= 0)
                    return machineCanvas.GetPositionAtPoint(contextMenuPoint);
                else
                    return machineCanvas.GetPositionAtPoint(Win32Mouse.GetPosition(machineCanvas));
            }

        }

        internal void UpdateSolo()
        {
            PropertyChanged.Raise(this, "IsSoloActive");
        }

        #region CPUMonitor
        static CPUMonitor.CPUMonitorWindow cpuMonitorWindow;
        public bool IsCPUMonitorVisible
        {
            get { return cpuMonitorWindow != null && cpuMonitorWindow.IsVisible; }
            set
            {
                if (value)
                {
                    if (cpuMonitorWindow == null)
                    {
                        cpuMonitorWindow = new CPUMonitor.CPUMonitorWindow()
                        {
                            Resources = this.Resources.MergedDictionaries[0],
                            WindowStartupLocation = WindowStartupLocation.CenterOwner
                        };
                        new WindowInteropHelper(cpuMonitorWindow).Owner = ((HwndSource)PresentationSource.FromVisual(this)).Handle;

                        cpuMonitorWindow.Closed += cpuMonitorWindow_Closed;
                        cpuMonitorWindow.MachineGraph = machineGraph;
                        cpuMonitorWindow.Show();
                    }
                }
                else
                {
                    if (cpuMonitorWindow != null)
                    {
                        cpuMonitorWindow.MachineGraph = null;
                        cpuMonitorWindow.Close();
                    }
                }
            }
        }

        void cpuMonitorWindow_Closed(object sender, EventArgs e)
        {
            cpuMonitorWindow.Closed -= cpuMonitorWindow_Closed;
            cpuMonitorWindow = null;
            Buzz.IsCPUMonitorWindowVisible = false;
        }
        #endregion

        #region HDRecorder
        static HDRecorder.HDRecorderWindow hdRecorderWindow;
        public bool IsHardDiskRecorderVisible
        {
            get { return hdRecorderWindow != null && hdRecorderWindow.IsVisible; }
            set
            {
                if (value)
                {
                    if (hdRecorderWindow == null)
                    {
                        hdRecorderWindow = new HDRecorder.HDRecorderWindow()
                        {
                            Resources = this.Resources.MergedDictionaries[0],
                            WindowStartupLocation = WindowStartupLocation.CenterOwner
                        };
                        new WindowInteropHelper(hdRecorderWindow).Owner = ((HwndSource)PresentationSource.FromVisual(this)).Handle;

                        hdRecorderWindow.Closed += hdRecorderWindow_Closed;
                        hdRecorderWindow.MachineGraph = machineGraph;
                        hdRecorderWindow.Show();
                    }

                }
                else
                {
                    if (hdRecorderWindow != null)
                    {
                        hdRecorderWindow.MachineGraph = null;
                        hdRecorderWindow.Close();
                    }
                }
            }
        }

        void hdRecorderWindow_Closed(object sender, EventArgs e)
        {
            hdRecorderWindow.Closed -= hdRecorderWindow_Closed;
            hdRecorderWindow = null;
            Buzz.IsHardDiskRecorderWindowVisible = false;
        }

        #endregion

        void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start((sender as Hyperlink).NavigateUri.ToString());
        }

        Connection MouseOverConnection
        {
            get
            {
                var uie = Mouse.DirectlyOver as UIElement;
                if (uie == null) return null;
                Control ctrl = uie.GetAncestor<Control>();
                if (ctrl == null) return null;
                return Connections.FirstOrDefault(c => c.ConnectionControl == ctrl);
            }
        }

        public TextFormattingMode TextFormattingMode { get { return Global.GeneralSettings.WPFIdealFontMetrics ? TextFormattingMode.Ideal : TextFormattingMode.Display; } }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

    }
}
