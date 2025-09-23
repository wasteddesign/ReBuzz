using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using BuzzGUI.MachineView.SignalAnalysis;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

//using PropertyChanged;

namespace BuzzGUI.MachineView
{
    //[DoNotNotify]
    public class MachineControl : Control, INotifyPropertyChanged
    {
        #region IsSelected
        public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register("IsSelected", typeof(bool), typeof(MachineControl),
            new FrameworkPropertyMetadata(false, new PropertyChangedCallback(OnIsSelectedChanged)));

        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        private static void OnIsSelectedChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            MachineControl control = (MachineControl)obj;
            bool value = (bool)args.NewValue;
        }
        #endregion

        #region ThemeMachineImageSource
        public static readonly DependencyProperty ThemeMachineImageSourceProperty = DependencyProperty.Register("ThemeMachineImageSource", typeof(ImageSource), typeof(MachineControl),
            new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnThemeMachineImageSourceChanged)));

        public ImageSource ThemeMachineImageSource
        {
            get { return (ImageSource)GetValue(ThemeMachineImageSourceProperty); }
            set { SetValue(ThemeMachineImageSourceProperty, value); }
        }

        private static void OnThemeMachineImageSourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            MachineControl control = (MachineControl)obj;
            control.PropertyChanged.Raise(control, "MachineImageSource");
            control.PropertyChanged.Raise(control, "HasMachineImageSource");

        }

        #endregion

        #region Parts

        FrameworkElement rootPart;
        public FrameworkElement RootPart
        {
            get { return rootPart; }
            set
            {
                rootPart = value;
            }

        }

        Border borderPart;
        public Border BorderPart
        {
            get { return borderPart; }
            set
            {
                borderPart = value;
            }

        }

        #endregion

        public MachineView MachineView { get { return view; } }

        static MachineControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MachineControl), new FrameworkPropertyMetadata(typeof(MachineControl)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            RootPart = (FrameworkElement)GetTemplateChild("Root");
            BorderPart = (Border)GetTemplateChild("Border");

        }


        IMachine machine;
        readonly MachineView view;
        internal List<Connection> inputs = new List<Connection>();
        internal List<Connection> outputs = new List<Connection>();

        public IEnumerable<Connection> WirelessOutputs { get { return outputs.Where(c => !c.IsVisible).OrderBy(c => c.WirelessID); } }

        public IMachine Machine
        {
            get { return machine; }
            set
            {
                if (machine != null)
                {
                    machine.PropertyChanged -= machine_PropertyChanged;
                    machine.DLL.PropertyChanged -= DLL_PropertyChanged;
                }

                machine = value;
                UpdatePosition();

                if (machine != null)
                {
                    machine.PropertyChanged += machine_PropertyChanged;
                    machine.DLL.PropertyChanged += DLL_PropertyChanged;
                }
            }

        }

        void machine_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Position":
                    UpdatePosition();
                    break;
                case "IsActive":
                    PropertyChanged.Raise(this, HasSkinLED ? "IsSkinLEDActive" : "IsLEDActive");
                    break;
                case "Name":
                    PropertyChanged.Raise(this, "NameText");
                    foreach (var i in inputs) i.UpdateWirelessID();
                    break;
                case "IsMuted":
                case "IsBypassed":
                    PropertyChanged.Raise(this, "MachineBackgroundColor");
                    PropertyChanged.Raise(this, "NameText");
                    break;
                case "IsSoloed":
                    foreach (var m in view.Machines.Where(mc => mc.Machine.DLL.Info.Type == MachineType.Generator && !mc.Machine.IsControlMachine))
                        m.PropertyChanged.Raise(m, "NameText");
                    view.UpdateSolo();
                    break;
                case "IsWireless":
                    foreach (var i in inputs)
                    {
                        i.IsVisible = !Machine.IsWireless;
                        i.Source.UpdateWirelessOutputs();
                    }
                    break;
                case "LastEngineThread":
                    if (MachineView.Settings.ShowEngineThreads) PropertyChanged.Raise(this, "MachineBackgroundColor");
                    break;
                case "OversampleFactor":
                    PropertyChanged.Raise(this, "ModeText");
                    break;
                case "MIDIInputChannel":
                    PropertyChanged.Raise(this, "ModeText");
                    break;
            }
        }

        void DLL_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "IsCrashed":
                    PropertyChanged.Raise(this, "ErrorText");
                    break;
            }
        }

        public ICommand DeleteCommand { get; private set; }
        public ICommand MuteCommand { get; private set; }
        public ICommand SoloCommand { get; private set; }
        public ICommand BypassCommand { get; private set; }
        public ICommand OversampleCommand { get; private set; }
        public ICommand WirelessCommand { get; private set; }
        public ICommand LatencyCommand { get; private set; }
        public ICommand ShowDialogCommand { get; private set; }
        public ICommand CloneCommand { get; private set; }
        public ICommand SetMIDIInputChannelCommand { get; private set; }
        public ICommand SearchOnlineCommand { get; private set; }
        public ICommand GroupAddCommand { get; private set; }
        public ICommand GroupRemoveCommand { get; private set; }

        void Commands()
        {
            DeleteCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { view.DeleteMachines(new IMachine[] { machine }); }
            };

            MuteCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { machine.IsMuted = !machine.IsMuted; }
            };

            SoloCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { machine.IsSoloed = !machine.IsSoloed; }
            };

            BypassCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { machine.IsBypassed = !machine.IsBypassed; }
            };

            OversampleCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { machine.OversampleFactor = machine.OversampleFactor != 1 ? 1 : 2; }
            };

            WirelessCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x => { machine.IsWireless = !machine.IsWireless; }
            };

            LatencyCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    var w = new LatencyWindow(machine)
                    {
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Resources = view.Resources.MergedDictionaries[0]

                    };
                    new WindowInteropHelper(w).Owner = ((HwndSource)PresentationSource.FromVisual(this)).Handle;
                    if ((bool)w.ShowDialog())
                    {
                        if (w.IsOverridden)
                            machine.OverrideLatency = w.OverrideLatency;
                        else
                            machine.OverrideLatency = -1;
                    }

                }
            };

            ShowDialogCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = d =>
                {
                    Point p = view.CommandPoint;
                    var md = (MachineDialog)d;
                    if (md == MachineDialog.SignalAnalysis)
                    {
                        Point pos = Win32Mouse.GetScreenPosition();
                        pos.X /= WPFExtensions.PixelsPerDip;
                        pos.Y /= WPFExtensions.PixelsPerDip;

                        switch (MachineView.StaticSettings.SignalAnalysisMode)
                        {
                            case SignalAnalysisModes.Classic:
                                machine.ShowDialog((MachineDialog)d, (int)pos.X, (int)pos.Y);
                                break;
                            case SignalAnalysisModes.Modern:
                                {
                                    var saw = new SignalAnalysisWindow(Machine);

                                    new WindowInteropHelper(saw).Owner = ((HwndSource)PresentationSource.FromVisual(MachineView)).Handle;

                                    saw.Resources.MergedDictionaries.Add(this.MachineView.Resources);
                                    saw.WindowStartupLocation = WindowStartupLocation.Manual;
                                    saw.Top = pos.Y - saw.Height / 2;
                                    saw.Left = pos.X - saw.Width / 2;
                                    saw.Show();
                                }
                                break;
                            case SignalAnalysisModes.VST:
                                {
                                    var saw = new SignalAnalysisVSTWindow(Machine);

                                    new WindowInteropHelper(saw).Owner = ((HwndSource)PresentationSource.FromVisual(MachineView)).Handle;

                                    saw.Resources.MergedDictionaries.Add(this.MachineView.Resources);
                                    saw.WindowStartupLocation = WindowStartupLocation.Manual;
                                    saw.Top = pos.Y - saw.Height / 2;
                                    saw.Left = pos.X - saw.Width / 2;
                                    saw.Show();
                                }
                                break;
                        }
                    }
                    else
                    {
                        Point pos = PointToScreen(p);
                        machine.ShowDialog((MachineDialog)d, (int)pos.X, (int)pos.Y);
                    }
                }
            };

            CloneCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = includepatandseq =>
                {
                    view.CloneSelectedMachines((bool)includepatandseq);
                }
            };

            SetMIDIInputChannelCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = ch =>
                {
                    machine.MIDIInputChannel = (int)ch;
                }
            };

            SearchOnlineCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    var w = machine.DLL.Name.Split(' ');
                    if (w.Length > 0)
                    {
                        var ps = new ProcessStartInfo("https://buzz.robotplanet.dk/search.php?q=" + w[0])
                        {
                            UseShellExecute = true,
                            Verb = "open"
                        };
                        Process.Start(ps);
                    }
                }
            };

            GroupAddCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    var d = (GroupControl)x;

                    view.GroupSelectedMachines(d);
                }
            };

            GroupRemoveCommand = new SimpleCommand
            {
                CanExecuteDelegate = x =>
                {
                    if (view.Buzz.Song.MachineToGroupDict.ContainsKey(Machine))
                        return true;
                    else
                        return false;
                },
                ExecuteDelegate = x =>
                {
                    var d = (GroupControl)x;
                    view.UnGroupSelectedMachines(d);

                }
            };

            this.InputBindings.Add(new InputBinding(MuteCommand, new KeyGesture(Key.M, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(ShowDialogCommand, new KeyGesture(Key.Return)) { CommandParameter = MachineDialog.Patterns });
        }

        void CreateMenu()
        {
            if (ContextMenu == null)
            {
                ContextMenu = new ContextMenu()
                {
                    ItemContainerStyle = view.TryFindResource("ContextMenuItemStyle") as Style
                };

                ContextMenuOpening += (sender, e) =>
                {
                    view.contextMenuPoint.X = e.CursorLeft;
                    view.contextMenuPoint.Y = e.CursorTop;
                };

                ContextMenuClosing += (sender, e) =>
                {
                    view.contextMenuPoint = new Point(-1, -1);
                };

                TextOptions.SetTextFormattingMode(ContextMenu, Global.GeneralSettings.WPFIdealFontMetrics ? TextFormattingMode.Ideal : TextFormattingMode.Display);

            }

            if (machine.DLL.Info.Type == MachineType.Master)
            {
                ContextMenu.ItemsSource = new List<MenuItemVM>()
                {
                    new MenuItemVM() { Text = "Parameters...", Command = ShowDialogCommand, CommandParameter = MachineDialog.Parameters },
                    new MenuItemVM() { Text = "Patterns", Command = ShowDialogCommand, CommandParameter = MachineDialog.Patterns, GestureText = "Return" },
                    new MenuItemVM() { IsSeparator = true },
                    new MenuItemVM() { Text = "Signal Analysis...", Command = ShowDialogCommand, CommandParameter = MachineDialog.SignalAnalysis },
                    new MenuItemVM() { IsSeparator = true },
                    new MenuItemVM() { Text = "Cut", Command = view.CutCommand, GestureText = "Ctrl+X" },
                    new MenuItemVM() { Text = "Copy", Command = view.CopyCommand, GestureText = "Ctrl+C" },
                    new MenuItemVM() { Text = "Paste", Command = view.PasteCommand, GestureText = "Ctrl+V" },
                };
            }
            else
            {
                bool g = machine.DLL.Info.Type == MachineType.Generator;

                var groupsMenu = new MenuItemVM() { Text = "Groups" };
                var groupsAdd = new MenuItemVM() { Text = "Add To Group" };
                var groupsRemove = new MenuItemVM() { Text = "Remove From Group", Command = GroupRemoveCommand };

                List<MenuItemVM> availableGroups = new List<MenuItemVM>();
                foreach (var group in view.Groups)
                    availableGroups.Add(new MenuItemVM() { Text = group.MachineGroup.Name, CommandParameter = group, Command = GroupAddCommand });

                groupsAdd.Children = availableGroups;

                groupsMenu.Children = [groupsAdd, groupsRemove];

                var l = new List<IMenuItem>()
                {
                    new MenuItemVM() { Text = "Mute", Command = MuteCommand, IsCheckable = true, StaysOpenOnClick = true, IsChecked = machine.IsMuted },
                    new MenuItemVM() { Text = g ? "Solo" : "Bypass", Command = g ? SoloCommand : BypassCommand, IsCheckable = true, StaysOpenOnClick = true, IsChecked = g ? machine.IsSoloed : machine.IsBypassed },
                    new MenuItemVM() { Text = "Oversample", Command = OversampleCommand, IsCheckable = true, StaysOpenOnClick = true, IsChecked = machine.OversampleFactor != 1, IsEnabled = (machine.DLL.Info.Type == MachineType.Generator && !machine.IsControlMachine) || machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.MULTI_IO) },
                    new MenuItemVM() { Text = "Wireless", Command = WirelessCommand, IsCheckable = true, StaysOpenOnClick = true, IsChecked = machine.IsWireless, IsEnabled = machine.DLL.Info.Type == MachineType.Effect && machine.InputChannelCount == 1 },
                    new MenuItemVM() { IsSeparator = true },
                    new MenuItemVM() { Text = "Parameters...", Command = ShowDialogCommand, CommandParameter = MachineDialog.Parameters },
                    new MenuItemVM() { Text = "Attributes", IsEnabled = machine.Attributes.Count > 0, Children = machine.GetAttributeMenuItems() },
                    new MenuItemVM() { Text = "MIDI Input Channel", Children = MIDIInputChannelMenu },
                    new MenuItemVM() { Text = "Latency...", Command = LatencyCommand, IsEnabled = !machine.IsControlMachine },
                    new MenuItemVM() { Text = "Patterns", Command = ShowDialogCommand, CommandParameter = MachineDialog.Patterns, GestureText = "Return" },
                    new MenuItemVM() { IsSeparator = true },
                    new MenuItemVM() { Text = "Signal Analysis...", Command = ShowDialogCommand, CommandParameter = MachineDialog.SignalAnalysis },
                    new MenuItemVM() { IsSeparator = true },
                    new MenuItemVM() { Text = "Replace", Children = view.ReplaceMachineIndex },
                    new MenuItemVM() { Text = "Rename...", Command = ShowDialogCommand, CommandParameter = MachineDialog.Rename },
                    new MenuItemVM() { Text = "Clone", Command = CloneCommand, CommandParameter = false },
                    new MenuItemVM() { Text = "Clone with patterns", Command = CloneCommand, CommandParameter = true },
                    new MenuItemVM() { Text = "Delete", Command = view.DeleteSelectedCommand, GestureText = "Del" },
                    new MenuItemVM() { IsSeparator = true },
                    new MenuItemVM() { Text = "Cut", Command = view.CutCommand, GestureText = "Ctrl+X" },
                    new MenuItemVM() { Text = "Copy", Command = view.CopyCommand, GestureText = "Ctrl+C" },
                    new MenuItemVM() { Text = "Paste", Command = view.PasteCommand, GestureText = "Ctrl+V" },
                    new MenuItemVM() { IsSeparator = true },
                    groupsMenu,
                    new MenuItemVM() { IsSeparator = true },
                    new MenuItemVM() { Text = "Search Online", Command = SearchOnlineCommand },
                };

                var cmds = machine.Commands;
                if (cmds != null && cmds.Count() > 0)
                {
                    l.Add(new MenuItemVM() { IsSeparator = true });
                    l.AddRange(cmds);
                }

                ContextMenu.ItemsSource = l;
            }

        }

        List<MenuItemVM> MIDIInputChannelMenu
        {
            get
            {
                var l = new List<MenuItemVM>();
                var g = new MenuItemVM.Group();

                l.Add(new MenuItemVM() { Text = "None", Command = SetMIDIInputChannelCommand, CommandParameter = -1, IsCheckable = true, StaysOpenOnClick = true, CheckGroup = g, IsChecked = machine.MIDIInputChannel == -1 });
                l.Add(new MenuItemVM() { Text = "All", Command = SetMIDIInputChannelCommand, CommandParameter = 0, IsCheckable = true, StaysOpenOnClick = true, CheckGroup = g, IsChecked = machine.MIDIInputChannel == 0 });
                l.Add(new MenuItemVM() { IsSeparator = true });

                for (int ch = 1; ch <= 16; ch++)
                    l.Add(new MenuItemVM() { Text = ch.ToString(), Command = SetMIDIInputChannelCommand, CommandParameter = ch, IsCheckable = true, StaysOpenOnClick = true, CheckGroup = g, IsChecked = machine.MIDIInputChannel == ch });

                return l;
            }
        }


        void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Shadows":
                    PropertyChanged.Raise(this, "HasShadow");
                    break;
                case "EnableSkins":
                    PropertyChanged.Raise(this, "MachineImageSource");
                    PropertyChanged.Raise(this, "HasMachineImageSource");
                    PropertyChanged.Raise(this, "HasSkin");
                    PropertyChanged.Raise(this, "HasSkinLED");
                    PropertyChanged.Raise(this, "MachineTextColor");
                    break;
                case "ShowEngineThreads":
                    PropertyChanged.Raise(this, "MachineBackgroundColor");
                    PropertyChanged.Raise(this, "MachineImageSource");
                    PropertyChanged.Raise(this, "HasMachineImageSource");
                    break;
            }
        }

        public MachineControl(MachineView view)
        {
            this.DataContext = this;
            this.view = view;
            this.Loaded += MachineControl_Loaded;
            this.GotFocus += MachineControl_GotFocus;
            MachineView.Settings.PropertyChanged += Settings_PropertyChanged;
            this.AllowDrop = true;

            Commands();
        }

        public void Release()
        {
            this.GotFocus -= MachineControl_GotFocus;
            MachineView.Settings.PropertyChanged -= Settings_PropertyChanged;
            Machine = null;

            foreach (var c in inputs)
                c.RemoveVisuals();

            inputs.Clear();
        }

        void UpdatePosition()
        {
            if (machine == null) return;
            Tuple<float, float> p = Machine.Position;
            MachineCanvas.SetPosition(this, new Point(p.Item1, p.Item2));
        }

        public void UpdateConnectionVisuals()
        {
            foreach (Connection c in inputs)
                c.UpdateVisuals();
        }

        public void BringToTop() { Panel.SetZIndex(this, ++MachineCanvas.ZIndex); }


        internal Point BeginDragPosition { get; set; }

        void MachineControl_Loaded(object l_sender, RoutedEventArgs l_e)
        {
            var c = VisualTreeHelper.GetParent(this) as MachineCanvas;

            // move
            Connection insertConnection = null;
            var startPoint = new Point(0, 0);
            var lastDelta = new Point(0, 0);
            bool createTemplate = false;

            new Dragger
            {
                Element = this,
                Container = c,
                Gesture = new DragMouseGesture { Button = MouseButton.Left },
                DragCursor = Cursors.Hand,
                Mode = DraggerMode.DeltaFromOrigin,
                BeginDrag = (p, a, cc) =>
                {
                    startPoint = p;
                    insertConnection = null;
                    view.BeginMoveSelectedMachines();
                    Focus();

                    if (!IsSelected)
                        view.SetSelection(this, false);

                    lastDelta = new Point(0, 0);

                },
                Drag = delta =>
                {
                    Mouse.OverrideCursor = Cursors.Hand;
                    createTemplate = false;

                    if (view.templateListBox.IsVisible && VisualTreeHelper.HitTest(view.templateListBox, c.TranslatePoint(startPoint + (Vector)delta, view.templateListBox)) != null)
                    {
                        Mouse.OverrideCursor = Cursors.UpArrow;
                        delta = new Point(0, 0);
                        createTemplate = true;
                    }

                    lastDelta = delta;

                    view.MoveSelectedMachines(delta);
                    insertConnection = null;

                    if (view.SelectedMachines.Count() == 1)
                    {

                        if (Machine.DLL.Info.Type == MachineType.Effect && !Machine.IsControlMachine && Machine.Inputs.Count == 0 && Machine.Outputs.Count == 0)
                        {
                            var p = MachineCanvas.GetPosition(this);

                            foreach (var conn in view.Connections)
                            {
                                if (view.machineCanvas.ScaleToPixels((conn.Center - p)).Length <= 15)
                                {
                                    Mouse.OverrideCursor = Cursors.UpArrow;
                                    insertConnection = conn;
                                    break;
                                }
                            }
                        }
                        else if (Machine.Inputs.Count > 0 || Machine.Outputs.Count > 0)
                        {
                            if (MachineView.Settings.ScreenEdgeDisconnect && view.IsMouseOnEdgeOrOutsideScreen())
                                view.DisconnectMachine(Machine);
                        }
                    }
                },
                EndDrag = _ =>
                {
                    if (insertConnection != null)
                    {
                        using (new ActionGroup(Machine.Graph))
                        {
                            view.EndMoveSelectedMachines();

                            var src = insertConnection.Source;
                            var dst = insertConnection.Destination;
                            var srcchn = insertConnection.MachineConnection.SourceChannel;
                            var dstchn = insertConnection.MachineConnection.DestinationChannel;
                            var amp = insertConnection.MachineConnection.Amp;
                            insertConnection.MachineGraph.DisconnectMachines(insertConnection.MachineConnection);
                            Machine.Graph.ConnectMachines(src.Machine, this.Machine, srcchn, 0, amp, 0x4000);
                            Machine.Graph.ConnectMachines(this.Machine, dst.Machine, 0, dstchn, 0x4000, 0x4000);
                        }
                    }
                    else
                    {
                        if (lastDelta.X != 0.0 || lastDelta.Y != 0.0)
                            view.EndMoveSelectedMachines();

                        if (createTemplate)
                            view.CreateTemplate();
                    }
                },
                CancelDrag = () =>
                {
                    view.EndMoveSelectedMachines();
                }
            };

            // rotate
            Point rotateCenter = new Point(0, 0);

            new Dragger
            {
                Element = this,
                Container = c,
                Gesture = new DragMouseGesture { Button = MouseButton.XButton1 },
                AlternativeGesture = new DragMouseGesture { Button = MouseButton.Left, Modifiers = ModifierKeys.Alt },
                DragCursor = Cursors.Hand,
                Mode = DraggerMode.DeltaFromOrigin,
                BeginDrag = (p, a, cc) =>
                {
                    view.BeginMoveSelectedMachines();
                    Focus();

                    if (!IsSelected)
                        view.SetSelection(this, false);

                    var sm = view.SelectedMachines.Select(m => m.Machine);
                    var selectionoutputs = sm.Where(m => m.Inputs.Count() == 0).Traverse(m => sm.Contains(m) ? m.Outputs.Select(mc => mc.Destination) : null).Where(m => !sm.Contains(m));
                    var positions = selectionoutputs.Select(m => new Vector(m.Position.Item1, m.Position.Item2));

                    if (positions.Count() > 0)
                        rotateCenter = c.GetPointAtPosition((Point)(positions.Aggregate((acc, x) => acc + x) / positions.Count()));
                    else
                        rotateCenter = c.GetPointAtPosition(view.MachineGraph.Machines.Where(m => m.Name == "Master").Select(m => new Point(m.Position.Item1, m.Position.Item2)).First());

                    startPoint = p;

                },
                Drag = delta =>
                {
                    var p = startPoint + (Vector)delta;

                    var deltaangle = Vector.AngleBetween(p - rotateCenter, startPoint - rotateCenter);
                    var spr = (startPoint - rotateCenter).Length;
                    var deltaradius = spr > 0 ? (p - rotateCenter).Length / spr : 0;

                    view.MoveSelectedMachines(view.SelectedMachines.Select(m =>
                    {
                        var mp = c.GetPointAtPosition(m.BeginDragPosition);
                        var mangle = Vector.AngleBetween(mp - rotateCenter, new Vector(0, 1));
                        var mradius = (mp - rotateCenter).Length;
                        mangle += deltaangle;
                        mradius *= deltaradius;
                        return (Point)(rotateCenter + mradius * new Vector(Math.Sin(mangle * Math.PI / 180.0), Math.Cos(mangle * Math.PI / 180.0)) - mp);
                    }));

                },
                EndDrag = _ =>
                {
                    view.EndMoveSelectedMachines();
                },
                CancelDrag = () =>
                {
                    view.EndMoveSelectedMachines();
                }
            };

            // connect
            MachineControl dstMachine = null;
            GroupControl dstGroupMachine = null;
            Connection tempConnection = null;
            bool isGroupedTargetOld = false;

            new Dragger
            {
                Element = this,
                Container = c,
                Gesture = new DragMouseGesture { Button = MouseButton.Middle },
                AlternativeGesture = new DragMouseGesture { Button = MouseButton.Left, Modifiers = ModifierKeys.Shift },
                Mode = DraggerMode.Absolute,
                BeginDrag = (p, alt, cc) =>
                {
                    Focus();
                    tempConnection = new Connection(null, view, this, null, p);
                    dstMachine = null;
                },
                Drag = p =>
                {
                    dstMachine = c.GetMachineAtPoint(p);
                    if (dstMachine == null || dstMachine == this)
                    {
                        // Group the previous one if needed.
                        var dstGroupMachineNew = c.GetMachineGroupAtPoint(p);
                        if (dstGroupMachineNew != null && dstGroupMachine != null && dstGroupMachineNew != dstGroupMachine)
                        {
                            if (isGroupedTargetOld)
                            {
                                view.GroupMachines(dstGroupMachine, true);
                            }
                            dstGroupMachine = null;
                        }
                        // Mouse is on top of group machine
                        if (dstGroupMachineNew != null && dstGroupMachine != dstGroupMachineNew)
                        {
                            dstGroupMachine = dstGroupMachineNew;
                            isGroupedTargetOld = dstGroupMachine.MachineGroup.IsGrouped;
                            if (isGroupedTargetOld)
                            {
                                view.UnGroupMachines(dstGroupMachine, true);
                            }
                        }

                        Mouse.OverrideCursor = null;
                    }
                    else
                    {
                        if (view.MachineGraph.CanConnectMachines(Machine, dstMachine.Machine))
                            Mouse.OverrideCursor = Cursors.UpArrow;
                        else
                        {
                            Mouse.OverrideCursor = Cursors.No;
                            dstMachine = null;
                        }
                    }

                    tempConnection.Destination = dstMachine;
                    tempConnection.UpdatePlugVisibility();
                    tempConnection.MousePoint = p;
                    tempConnection.UpdateVisuals();
                },
                EndDrag = _ =>
                {
                    int sc = tempConnection.SourcePlugInfo.Channel;
                    int dc = tempConnection.DestinationPlugInfo.Channel;
                    tempConnection.RemoveVisuals();
                    tempConnection = null;
                    Mouse.OverrideCursor = null;

                    if (dstMachine != null)
                    {
                        if (view.MachineGraph.CanConnectMachines(Machine, dstMachine.Machine))
                            view.MachineGraph.ConnectMachines(Machine, dstMachine.Machine, sc, dc, 0x4000, 0x4000);
                    }

                    if (isGroupedTargetOld && dstGroupMachine != null)
                    {
                        view.GroupMachines(dstGroupMachine, true);
                    }
                    dstGroupMachine = null;
                },
                CancelDrag = () =>
                {
                    tempConnection.RemoveVisuals();
                    tempConnection = null;
                    Mouse.OverrideCursor = null;

                    if (isGroupedTargetOld && dstGroupMachine != null)
                    {
                        view.GroupMachines(dstGroupMachine, true);
                    }
                    dstGroupMachine = null;
                }
            };

            this.KeyDown += (sender, e) =>
            {
                if (tempConnection != null)
                {
                    if (e.Key == Key.Tab)
                    {
                        if (tempConnection.Destination == null)
                            tempConnection.SourcePlugInfo.ChangeChannel();
                        else
                            tempConnection.DestinationPlugInfo.ChangeChannel();

                        e.Handled = true;
                    }
                }
            };

            this.MouseLeftButtonDown += (sender, e) =>
            {
                if (e.ClickCount == 2)
                {
                    machine.DoubleClick();
                    e.Handled = true;
                }
            };

            this.MouseRightButtonDown += (sender, e) =>
            {
                Focus();
                //				if (!IsSelected)
                //					view.SetSelection(this, false);

                CreateMenu();
                /*
				Point p = e.GetPosition(view);
				machine.ShowContextMenu((int)p.X, (int)p.Y);
				e.Handled = true;
				*/
            };

            this.GotKeyboardFocus += (sender, e) =>
            {
                if (!IsSelected)
                    view.SetSelection(this, false);
            };

            this.DragEnter += (sender, e) => { UpdateDragEffect(e); };
            this.DragOver += (sender, e) => { UpdateDragEffect(e); };
            this.Drop += (sender, e) =>
            {
                if (e.Data.GetDataPresent(typeof(MachineListItemVM)))
                {
                    var mli = e.Data.GetData(typeof(MachineListItemVM)) as MachineListItemVM;
                    if (mli != null)
                    {
                        Machine.Graph.ReplaceMachine(Machine, mli.Instrument.MachineDLL.Name, mli.Instrument.Name, Machine.Position.Item1, machine.Position.Item2);
                        e.Handled = true;
                    }
                }

                if (e.Data.GetDataPresent(typeof(TemplateListItemVM)))
                {
                    var tli = e.Data.GetData(typeof(TemplateListItemVM)) as TemplateListItemVM;
                    if (tli != null)
                    {
                        view.PasteTemplate(tli.Path, new Point(Machine.Position.Item1, Machine.Position.Item2), Machine);
                        e.Handled = true;
                    }
                }

            };

        }

        void MachineControl_GotFocus(object sender, RoutedEventArgs e)
        {
            BringToTop();

            if (Machine.DLL.Info.Type == MachineType.Generator || Machine.IsControlMachine)
                Machine.Graph.Buzz.MIDIFocusMachine = Machine;

            e.Handled = true;
        }



        public Rect Rect { get { return view.machineCanvas.GetRect(this); } }
        public Rect BorderRect
        {
            get
            {
                Rect r = view.machineCanvas.GetRect(this);
                if (BorderPart != null)
                    r.Size = BorderPart.DesiredSize;

                return r;
            }
        }

        public bool HasShadow { get { return MachineView.Settings.Shadows == ShadowModes.Machines; } }
        public bool HasSkin { get { return machine.DLL.Skin != null && MachineView.Settings.EnableSkins; } }
        public bool HasSkinLED { get { return machine.DLL.SkinLED != null && MachineView.Settings.EnableSkins; } }
        public bool IsLEDActive { get { return !HasSkinLED && machine.IsActive; } }
        public bool IsSkinLEDActive { get { return HasSkinLED && machine.IsActive; } }
        public ImageSource MachineImageSource
        {
            get
            {
                if (MachineView.Settings.ShowEngineThreads) return null;
                return HasSkin ? Machine.DLL.Skin : ThemeMachineImageSource;
            }
        }
        public bool HasMachineImageSource { get { return MachineImageSource != null; } }
        public Color MachineTextColor { get { return MachineView.Settings.EnableSkins ? machine.DLL.TextColor : machine.Graph.Buzz.ThemeColors["MV Machine Text"]; } }

        public string NameText
        {
            get
            {
                string s = Machine.Name;
                if (Machine.IsMuted) s = '(' + s + ')';
                if (Machine.IsBypassed) s = '<' + s + '>';
                bool solomode = view.Machines.Where(m => m.Machine.IsSoloed).Any();
                if (solomode && Machine.DLL.Info.Type == MachineType.Generator && !Machine.IsControlMachine && !Machine.IsSoloed) s = '[' + s + ']';
                return s;
            }
        }

        public string ModeText
        {
            get
            {
                int m = machine.MIDIInputChannel;
                int o = machine.OversampleFactor;

                string s = "";

                if (machine.DLL.IsOutOfProcess)
                    s += "32 ";
                else if (machine.DLL.IsManaged)
                    s += ".NET ";

                if (m >= 0)
                {
                    if (m == 0)
                        s += "MIDI A";
                    else
                        s += "MIDI " + m.ToString();
                }

                if (o > 1)
                {
                    if (s.Length > 0)
                        s += ", ";

                    s += "2x";
                }

                return s.Length > 0 ? s : null;
            }
        }

        public string ErrorText
        {
            get
            {
                if (machine.DLL.IsMissing)
                    return "missing";
                else if (machine.DLL.IsCrashed)
                    return "crashed";
                else
                    return null;
            }
        }

        public Color MachineBackgroundColor
        {
            get
            {
                if (MachineView.Settings.ShowEngineThreads)
                {
                    switch (Machine.LastEngineThread)
                    {
                        case 0: return Colors.Yellow;
                        case 1: return Colors.Blue;
                        case 2: return Colors.Red;
                        case 3: return Colors.Purple;
                        case 4: return Colors.Orange;
                        case 5: return Colors.Green;
                        case 6: return Colors.Brown;
                        case 7: return Colors.Cyan;
                        default: return Colors.Gray;
                    }
                }

                return machine.GetThemeColor();
            }
        }

        public Color MachineLEDOnColor
        {
            get
            {
                if (!Machine.IsControlMachine)
                {
                    switch (Machine.DLL.Info.Type)
                    {
                        case MachineType.Master: return Machine.Graph.Buzz.ThemeColors["MV Master LED On"];
                        case MachineType.Generator: return Machine.Graph.Buzz.ThemeColors["MV Generator LED On"];
                        case MachineType.Effect: return Machine.Graph.Buzz.ThemeColors["MV Effect LED On"];
                    }
                }

                return Colors.LightCyan;
            }
        }

        public Color MachineLEDOffColor
        {
            get
            {
                if (!Machine.IsControlMachine)
                {
                    switch (Machine.DLL.Info.Type)
                    {
                        case MachineType.Master: return Machine.Graph.Buzz.ThemeColors["MV Master LED Off"];
                        case MachineType.Generator: return Machine.Graph.Buzz.ThemeColors["MV Generator LED Off"];
                        case MachineType.Effect: return Machine.Graph.Buzz.ThemeColors["MV Effect LED Off"];
                    }
                }

                return Colors.LightCyan;
            }
        }

        Tuple<float, float> oldPosition = new Tuple<float, float>(0, 0);
        public Tuple<float, float> OldPosition { get => oldPosition; internal set => oldPosition = value; }

        internal static BuzzGUI.Common.Templates.Template CachedTemplate;

        void UpdateDragEffect(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(MachineListItemVM)))
            {
                var mli = e.Data.GetData(typeof(MachineListItemVM)) as MachineListItemVM;

                bool ok = false;

                switch (mli.Instrument.Type)
                {
                    case InstrumentType.Generator: ok = Machine.DLL.Info.Type == MachineType.Generator; break;
                    case InstrumentType.Effect: ok = Machine.DLL.Info.Type == MachineType.Effect; break;
                    case InstrumentType.Control: ok = Machine.IsControlMachine; break;
                }

                e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;

            }
            else if (e.Data.GetDataPresent(typeof(TemplateListItemVM)))
            {
                var tli = e.Data.GetData(typeof(TemplateListItemVM)) as TemplateListItemVM;

                try
                {

                    if (CachedTemplate == null || CachedTemplate.Path != tli.Path)
                    {
                        Mouse.OverrideCursor = Cursors.Wait;
                        CachedTemplate = BuzzGUI.Common.Templates.Template.Load(tli.Path);
                    }

                    var sm = CachedTemplate.SourceMachine;

                    if (sm != null && sm.Preset.Machine == Machine.DLL.Name)
                        e.Effects = DragDropEffects.Move;
                    else
                        e.Effects = DragDropEffects.None;

                }
                catch (Exception)
                {
                    e.Effects = DragDropEffects.None;
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        internal void UpdateWirelessOutputs()
        {
            PropertyChanged.Raise(this, "WirelessOutputs");
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
