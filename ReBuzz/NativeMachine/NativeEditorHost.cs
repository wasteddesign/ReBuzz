using ReBuzz.Core;
using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ReBuzz.NativeMachine
{
    internal class NativeEditorHost : HwndHost
    {
        internal const int
            WsChild = 0x40000000,
            WsVisible = 0x10000000,
            LbsNotify = 0x00000001,
            HostId = 0x00000002,
            ListboxId = 0x00000001,
            WsVscroll = 0x00200000,
            WsBorder = 0x00800000,
            WS_EX_TOPMOST = 0x00000008;

        private readonly int _hostHeight;
        private readonly int _hostWidth;
        private readonly MachineCore machine;
        private readonly UIMessage uiMessage;
        private readonly IntPtr wndhandle;
        private IntPtr _hwndHost;

        public NativeEditorHost(double height, double width, MachineCore machine, UIMessage uiMessage, IntPtr wndHandle)
        {
            _hostHeight = (int)height;
            _hostWidth = (int)width;

            this.machine = machine;
            this.uiMessage = uiMessage;
            this.wndhandle = wndHandle;
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            _hwndHost = CreateWindowEx(0, "static", "",
                          WsChild | WsVisible,
                          0, 0,
                          _hostWidth, _hostHeight,
                          hwndParent.Handle,
                          HostId,
                          IntPtr.Zero,
                          0);
            var editorHost = uiMessage.UICreatePatternEditor(machine, _hwndHost);
            return new HandleRef(this, _hwndHost);
        }

        protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            handled = false;
            return hwnd;
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
