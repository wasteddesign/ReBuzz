using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
//using PropertyChanged;

namespace BuzzGUI.MachineView
{
    //[DoNotNotify]
    public class Connection : INotifyPropertyChanged
    {
        public IMachineConnection MachineConnection { get; set; }
        public MachineControl Source { get; set; }
        public MachineControl Destination { get; set; }
        public Point MousePoint { get; set; }
        public Control ConnectionControl { get; set; }
        public IMachineGraph MachineGraph { get; set; }

        readonly MachineView machineView;
        Path path;
        Path path2;
        Control srcPlug;
        Control dstPlug;
        readonly Canvas canvas;
        readonly LineGeometry lineg;
        readonly LineGeometry lineg2;
        readonly PathGeometry pathg;
        readonly PathFigure pathf;
        readonly BezierSegment bez;

        bool curvedWires;
        public bool CurvedWires
        {
            get { return curvedWires; }
            set
            {
                curvedWires = value;

                if (value)
                {
                    path.Data = pathg;
                    if (path2 != null) path2.Data = pathg;
                }
                else
                {
                    path.Data = lineg;
                    if (path2 != null) path2.Data = lineg;
                }
            }
        }

        public int PlugSnapping
        {
            get;
            set;
        }

        bool ShowSrcPlug
        {
            get;
            set;
        }

        bool ShowDstPlug
        {
            get;
            set;
        }

        // [DoNotNotify]
        public class PlugInfo : INotifyPropertyChanged
        {
            public Connection Connection { get; set; }
            public MachineControl Machine { get; set; }

            int channel;
            public int Channel
            {
                get { return channel; }
                set
                {
                    channel = value;
                    PropertyChanged.Raise(this, "Channel");
                    PropertyChanged.Raise(this, "ChannelName");
                }
            }

            public string ChannelName { get { return Machine.Machine.GetChannelName(Machine == Connection.Destination, Channel); } }

            public void ChangeChannel()
            {
                int nch = (Machine == Connection.Source) ? Machine.Machine.OutputChannelCount : Machine.Machine.InputChannelCount;
                if (nch > 0)
                    Channel = (Channel + 1) % nch;
            }

            #region INotifyPropertyChanged Members

            public event PropertyChangedEventHandler PropertyChanged;

            public void OnPropertyChanged(string name)
            {
                PropertyChanged.Raise(this, name);
            }

            #endregion
        }

        public PlugInfo SourcePlugInfo { get; set; }
        public PlugInfo DestinationPlugInfo { get; set; }

        public bool IsConnecting { get { return Destination == null; } }

        public char WirelessID { get { return Destination.Machine.Name[0]; } }
        public void UpdateWirelessID() { PropertyChanged.Raise(this, "WirelessID"); }

        public Point Center
        {
            get
            {
                return new Point(
                    0.5 * (Source.Machine.Position.Item1 + Destination.Machine.Position.Item1),
                    0.5 * (Source.Machine.Position.Item2 + Destination.Machine.Position.Item2));
            }
        }

        public ICommand MouseDownCommand { get; set; }

