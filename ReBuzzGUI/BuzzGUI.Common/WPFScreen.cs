using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;

namespace BuzzGUI.Common
{
    // TODO: rewrite using PInvoke to avoid having to reference windows forms
    public class WPFScreen
    {
        public static IEnumerable<WPFScreen> AllScreens
        {
            get
            {
                foreach (Screen screen in System.Windows.Forms.Screen.AllScreens)
                {
                    yield return new WPFScreen(screen);
                }
            }
        }

        public static WPFScreen GetScreenFrom(Window window)
        {
            WindowInteropHelper windowInteropHelper = new WindowInteropHelper(window);
            Screen screen = System.Windows.Forms.Screen.FromHandle(windowInteropHelper.Handle);
            WPFScreen wpfScreen = new WPFScreen(screen);
            return wpfScreen;
        }

        public static WPFScreen GetScreenFrom(Visual visual)
        {
            Screen screen = System.Windows.Forms.Screen.FromHandle(((HwndSource)PresentationSource.FromVisual(visual)).Handle);
            WPFScreen wpfScreen = new WPFScreen(screen);
            return wpfScreen;
        }

        public static WPFScreen GetScreenFrom(System.Windows.Point point)
        {
            int x = (int)Math.Round(point.X);
            int y = (int)Math.Round(point.Y);
            // are x,y device-independent-pixels ??
            System.Drawing.Point drawingPoint = new System.Drawing.Point(x, y);
            Screen screen = System.Windows.Forms.Screen.FromPoint(drawingPoint);
            WPFScreen wpfScreen = new WPFScreen(screen);
            return wpfScreen;
        }

        public static WPFScreen Primary
        {
            get
            {
                return new WPFScreen(System.Windows.Forms.Screen.PrimaryScreen);
            }
        }

        private readonly Screen screen;

        internal WPFScreen(System.Windows.Forms.Screen screen)
        {
            this.screen = screen;
        }

        public Rect DeviceBounds
        {
            get
            {
                return this.GetRect(this.screen.Bounds);
            }
        }

        public Rect WorkingArea
        {
            get
            {
                return this.GetRect(this.screen.WorkingArea);
            }
        }

        private Rect GetRect(Rectangle value)
        {
            // should x, y, width, hieght be device-independent-pixels ??
            return new Rect
            {
                X = value.X,
                Y = value.Y,
                Width = value.Width,
                Height = value.Height
            };
        }

        public bool IsPrimary
        {
            get
            {
                return this.screen.Primary;
            }
        }

        public string DeviceName
        {
            get
            {
                return this.screen.DeviceName;
            }
        }
    }
}
