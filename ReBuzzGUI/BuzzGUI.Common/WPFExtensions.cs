using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace BuzzGUI.Common
{
    public static class WPFExtensions
    {
        static int dpi = -1;
        static readonly object _dpiLock = new object();

        public static int DPI
        {
            get
            {
                if (dpi < 0)
                {
                    lock (_dpiLock)
                    {
                        IntPtr dc = Win32.GetDC(IntPtr.Zero);
                        if (dc == IntPtr.Zero) { dpi = 96; return dpi; }
                        dpi = Win32.GetDeviceCaps(dc, Win32.DeviceCap.LOGPIXELSY);
                        Win32.ReleaseDC(IntPtr.Zero, dc);
                    }
                }

                return dpi;
            }
        }

        public static float PixelsPerDip { get { return ((float)DPI) / 96; } }

        public static double RoundDipForDisplayMode(double value)
        {
            return RoundDipForDisplayMode(value, MidpointRounding.ToEven);
        }

        static double RoundDipForDisplayMode(double value, MidpointRounding midpointRounding)
        {
            return Math.Round(value * PixelsPerDip, midpointRounding) / PixelsPerDip;
        }

        public static T GetAncestor<T>(this DependencyObject e) where T : DependencyObject
        {
            do
            {
                e = VisualTreeHelper.GetParent(e);
            } while (e != null && !(e is T));

            return (T)e;
        }

        public static Rect TranslateRect(this UIElement e, UIElement x, Rect r)
        {
            return new Rect(e.TranslatePoint(r.TopLeft, x), e.TranslatePoint(r.BottomRight, x));
        }

        public static Rect GetBounds(this UIElement e, UIElement x)
        {
            Point tl = e.TranslatePoint(new Point(0, 0), x);
            Point bl = e.TranslatePoint(new Point(e.RenderSize.Width, e.RenderSize.Height), x);
            return new Rect(tl, bl);
        }

        public static bool IsPointInside(this UIElement e, Point p)
        {
            return p.X >= 0 && p.Y >= 0 && p.X < e.RenderSize.Width && p.Y < e.RenderSize.Height;
        }

        public static Point GetCenter(this Rect r)
        {
            return new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
        }

        public static Vector ElementMul(this Vector a, Vector b) { return new Vector(a.X * b.X, a.Y * b.Y); }
        public static Vector ElementDiv(this Vector a, Vector b) { return new Vector(a.X / b.X, a.Y / b.Y); }

        public static Point Clamp(this Point p, double min, double max)
        {
            return new Point(Math.Min(Math.Max(p.X, min), max), Math.Min(Math.Max(p.Y, min), max));
        }

        public static Color Blend(this Color c, Color d, double a)
        {
            return Color.FromArgb(
                (byte)(a * c.A + (1 - a) * d.A),
                (byte)(a * c.R + (1 - a) * d.R),
                (byte)(a * c.G + (1 - a) * d.G),
                (byte)(a * c.B + (1 - a) * d.B));
        }

        public static int GetPerceivedBrightness(this Color c)
        {
            return (int)Math.Sqrt(c.R * c.R * .299 + c.G * c.G * .587 + c.B * c.B * .114);
        }

        public static Color EnsureContrast(this Color fg, Color bg)
        {
            var fb = fg.GetPerceivedBrightness();
            var bb = bg.GetPerceivedBrightness();

            if (Math.Abs(fb - bb) < 64)
            {
                if (bb >= 128)
                    return Colors.Black;
                else
                    return Colors.White;
            }
            else
            {
                return fg;
            }
        }

        public static bool IsMouseOnEdgeOrOutsideScreen(this Visual visual)
        {
            var p = Win32Mouse.GetScreenPosition();
            var r = WPFScreen.GetScreenFrom(visual).DeviceBounds;
            r.X++;
            r.Y++;
            r.Width -= 3;
            r.Height -= 3;
            return !r.Contains(p);
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool BringWindowToTop(IntPtr hWnd);

        public static bool BringToTop(this Window wnd)
        {
            return BringWindowToTop(new WindowInteropHelper(wnd).Handle);
        }

        public static Size GetTotalNonclientAreaSize(this Window wnd)
        {
            switch (wnd.WindowStyle)
            {
                case WindowStyle.ThreeDBorderWindow:
                    throw new Exception("not supported");

                case WindowStyle.SingleBorderWindow:
                    if (wnd.ResizeMode == ResizeMode.CanResize || wnd.ResizeMode == ResizeMode.CanResizeWithGrip)
                        return new Size(2 * SystemParameters.ResizeFrameVerticalBorderWidth, System.Windows.Forms.SystemInformation.CaptionHeight + 2 * SystemParameters.ResizeFrameHorizontalBorderHeight);
                    else
                        return new Size(2 * SystemParameters.FixedFrameVerticalBorderWidth, System.Windows.Forms.SystemInformation.CaptionHeight + 2 * SystemParameters.FixedFrameHorizontalBorderHeight);

                case WindowStyle.ToolWindow:
                    if (wnd.ResizeMode == ResizeMode.CanResize || wnd.ResizeMode == ResizeMode.CanResizeWithGrip)
                        return new Size(2 * SystemParameters.ResizeFrameVerticalBorderWidth, System.Windows.Forms.SystemInformation.ToolWindowCaptionHeight + 2 * SystemParameters.ResizeFrameHorizontalBorderHeight);
                    else
                        return new Size(2 * SystemParameters.FixedFrameVerticalBorderWidth, System.Windows.Forms.SystemInformation.ToolWindowCaptionHeight + 2 * SystemParameters.FixedFrameHorizontalBorderHeight);

                default:
                    if (wnd.ResizeMode == ResizeMode.CanResize || wnd.ResizeMode == ResizeMode.CanResizeWithGrip)
                        throw new Exception("not supported");
                    else
                        return new Size(0, 0);
            }
        }

    }
}