        public Connection(IMachineConnection mc, MachineView mv, MachineControl src, MachineControl dst, Point p)
        {
            this.MachineConnection = mc;
            this.canvas = mv.connectionCanvas;
            this.Source = src;
            this.Destination = dst;
            this.MachineGraph = mv.MachineGraph;
            machineView = mv;
            SourcePlugInfo = new PlugInfo();
            SourcePlugInfo.Connection = this;
            SourcePlugInfo.Machine = src;
            DestinationPlugInfo = new PlugInfo();
            DestinationPlugInfo.Connection = this;
            DestinationPlugInfo.Machine = dst;

            if (mc != null)
            {
                DestinationPlugInfo.Channel = mc.DestinationChannel;
                SourcePlugInfo.Channel = mc.SourceChannel;
                mc.PropertyChanged += new PropertyChangedEventHandler(mc_PropertyChanged);
            }

            MousePoint = p;

            bez = new BezierSegment();

            pathf = new PathFigure();
            pathf.Segments.Add(bez);

            pathg = new PathGeometry();
            pathg.Figures.Add(pathf);

            lineg = new LineGeometry();
            lineg2 = new LineGeometry();

            path = new Path()
            {
                DataContext = this,
                Style = canvas.TryFindResource("MachineConnectionPathStyle") as Style
            };

            Style ips = canvas.TryFindResource("MachineConnectionInnerPathStyle") as Style;

            if (ips != null)
            {
                path2 = new Path()
                {
                    DataContext = this,
                    Style = ips
                };
            }

            ConnectionControl = new Control()
            {
                DataContext = this,
                Style = canvas.TryFindResource("MachineConnectionTriangleStyle") as Style,
                RenderTransform = new RotateTransform(),
            };



            if (mc != null)
            {
                ConnectionControl.MouseDown += (sender, e) =>
                {
                    HandleMouseDown(e);
                };

                ConnectionControl.AllowDrop = true;
                ConnectionControl.DragEnter += (sender, e) => { UpdateDragEffect(e); };
                ConnectionControl.DragOver += (sender, e) => { UpdateDragEffect(e); };
                ConnectionControl.Drop += (sender, e) =>
                {
                    if (e.Data.GetDataPresent(typeof(MachineListItemVM)))
                    {
                        var mli = e.Data.GetData(typeof(MachineListItemVM)) as MachineListItemVM;

                        MachineGraph.InsertMachine(mc, mli.Instrument.MachineDLL.Name, mli.Instrument.Name,
                            0.5f * (Source.Machine.Position.Item1 + Destination.Machine.Position.Item1),
                            0.5f * (Source.Machine.Position.Item2 + Destination.Machine.Position.Item2));

                        e.Handled = true;
                    }
                    else if (e.Data.GetDataPresent(typeof(MDBTab.MachineListItemVM)))
                    {
                        var mli = e.Data.GetData(typeof(MDBTab.MachineListItemVM)) as MDBTab.MachineListItemVM;

                        MachineGraph.InsertMachine(mc, mli.Instrument.MachineDLL.Name, mli.Instrument.Name,
                            0.5f * (Source.Machine.Position.Item1 + Destination.Machine.Position.Item1),
                            0.5f * (Source.Machine.Position.Item2 + Destination.Machine.Position.Item2));

                        e.Handled = true;
                    }
                };


            }

            srcPlug = new Control() { DataContext = SourcePlugInfo, Style = canvas.TryFindResource("PlugStyle") as Style };
            dstPlug = new Control() { DataContext = DestinationPlugInfo, Style = canvas.TryFindResource("PlugStyle") as Style };

            if (mv.Settings.ShowCurvedWires)
            {
                PlugSnapping = 4;
                CurvedWires = true;
            }
            else
            {
                PlugSnapping = 0;
                CurvedWires = false;
            }


            UpdatePlugVisibility();

            if (mc != null)     // if not temp. connection
            {
                if (ShowSrcPlug) CreateChannelMenu(srcPlug, false, Source.Machine);
                if (ShowDstPlug) CreateChannelMenu(dstPlug, true, Destination.Machine);
            }

            UpdateVisuals();

            canvas.Children.Add(path);
            if (path2 != null) canvas.Children.Add(path2);

            canvas.Children.Add(ConnectionControl);
            canvas.Children.Add(srcPlug);
            canvas.Children.Add(dstPlug);

            if (mc != null && mc.Destination.IsWireless)
                IsVisible = false;

            MouseDownCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => true,
                ExecuteDelegate = _e => { HandleMouseDown(_e as MouseButtonEventArgs); }
            };
        }

