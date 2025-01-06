using BuzzGUI.Common;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace BuzzGUI.MachineView.SignalAnalysis
{
    internal class ControlHost : HwndHost, INotifyPropertyChanged
    {
        IntPtr hwndHost;
        private readonly SignalAnalysisVST savst;
        readonly int hostHeight, hostWidth;

        internal const int
           WsChild = 0x40000000,
           WsVisible = 0x10000000,
           LbsNotify = 0x00000001,
           HostId = 0x00000002,
           ListboxId = 0x00000001,
           WsVscroll = 0x00200000,
           WsBorder = 0x00800000,
           WM_SIZE = 0x0005;

        public event PropertyChangedEventHandler PropertyChanged;

        public ControlHost(double height, double width, SignalAnalysisVST savst)
        {
            this.savst = savst;
            hostHeight = (int)height;
            hostWidth = (int)width;
        }

        internal IntPtr GetHostHwnd()
        {
            return hwndHost;
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            hwndHost = IntPtr.Zero;
            hwndHost = CreateWindowEx(0, "static", "",
                WsChild | WsVisible,
                0, 0,
                hostHeight, hostWidth,
                hwndParent.Handle,
                (IntPtr)HostId,
                IntPtr.Zero,
                0);

            PropertyChanged.Raise(this, "WndHostReady");
            return new HandleRef(this, hwndHost);
        }

        protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            handled = false;
            return IntPtr.Zero;
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            DestroyWindow(hwnd.Handle);
        }

        //PInvoke declarations
        [DllImport("user32.dll", EntryPoint = "CreateWindowEx", CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateWindowEx(int dwExStyle,
            string lpszClassName,
            string lpszWindowName,
            int style,
            int x, int y,
            int width, int height,
            IntPtr hwndParent,
            IntPtr hMenu,
            IntPtr hInst,
            [MarshalAs(UnmanagedType.AsAny)] object pvParam);

        [DllImport("user32.dll", EntryPoint = "DestroyWindow", CharSet = CharSet.Unicode)]
        internal static extern bool DestroyWindow(IntPtr hwnd);
    }
}
