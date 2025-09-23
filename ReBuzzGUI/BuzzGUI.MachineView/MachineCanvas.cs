using BuzzGUI.Common;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace BuzzGUI.MachineView
{
    public class MachineCanvas : Panel
    {
        public static int ZIndex = 0;
        internal static bool IsDoubleFiniteOrNaN(object o)
        {
            double d = (double)o;
            return !double.IsInfinity(d);
        }

        public static readonly DependencyProperty PositionProperty = DependencyProperty.RegisterAttached("Position", typeof(Point), typeof(MachineCanvas), new FrameworkPropertyMetadata(new Point(0, 0), new PropertyChangedCallback(MachineCanvas.OnPositioningChanged)));

        public static Point GetPosition(UIElement element)
        {
            if (element == null)
                throw new ArgumentNullException("element");

            return (Point)element.GetValue(PositionProperty);
        }

        public static void SetPosition(UIElement element, Point p)
        {
            if (element == null)
                throw new ArgumentNullException("element");

            p.X = (float)p.X;
            p.Y = (float)p.Y;

            if (p != GetPosition(element))
                element.SetValue(PositionProperty, p);
        }



        private static void OnPositioningChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MachineControl)
            {
                MachineControl reference = d as MachineControl;
                if (reference != null)
                {
                    var parent = VisualTreeHelper.GetParent(reference) as MachineCanvas;
                    if (parent != null)
                        parent.InvalidateArrange();

                }
            }
            else if (d is GroupControl)
            {
                GroupControl reference = d as GroupControl;
                if (reference != null)
                {
                    var parent = VisualTreeHelper.GetParent(reference) as MachineCanvas;
                    if (parent != null)
                        parent.InvalidateArrange();

                }
            }
        }

        protected override Size MeasureOverride(Size constraint)
        {
            Size availableSize = new Size(double.PositiveInfinity, double.PositiveInfinity);
            foreach (UIElement element in base.InternalChildren)
                if (element != null)
                    element.Measure(availableSize);

            return new Size();
        }

        int snap = 0;

        public double CanvasSize { get; set; }
        public int Snap { get { return snap; } set { snap = value; } }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            foreach (var element in base.InternalChildren)
                if (element is MachineControl)
                {
                    var el = element as MachineControl;
                    el?.Arrange(GetRect(el));
                }
                else if (element is GroupControl)
                {
                    var el = element as GroupControl;
                    el?.Arrange(GetRect(el));
                }

            foreach (var element in base.InternalChildren)
                if (element is MachineControl)
                {
                    var el = element as MachineControl;
                    el.UpdateConnectionVisuals();
                }
            return arrangeSize;
        }

        public Rect GetRect(MachineControl mc)
        {
            UIElement border = mc;
            if (mc.BorderPart != null) border = mc.BorderPart;

            double cx = Width / 2;
            double cy = Height / 2;
            double sx = cx / CanvasSize;
            double sy = cy / CanvasSize;

            Point p = GetPosition(mc);

            double fx = Math.Round(cx + p.X * sx - border.DesiredSize.Width / 2);
            double fy = Math.Round(cy + p.Y * sy - border.DesiredSize.Height / 2);

            if (snap > 0)
            {
                fx = Math.Round(fx / snap) * snap;
                fy = Math.Round(fy / snap) * snap;
            }

            return new Rect(new Point(fx, fy), mc.DesiredSize);
        }

        public Point GetCanvasPos(Point p)
        {
            double cx = Width / 2;
            double cy = Height / 2;
            double sx = cx / CanvasSize;
            double sy = cy / CanvasSize;

            double fx = Math.Round(cx + p.X * sx);
            double fy = Math.Round(cy + p.Y * sy);

            if (snap > 0)
            {
                fx = Math.Round(fx / snap) * snap;
                fy = Math.Round(fy / snap) * snap;
            }

            return new Point(fx, fy);
        }

        public Rect GetRect(GroupControl mc)
        {
            UIElement border = mc;
            if (mc.BorderPart != null) border = mc.BorderPart;

            double cx = Width / 2;
            double cy = Height / 2;
            double sx = cx / CanvasSize;
            double sy = cy / CanvasSize;

            Point p = GetPosition(mc);

            double fx = Math.Round(cx + p.X * sx - border.DesiredSize.Width / 2);
            double fy = Math.Round(cy + p.Y * sy - border.DesiredSize.Height / 2);

            if (snap > 0)
            {
                fx = Math.Round(fx / snap) * snap;
                fy = Math.Round(fy / snap) * snap;
            }

            return new Rect(new Point(fx, fy), mc.DesiredSize);
        }

        public void Drag(MachineControl e, Point d)
        {
            Point center = new Point(Width / 2, Height / 2);
            Vector scale = (Vector)center / CanvasSize;

            Point p = e.BeginDragPosition;
            p += ((Vector)d).ElementDiv(scale);
            p = p.Clamp(-CanvasSize, CanvasSize);

            SetPosition(e, p);
        }

        public void Drag(GroupControl e, Point d)
        {
            Point center = new Point(Width / 2, Height / 2);
            Vector scale = (Vector)center / CanvasSize;

            Point p = e.BeginDragPosition;
            p += ((Vector)d).ElementDiv(scale);
            p = p.Clamp(-CanvasSize, CanvasSize);

            SetPosition(e, p);

        }

        public Point GetPositionAtPoint(Point p)
        {
            Point center = new Point(Width / 2, Height / 2);
            Vector scale = (Vector)center / CanvasSize;
            return (Point)((p - center).ElementDiv(scale));
        }

        public Point GetPointAtPosition(Point p)
        {
            Point center = new Point(Width / 2, Height / 2);
            Vector scale = (Vector)center / CanvasSize;
            return center + scale.ElementMul((Vector)p);
        }

        public Vector ScaleToPixels(Vector v)
        {
            Point center = new Point(Width / 2, Height / 2);
            Vector scale = (Vector)center / CanvasSize;
            return scale.ElementMul(v);
        }

        public MachineControl GetMachineAtPoint(Point p)
        {
            object e = InputHitTest(p);

            while (true)
            {
                if (e == null || e == this) return null;
                if (e is MachineControl) return e as MachineControl;
                if (e is GroupControl)
                {
                    var g = (GroupControl)e;
                    var inputMachine = g.MachineGroup.MainInputMachine;
                    if (inputMachine != null)
                        return g.MachineView.GetMachineControl(inputMachine);
                }
                e = VisualTreeHelper.GetParent(e as DependencyObject);
            }
        }

        public GroupControl GetMachineGroupAtPoint(Point p)
        {
            object e = InputHitTest(p);

            while (true)
            {
                if (e == null || e == this) return null;
                if (e is GroupControl) return e as GroupControl;
                e = VisualTreeHelper.GetParent(e as DependencyObject);
            }
        }


    }
}