        void HandleMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                Disconnect();
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Right)
            {
                machineView.ampControl.Activate(this, e.GetPosition(machineView.ampCanvas), e.ChangedButton == MouseButton.Left ? e : null);
                e.Handled = true;
            }
        }

        void UpdateDragEffect(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(MachineListItemVM)))
            {
                var mli = e.Data.GetData(typeof(MachineListItemVM)) as MachineListItemVM;
                e.Effects = mli.Instrument.Type == InstrumentType.Effect ? DragDropEffects.Copy : DragDropEffects.None;
            }
            else if (e.Data.GetDataPresent(typeof(MDBTab.MachineListItemVM)))
            {
                var mli = e.Data.GetData(typeof(MDBTab.MachineListItemVM)) as MDBTab.MachineListItemVM;
                e.Effects = mli.Instrument.Type == InstrumentType.Effect ? DragDropEffects.Copy : DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        void CreateChannelMenu(Control plug, bool dst, IMachine m)
        {
            plug.ContextMenu = new ContextMenu();
            plug.ContextMenuOpening += (_s, _e) =>
            {
                int sel = dst ? MachineConnection.DestinationChannel : MachineConnection.SourceChannel;
                int count = dst ? m.InputChannelCount : m.OutputChannelCount;
                ContextMenu cm = new ContextMenu();
                for (int i = 0; i < count; i++)
                {
                    MenuItem mi = new MenuItem() { Header = string.Format("{0}. {1}", i, m.GetChannelName(dst, i)), Tag = i, IsChecked = i == sel };
                    mi.Click += (sender, e) => { MachineGraph.SetConnectionChannel(MachineConnection, dst, (int)mi.Tag); };
                    cm.Items.Add(mi);
                }

                plug.ContextMenu = cm;

            };
        }

        void mc_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "SourceChannel":
                    SourcePlugInfo.Channel = MachineConnection.SourceChannel;
                    break;
                case "DestinationChannel":
                    DestinationPlugInfo.Channel = MachineConnection.DestinationChannel;
                    break;

            }
        }

        public void UpdatePlugVisibility()
        {
            ShowSrcPlug = Source != null && (Source.Machine.OutputChannelCount > 1 || Source.Machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.ALWAYS_SHOW_PLUGS));
            ShowDstPlug = Destination != null && (Destination.Machine.InputChannelCount > 1 || Destination.Machine.DLL.Info.Flags.HasFlag(MachineInfoFlags.ALWAYS_SHOW_PLUGS));
            SourcePlugInfo.Machine = Source;
            DestinationPlugInfo.Machine = Destination;
        }

        Visibility visibility = Visibility.Visible;
        bool isVisible = true;
        public bool IsVisible
        {
            get { return isVisible; }
            set
            {
                if (value == isVisible) return;

                isVisible = value;
                visibility = IsVisible ? Visibility.Visible : Visibility.Hidden;
                lastSrcRect = Rect.Empty;
                UpdateVisuals();
                PropertyChanged.Raise(this, "IsVisible");
            }
        }


        public void RemoveVisuals()
        {
            if (MachineConnection != null)
                MachineConnection.PropertyChanged -= new PropertyChangedEventHandler(mc_PropertyChanged);

            canvas.Children.Remove(path);
            canvas.Children.Remove(path2);
            canvas.Children.Remove(ConnectionControl);
            canvas.Children.Remove(srcPlug);
            canvas.Children.Remove(dstPlug);

            // apparently styles hold references to elements so must null to avoid memory leaks
            path = null;
            path2 = null;
            ConnectionControl = null;
            srcPlug = null;
            dstPlug = null;
        }

        public void Disconnect()
        {
            machineView.MachineGraph.DisconnectMachines(MachineConnection);
        }

        public void Insert()
        {
            machineView.InsertMachinePopup(MachineConnection);
        }

        public void Invalidate()
        {
            lastSrcRect = lastDstRect = Rect.Empty;
            UpdateVisuals();
        }

        Rect lastSrcRect, lastDstRect;

        public void UpdateVisuals()
        {
            Rect srcr = Source.BorderRect;
            Rect dstr = Destination != null ? Destination.BorderRect : new Rect(MousePoint.X, MousePoint.Y, 1, 1);

            if (srcr.Equals(lastSrcRect) && dstr.Equals(lastDstRect))
                return;

            lastSrcRect = srcr;
            lastDstRect = dstr;


            Point sp = srcr.GetCenter();
            Point dp = dstr.GetCenter();

            if (srcr.IntersectsWith(dstr))
            {
                srcPlug.Visibility = Visibility.Collapsed;
                dstPlug.Visibility = Visibility.Collapsed;
                ConnectionControl.Visibility = Visibility.Collapsed;
                path.Visibility = Visibility.Collapsed;
                if (path2 != null) path2.Visibility = Visibility.Collapsed;
            }
            else
            {
                srcPlug.Visibility = ShowSrcPlug ? visibility : Visibility.Collapsed;
                dstPlug.Visibility = ShowDstPlug ? visibility : Visibility.Collapsed;
                ConnectionControl.Visibility = visibility;
                path.Visibility = visibility;
                if (path2 != null) path2.Visibility = visibility;

            }

            if (ShowSrcPlug && !machineView.Settings.ChannelOnWire) srcr.Inflate(srcPlug.Width / 2, srcPlug.Height / 2);
            if (ShowDstPlug && !machineView.Settings.ChannelOnWire) dstr.Inflate(srcPlug.Width / 2, srcPlug.Height / 2);

            //            Clipper.ClipLineByTwoRoundedRects(ref fp, ref tp, fromr, tor, from.border.CornerRadius.TopLeft + fromcircle.Width / 2 + 1);

            Point cp1, cp2;

            cp1 = sp;
            cp2 = dp;
            Vector svec = SnapPlug(ref cp1, ref cp2);

            if (Source != null && Source.BorderPart != null)
                Clipper.ClipLineByTwoRoundedRects(ref cp1, ref cp2, srcr, dstr, Source.BorderPart.CornerRadius.TopLeft + srcPlug.Width / 2 + 1);
            else
                Clipper.ClipLineByTwoRoundedRects(ref cp1, ref cp2, srcr, dstr, srcPlug.Width / 2 + 1);

            Point dcp1, dcp2;
            dcp1 = dp;
            dcp2 = sp;
            Vector dvec = SnapPlug(ref dcp1, ref dcp2);

            if (Destination != null && Destination.BorderPart != null)
                Clipper.ClipLineByTwoRoundedRects(ref dcp1, ref dcp2, dstr, srcr, Destination.BorderPart.CornerRadius.TopLeft + dstPlug.Width / 2 + 1);
            else
                Clipper.ClipLineByTwoRoundedRects(ref dcp1, ref dcp2, dstr, srcr, srcPlug.Width / 2 + 1);

            sp = cp1;
            dp = dcp1;

            double len = (dp - sp).Length;

            //          lineg.StartPoint = fp;
            //            lineg.EndPoint = tp;

            pathf.StartPoint = sp;
            bez.Point3 = dp;

            Vector vn = (Vector)dp - (Vector)sp;
            vn.Normalize();

            double scale = Math.Min(100, 0.33 * len);

            bez.Point1 = sp + svec * scale;
            bez.Point2 = dp + dvec * scale;

            lineg.StartPoint = sp;
            lineg.EndPoint = dp;


            if (machineView.Settings.ChannelOnWire)
            {
                var spp = GetPointOnBezierSegment(pathf.StartPoint, bez, 0.25);
                var dpp = GetPointOnBezierSegment(pathf.StartPoint, bez, 0.75);

                Canvas.SetLeft(srcPlug, spp.X - srcPlug.Width / 2);
                Canvas.SetTop(srcPlug, spp.Y - srcPlug.Height / 2);

                Canvas.SetLeft(dstPlug, dpp.X - dstPlug.Width / 2);
                Canvas.SetTop(dstPlug, dpp.Y - dstPlug.Height / 2);
            }
            else
            {
                Canvas.SetLeft(srcPlug, sp.X - srcPlug.Width / 2);
                Canvas.SetTop(srcPlug, sp.Y - srcPlug.Height / 2);

                Canvas.SetLeft(dstPlug, dp.X - dstPlug.Width / 2);
                Canvas.SetTop(dstPlug, dp.Y - dstPlug.Height / 2);
            }



            Point m = new Point(0.5 * (sp.X + dp.X), 0.5 * (sp.Y + dp.Y));

            Point n;

            if (CurvedWires)
                n = (Point)MiddleNormal(pathf.StartPoint, bez);
            else
                n = new Point(dp.Y - sp.Y, sp.X - dp.X);

            Canvas.SetLeft(ConnectionControl, m.X);
            Canvas.SetTop(ConnectionControl, m.Y);
            (ConnectionControl.RenderTransform as RotateTransform).Angle = Vector.AngleBetween(new Vector(-1, 0), (Vector)n);

        }

        Vector MiddleNormal(Point p, BezierSegment b)
        {
            Vector v1 = (Vector)p;
            Vector v2 = (Vector)b.Point1;
            Vector v3 = (Vector)b.Point2;
            Vector v4 = (Vector)b.Point3;

            Vector t1 = 0.5 * (v1 + v2);
            Vector t2 = 0.5 * (v2 + v3);
            Vector t3 = 0.5 * (v3 + v4);

            Vector u1 = 0.5 * (t1 + t2);
            Vector u2 = 0.5 * (t2 + t3);

            return new Vector(u2.Y - u1.Y, u1.X - u2.X);
        }


        Vector SnapPlug(ref Point p1, ref Point p2)
        {
            int snap = PlugSnapping;

            Vector v = (Vector)p2 - (Vector)p1;
            double a = Vector.AngleBetween(v, new Vector(0, 1));

            if (snap > 0)
                a = Math.Round(a * snap / 360) * 360.0 / snap;

            Vector q = new Vector(Math.Sin(a * Math.PI / 180.0), Math.Cos(a * Math.PI / 180.0));
            p2 = p1 + 10000 * q;

            return q;
        }

        Point GetPointOnBezierSegment(Point p, BezierSegment b, double t)
        {
            Vector v1 = (Vector)p;
            Vector v2 = (Vector)b.Point1;
            Vector v3 = (Vector)b.Point2;
            Vector v4 = (Vector)b.Point3;
            return (Point)((1 - t) * (1 - t) * (1 - t) * v1 + 3 * t * (1 - t) * (1 - t) * v2 + 3 * t * t * (1 - t) * v3 + t * t * t * v4);
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
