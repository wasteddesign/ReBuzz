using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace BuzzGUI.MachineView
{
    //[DoNotNotify]
    public class GroupControl : Control, INotifyPropertyChanged
    {
        #region IsSelected
        public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register("IsSelected", typeof(bool), typeof(GroupControl),
            new FrameworkPropertyMetadata(false, new PropertyChangedCallback(OnIsSelectedChanged)));

        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        private static void OnIsSelectedChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            GroupControl control = (GroupControl)obj;
            bool value = (bool)args.NewValue;
        }
        #endregion

        #region ThemeGroupImageSource
        public static readonly DependencyProperty ThemeGroupImageSourceProperty = DependencyProperty.Register("ThemeGroupImageSource", typeof(ImageSource), typeof(GroupControl),
            new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnThemeGroupImageSourceChanged)));

        public ImageSource ThemeGroupImageSource
        {
            get { return (ImageSource)GetValue(ThemeGroupImageSourceProperty); }
            set { SetValue(ThemeGroupImageSourceProperty, value); }
        }

        private static void OnThemeGroupImageSourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            GroupControl control = (GroupControl)obj;
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

        static GroupControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GroupControl), new FrameworkPropertyMetadata(typeof(GroupControl)));
        }

        IMachineGroup machineGroup;
        public IMachineGroup MachineGroup
        {
            get { return machineGroup; }
            set
            {
                if (MachineGroup != null)
                {
                    machineGroup.PropertyChanged -= new System.ComponentModel.PropertyChangedEventHandler(machineGroup_PropertyChanged);
                }

                machineGroup = value;
                UpdatePosition();

                if (machineGroup != null)
                {
                    machineGroup.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(machineGroup_PropertyChanged);
                }
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            RootPart = (FrameworkElement)GetTemplateChild("Root");
            BorderPart = (Border)GetTemplateChild("Border");
        }

        readonly MachineView view;
        
        public string GroupText { get => MachineGroup.IsGrouped ? "Group" : "Group Open"; }
        public IList<string> MachineNames
        {
            get
            {
                List<string> names = new List<string>();
                var ml = view.Buzz.Song.MachineToGroupDict;
                foreach (var m in ml.Keys)
                {
                    if (ml[m] == MachineGroup)
                    {
                        names.Add(m.Name);
                    }

                }

                if (names.Count == 0)
                {
                    names.Add("Empty");
                }
                return names;
            }
        }

        void machineGroup_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Position":
                    UpdatePosition();
                    break;
                case "Name":
                    PropertyChanged.Raise(this, "NameText");
                    break;
                case "IsGrouped":
                    {
                        foreach (var m in Buzz.Song.MachineToGroupDict.Where(k => k.Value == this.MachineGroup).Select(s => s.Key))
                        {
                            view.GetMachineControl(m).Visibility = MachineGroup.IsGrouped ? Visibility.Collapsed : Visibility.Visible;
                        }
                        PropertyChanged.Raise(this, "GroupText");
                    }
                    break;
            }
        }

        public ICommand CloneCommand { get; private set; }
        public ICommand GroupMachinesCommand { get; private set; }
        public ICommand UnGroupMachinesCommand { get; private set; }
        public ICommand GroupRenameCommand { get; private set; }
        public ICommand GroupDeleteCommand { get; private set; }
        public ICommand SetGroupInputMachineCommand { get; private set; }
        public ICommand SetGroupOutputMachineCommand { get; private set; }

        void Commands()
        {
            CloneCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = includepatandseq =>
                {
                    view.CloneSelectedMachines((bool)includepatandseq);
                }
            };

            GroupMachinesCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => !MachineGroup.IsGrouped,
                ExecuteDelegate = x =>
                {
                    view.GroupMachines(this);
                }
            };

            UnGroupMachinesCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => MachineGroup.IsGrouped,
                ExecuteDelegate = x =>
                {
                    //isGroupedOld = false;
                    view.UnGroupMachines(this);
                }
            };

            GroupRenameCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    var p = this.PointToScreen(Mouse.GetPosition(this));
                    MachineGroup.ShowDialog(MachineGroupDialog.Rename, (int)p.X + 8, (int)p.Y - 60);
                }
            };

            GroupDeleteCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    view.UnGroupMachines(this);
                    view.Buzz.Song.DeleteMachineGroups([MachineGroup]);
                }
            };

            SetGroupInputMachineCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    MachineGroup.MainInputMachine = (IMachine)x;
                }
            };

            SetGroupOutputMachineCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = x =>
                {
                    MachineGroup.MainOutputMachine = (IMachine)x;
                }
            };
            //this.InputBindings.Add(new InputBinding(ShowDialogCommand, new KeyGesture(Key.Return)) { CommandParameter = MachineDialog.Patterns });
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

            var gIn = new MenuItemVM.Group();
            var gOut = new MenuItemVM.Group();

            List<MenuItemVM> availableInputs = new List<MenuItemVM>();
            List<MenuItemVM> availableOutputs = new List<MenuItemVM>();

            var inputList = MachineGroup.Machines.Where(m => m.DLL.Info.Type == MachineType.Effect);
            var outputList = MachineGroup.Machines.Where(m => m.DLL.Info.Type == MachineType.Effect || m.DLL.Info.Type == MachineType.Generator);

            availableInputs.Add(new MenuItemVM() { Text = "None", Command = SetGroupInputMachineCommand, IsCheckable = true, StaysOpenOnClick = true, CheckGroup = gIn, IsChecked = MachineGroup.MainInputMachine == null, IsEnabled = inputList.Count() > 0 });
            foreach (var machine in inputList)
            {   
                availableInputs.Add(new MenuItemVM() { Text = machine.Name, CommandParameter = machine, Command = SetGroupInputMachineCommand, IsCheckable = true, StaysOpenOnClick = true, CheckGroup = gIn, IsChecked = MachineGroup.MainInputMachine == machine });
            }

            availableOutputs.Add(new MenuItemVM() { Text = "None", Command = SetGroupOutputMachineCommand, IsCheckable = true, StaysOpenOnClick = true, CheckGroup = gOut, IsChecked = MachineGroup.MainOutputMachine == null, IsEnabled = outputList.Count() > 0 });
            foreach (var machine in outputList)
            {
                availableOutputs.Add(new MenuItemVM() { Text = machine.Name, CommandParameter = machine, Command = SetGroupOutputMachineCommand, IsCheckable = true, StaysOpenOnClick = true, CheckGroup = gOut, IsChecked = MachineGroup.MainOutputMachine == machine });
            }

            ContextMenu.ItemsSource = new List<MenuItemVM>()
            {
                new MenuItemVM() { Text = "Group", Command = GroupMachinesCommand },
                new MenuItemVM() { Text = "UnGroup", Command = UnGroupMachinesCommand },
                new MenuItemVM() { Text = "Default Input", Children = availableInputs},
                new MenuItemVM() { Text = "Default Output", Children = availableOutputs},
                new MenuItemVM() { IsSeparator = true },
                new MenuItemVM() { Text = "Rename...", Command = GroupRenameCommand, GestureText = "Ctrl+R" },
                new MenuItemVM() { Text = "Delete",  Command = GroupDeleteCommand, GestureText = "Ctrl+D" },
            };

            this.InputBindings.Add(new InputBinding(GroupRenameCommand, new KeyGesture(Key.R, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(GroupDeleteCommand, new KeyGesture(Key.D, ModifierKeys.Control)));
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

        public IBuzz Buzz { get; set; }

        readonly DispatcherTimer dtLED;
        public GroupControl(MachineView view)
        {   
            this.DataContext = this;
            this.view = view;
            this.Loaded += new RoutedEventHandler(GroupControl_Loaded);
            this.GotFocus += new RoutedEventHandler(GroupControl_GotFocus);
            MachineView.Settings.PropertyChanged += new PropertyChangedEventHandler(Settings_PropertyChanged);
            this.AllowDrop = true;

            Commands();

            dtLED = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1 / 30.0) };
            dtLED.Tick += (sender, e) =>
            {
                PropertyChanged?.Raise(this, HasSkinLED ? "IsSkinLEDActive" : "IsLEDActive");
            };
            dtLED.Start();
        }


        public void Release()
        {
            MachineView.Settings.PropertyChanged -= new PropertyChangedEventHandler(Settings_PropertyChanged);
            dtLED?.Stop();
        }

        void UpdatePosition()
        {   
            if (machineGroup == null) return;
            Tuple<float, float> p = MachineGroup.Position;
            MachineCanvas.SetPosition(this, new Point((float)p.Item1, (float)p.Item2));
        }

        public void UpdateConnectionVisuals()
        {
        }

        
        public void BringToTop() { Panel.SetZIndex(this, ++MachineCanvas.ZIndex); }


        internal Point BeginDragPosition { get; set; }

        /// <summary>
        /// Looks for a child control within a parent by name
        /// </summary>
        public static T FindChild<T>(DependencyObject parent, string childName)
        where T : DependencyObject
        {
            // Confirm parent and childName are valid.
            if (parent == null) return null;

            T foundChild = null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                // If the child is not of the request child type child
                T childType = child as T;
                if (childType == null)
                {
                    // recursively drill down the tree
                    foundChild = FindChild<T>(child, childName);

                    // If the child is found, break so we do not overwrite the found child.
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    // If the child's name is set for search
                    if (frameworkElement != null && frameworkElement.Name == childName)
                    {
                        // if the child's name is of the request name
                        foundChild = (T)child;
                        break;
                    }
                    else
                    {
                        // recursively drill down the tree
                        foundChild = FindChild<T>(child, childName);

                        // If the child is found, break so we do not overwrite the found child.
                        if (foundChild != null) break;
                    }
                }
                else
                {
                    // child element found.
                    foundChild = (T)child;
                    break;
                }
            }

            return foundChild;
        }

        void GroupControl_Loaded(object l_sender, RoutedEventArgs l_e)
        {
            var c = VisualTreeHelper.GetParent(this) as MachineCanvas;

            var border = FindChild<Border>(this, "Border");
            if (border != null)
            {
                border.ToolTipOpening += (s, e) =>
                {
                    PropertyChanged?.Raise(this, "MachineNames");
                };
            }

            /*
            // Make this a setting?
            MouseEnter += (s, e) =>
            {
                isGroupedOld = IsGrouped;
                if (IsGrouped)
                {
                    view.UnGroupMachines(this);
                }
            };

            MouseLeave += (s, e) =>
            {   
                if (isGroupedOld)
                {
                    view.GroupMachines(this);
                }
            };
            */
            // move

            var startPoint = new Point(0, 0);
            var lastDelta = new Point(0, 0);
            bool createTemplate = false;

            new Dragger
            {
                Element = this,
                Container = c,
                Gesture = new DragMouseGesture { Button = MouseButton.Left },
                AlternativeGesture = new DragMouseGesture { Button = MouseButton.Left, Modifiers = ModifierKeys.Alt },
                DragCursor = Cursors.Hand,
                Mode = DraggerMode.DeltaFromOrigin,
                BeginDrag = (p, a, cc) =>
                {
                    startPoint = p;
                    
                    view.BeginMoveSelectedGroups();
                    // Grouped machines move with groups
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

                    view.MoveSelectedGroups(delta);
                    if (!(Keyboard.Modifiers == ModifierKeys.Alt && MachineGroup.IsGrouped == false))
                        view.MoveGroupedMachines(delta);
                },
                EndDrag = _ =>
                {
                    if (lastDelta.X != 0.0 || lastDelta.Y != 0.0)
                    {
                        using (new ActionGroup(view.MachineGraph))
                        {
                            view.EndMoveSelectedGroups();
                            if (!(Keyboard.Modifiers == ModifierKeys.Alt && MachineGroup.IsGrouped == false))
                                view.EndMoveGroupedMachines();
                        }
                    }

                    //if (createTemplate)
                    //    view.CreateTemplate();
                },
                CancelDrag = () =>
                {
                    using (new ActionGroup(view.MachineGraph))
                    {
                        view.EndMoveSelectedGroups();
                        if (!(Keyboard.Modifiers == ModifierKeys.Alt && MachineGroup.IsGrouped == false))
                            view.EndMoveGroupedMachines();
                    }
                }
            };

            // connect
            MachineControl dstMachine = null;
            GroupControl dstGroupMachine = null;
            Connection tempConnection = null;
            bool isGroupedOld = false;
            bool isGroupedTargetOld = false;

            MachineControl? sourceMachine = null;

            new Dragger
            {
                Element = this,
                Container = c,
                Gesture = new DragMouseGesture { Button = MouseButton.Middle },
                AlternativeGesture = new DragMouseGesture { Button = MouseButton.Left, Modifiers = ModifierKeys.Shift },
                Mode = DraggerMode.Absolute,
                BeginDrag = (p, alt, cc) =>
                {
                    sourceMachine = null;
                    Focus();
                    if (MachineGroup.MainOutputMachine != null)
                    {
                        sourceMachine = view.GetMachineControl(MachineGroup.MainOutputMachine);

                        tempConnection = new Connection(null, view, sourceMachine, null, p);
                        dstMachine = null;
                    }
                    else
                    {
                        // Open group to select machine
                        isGroupedOld = this.MachineGroup.IsGrouped;
                        if (isGroupedOld)
                        {
                            view.UnGroupMachines(this, true);
                        }
                    }
                },
                Drag = p =>
                {
                    if (sourceMachine != null)
                    {
                        dstMachine = c.GetMachineAtPoint(p);
                        if (dstMachine == null || dstMachine == sourceMachine)
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
                            if (dstGroupMachineNew != null && dstGroupMachine != dstGroupMachineNew && dstGroupMachineNew != this)
                            {
                                dstGroupMachine = dstGroupMachineNew;
                                isGroupedTargetOld = dstGroupMachine.machineGroup.IsGrouped;
                                if (isGroupedTargetOld)
                                {   
                                    view.UnGroupMachines(dstGroupMachine, true);
                                }
                            }

                            Mouse.OverrideCursor = null;
                        }
                        else
                        {
                            if (view.MachineGraph.CanConnectMachines(sourceMachine.Machine, dstMachine.Machine))
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
                    }
                    else
                    {
                        var machineAtPoint = c.GetMachineAtPoint(p);
                        if (machineAtPoint != null && machineAtPoint != sourceMachine && MachineGroup.Machines.Contains(machineAtPoint.Machine) && !machineAtPoint.Machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.CONTROL_MACHINE))
                        {
                            sourceMachine = machineAtPoint;
                            tempConnection = new Connection(null, view, sourceMachine, null, p);
                            dstMachine = null;
                        }
                        else
                        {
                            sourceMachine = null;
                        }
                    }
                },
                EndDrag = _ =>
                {
                    if (sourceMachine != null)
                    {
                        int sc = tempConnection.SourcePlugInfo.Channel;
                        int dc = tempConnection.DestinationPlugInfo.Channel;
                        tempConnection.RemoveVisuals();
                        tempConnection = null;

                        if (dstMachine != null)
                        {
                            if (view.MachineGraph.CanConnectMachines(sourceMachine.Machine, dstMachine.Machine))
                                view.MachineGraph.ConnectMachines(sourceMachine.Machine, dstMachine.Machine, sc, dc, 0x4000, 0x4000);
                        }

                        sourceMachine = null;
                    }
                    Mouse.OverrideCursor = null;

                    if (isGroupedOld)
                    {
                        view.GroupMachines(this, true);
                    }

                    if (isGroupedTargetOld && dstGroupMachine != null)
                    {
                        view.GroupMachines(dstGroupMachine, true);
                    }
                    dstGroupMachine = null;

                },
                CancelDrag = () =>
                {
                    if (sourceMachine != null)
                    {
                        tempConnection.RemoveVisuals();
                        tempConnection = null;

                        sourceMachine = null;
                    }
                    Mouse.OverrideCursor = null;

                    if (isGroupedOld)
                    {
                        view.GroupMachines(this);
                    }
                    if (isGroupedTargetOld && dstGroupMachine != null)
                    {
                        view.GroupMachines(dstGroupMachine);
                    }
                    dstGroupMachine = null;
                }
            };

            this.MouseLeftButtonDown += (sender, e) =>
            {   
                if (e.ClickCount == 2)
                {
                    if (MachineGroup.IsGrouped)
                        view.UnGroupMachines(this);
                    else
                        view.GroupMachines(this);

                    e.Handled = true;
                }

                view.HideOtherGroupRect(this);
            };

            this.MouseRightButtonDown += (sender, e) =>
            {
                Focus();

                //if (!IsSelected)
                //	view.SetSelection(this, false);
                CreateMenu();
                /*
				Point p = e.GetPosition(view);
				machine.ShowContextMenu((int)p.X, (int)p.Y);
				e.Handled = true;
				*/
                e.Handled = true;
            };

            this.GotKeyboardFocus += (sender, e) =>
            {
                if (!IsSelected)
                    view.SetSelection(this, false);
            };

            this.DragEnter += (sender, e) => { UpdateDragEffect(e); };
            this.DragOver += (sender, e) => { UpdateDragEffect(e); };
            /*
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
            */
        }

        void GroupControl_GotFocus(object sender, RoutedEventArgs e)
        {
            BringToTop();

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
        public bool HasSkin { get { return false; } }
        public bool HasSkinLED { get { return false; } }
        public bool IsLEDActive { get
            {
                return IsChildMachineActibve();
            }
        }
        public bool IsSkinLEDActive { get { return IsChildMachineActibve(); } }

        private bool IsChildMachineActibve()
        {
            bool isActive = false;
            var list = view.Buzz.Song.MachineToGroupDict;
            foreach (var m in list.Keys)
            {
                if (list[m] == MachineGroup)
                {
                    isActive |= m.IsActive;
                }
            }

            return isActive;
        }

        public ImageSource MachineImageSource
        {
            get
            {
                if (MachineView.Settings.ShowEngineThreads) return null;
                return ThemeGroupImageSource;
            }
        }
        public bool HasMachineImageSource { get { return MachineImageSource != null; } }
        public Color MachineTextColor { get { return Global.Buzz.ThemeColors["MV Machine Text"]; } }

        public string NameText
        {
            get
            {
                string s = MachineGroup.Name;
                return s;
            }
        }

        public Color MachineBackgroundColor
        {
            get
            {
                return Buzz.ThemeColors["MV Control"];
            }
        }

        public Color MachineLEDOnColor
        {
            get
            {
                return Buzz.ThemeColors["MV Generator LED On"];
            }
        }

        public Color MachineLEDOffColor
        {
            get
            {
                return Buzz.ThemeColors["MV Generator LED Off"];
            }
        }

        void UpdateDragEffect(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(MachineControl)))
            {
                e.Effects = DragDropEffects.Move;

            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
