using System;
using System.Windows;
using System.Windows.Input;

namespace BuzzGUI.Common
{
    public enum DraggerMode { Delta, DeltaFromOrigin, Absolute };
    public class Dragger
    {
        public Action<Point, bool, int> BeginDrag { set; get; }
        public Action<Point> Drag { set; get; }
        public Action<Point> EndDrag { set; get; }
        public Action CancelDrag { set; get; }
        public DragMouseGesture Gesture { set; get; }
        public DragMouseGesture AlternativeGesture { set; get; }
        public Cursor DragCursor { set; get; }
        public Cursor MouseOverCursor { set; get; }
        public UIElement Container { set; get; }
        public DraggerMode Mode { set; get; }

        public UIElement Element
        {
            set
            {
                Point lastMousePos = new Point(0, 0);
                Point origMousePos = new Point(0, 0);
                bool dragging = false;
                MouseButton? button = null;

                value.MouseEnter += (sender, e) => { if (MouseOverCursor != null) Mouse.OverrideCursor = MouseOverCursor; };
                value.MouseLeave += (sender, e) => { if (MouseOverCursor != null) Mouse.OverrideCursor = null; };

                value.MouseDown += (sender, e) =>
                {
                    if (!dragging)
                    {
                        bool altg = false;

                        if (Gesture.Matches(e))
                        {
                            button = Gesture.Button;
                        }
                        else if (AlternativeGesture != null && AlternativeGesture.Matches(e))
                        {
                            button = AlternativeGesture.Button;
                            altg = true;
                        }
                        else
                        {
                            button = null;
                        }

                        if (button != null)
                        {
                            dragging = true;
                            origMousePos = lastMousePos = e.GetPosition(Container != null ? Container : value);
                            value.CaptureMouse();
                            if (DragCursor != null) Mouse.OverrideCursor = DragCursor;
                            e.Handled = true;
                            if (BeginDrag != null) BeginDrag(lastMousePos, altg, e.ClickCount);
                        }
                    }
                };

                value.MouseUp += (sender, e) =>
                {
                    if (e.ChangedButton == button)
                    {
                        if (dragging)
                        {
                            dragging = false;
                            value.ReleaseMouseCapture();
                            if (DragCursor != null) Mouse.OverrideCursor = MouseOverCursor;
                            e.Handled = true;
                            if (EndDrag != null) EndDrag(lastMousePos);
                        }
                    }
                };

                value.MouseMove += (sender, e) =>
                {
                    if (dragging)
                    {
                        e.Handled = true;
                        Point p = e.GetPosition(Container != null ? Container : value);
                        if (p != lastMousePos)
                        {
                            if (Mode == DraggerMode.Absolute)
                                Drag(p);
                            else if (Mode == DraggerMode.DeltaFromOrigin)
                                Drag((Point)(p - origMousePos));
                            else
                                Drag((Point)(p - lastMousePos));

                            lastMousePos = p;
                        }

                    }

                };

                value.LostMouseCapture += (sender, e) =>
                {
                    if (dragging)
                    {
                        dragging = false;
                        if (DragCursor != null) Mouse.OverrideCursor = MouseOverCursor;
                        if (CancelDrag != null) CancelDrag();
                    }
                };

            }
        }

    }
}
