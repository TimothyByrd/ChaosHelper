using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ChaosHelper
{
    class GlobalMouseHook : IDisposable
    {
        public static event EventHandler<GlobalMouseHookEventArgs> MouseLButtonUp;

        MessageWindow messageWindow = null;

        public GlobalMouseHook()
        {
            Thread messageLoop = new Thread(delegate ()
            {
                messageWindow = new MessageWindow();
                Application.Run(messageWindow);
            })
            {
                Name = "MouseHookMessageLoopThread",
                IsBackground = true
            };
            messageLoop.Start();
        }

        ~GlobalMouseHook()
        {
            messageWindow?.Close();
            messageWindow = null;
        }

        public void Dispose()
        {
            messageWindow?.Close();
            messageWindow = null;
            //GC.SuppressFinalize(this);
        }

        delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool FreeLibrary(IntPtr hModule);

        /// <summary>
        /// The SetWindowsHookEx function installs an application-defined hook procedure into a hook chain.
        /// You would install a hook procedure to monitor the system for certain types of events. These events are
        /// associated either with a specific thread or with all threads in the same desktop as the calling thread.
        /// </summary>
        /// <param name="idHook">hook type</param>
        /// <param name="lpfn">hook procedure</param>
        /// <param name="hMod">handle to application instance</param>
        /// <param name="dwThreadId">thread identifier</param>
        /// <returns>If the function succeeds, the return value is the handle to the hook procedure.</returns>
        [DllImport("USER32", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);

        /// <summary>
        /// The UnhookWindowsHookEx function removes a hook procedure installed in a hook chain by the SetWindowsHookEx function.
        /// </summary>
        /// <param name="hhk">handle to hook procedure</param>
        /// <returns>If the function succeeds, the return value is true.</returns>
        [DllImport("USER32", SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hHook);

        /// <summary>
        /// The CallNextHookEx function passes the hook information to the next hook procedure in the current hook chain.
        /// A hook procedure can call this function either before or after processing the hook information.
        /// </summary>
        /// <param name="hHook">handle to current hook</param>
        /// <param name="code">hook code passed to hook procedure</param>
        /// <param name="wParam">value passed to hook procedure</param>
        /// <param name="lParam">value passed to hook procedure</param>
        /// <returns>If the function succeeds, the return value is true.</returns>
        [DllImport("USER32", SetLastError = true)]
        static extern IntPtr CallNextHookEx(IntPtr hHook, int code, IntPtr wParam, IntPtr lParam);

        const int HC_ACTION = 0;
        const int WH_MOUSE_LL = 14;
        //const uint WM_MOUSEMOVE = 0x200;
        const uint WM_LBUTTONDOWN = 0x201;
        //const uint WM_LBUTTONUP = 0x202;
        //const uint WM_LBUTTONDBLCLK = 0x203;
        //const uint WM_RBUTTONDOWN = 0x204;
        //const uint WM_RBUTTONUP = 0x205;
        //const uint WM_RBUTTONDBLCLK = 0x206;
        //const uint WM_MBUTTONDOWN = 0x207;
        //const uint WM_MBUTTONUP = 0x208;
        //const uint WM_MBUTTONDBLCLK = 0x209;
        //const uint WM_MOUSEWHEEL = 0x20A;
        //const uint WM_MOUSEHWHEEL = 0x20E;

        private static readonly ManualResetEvent _windowReadyEvent = new ManualResetEvent(false);

        protected class MessageWindow : Form
        {
            private IntPtr _windowsHookHandle;
            private IntPtr _user32LibraryHandle;
            private HookProc _hookProc;

            public MessageWindow()
            {
                _windowReadyEvent.Set();
                SetHook();
            }

            protected override void SetVisibleCore(bool value)
            {
                // Ensure the window never becomes visible
                base.SetVisibleCore(false);
            }

            private void SetHook()
            {
                _windowsHookHandle = IntPtr.Zero;
                _user32LibraryHandle = IntPtr.Zero;
                _hookProc = LowLevelMouseProc; // we must keep alive _hookProc, because GC is not aware about SetWindowsHookEx behaviour.

                _user32LibraryHandle = LoadLibrary("User32");
                if (_user32LibraryHandle == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new Win32Exception(errorCode, "Failed to load library 'User32.dll'");
                }

                _windowsHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, _user32LibraryHandle, 0);
                if (_windowsHookHandle == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new Win32Exception(errorCode, "Failed to set mouse hook");
                }
            }

            public IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam)
            {
                // code != HC_ACTION should be evaluated PRIOR to considering the values
                // of wParam and lParam, because those values may be invalid or untrustworthy
                // whenever code < 0.
                if (nCode == HC_ACTION)  // MSDN docs specify that both LL keybd & mouse hook should return in this case.
                {
                    var wparamTyped = wParam.ToInt32();
                    if (wparamTyped == WM_LBUTTONDOWN)
                    {
                        object o = Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                        MSLLHOOKSTRUCT p = (MSLLHOOKSTRUCT)o;
                        var eventArguments = new GlobalMouseHookEventArgs(p);

                        EventHandler<GlobalMouseHookEventArgs> handler = GlobalMouseHook.MouseLButtonUp;
                        handler?.Invoke(this, eventArguments);
                    }
                }
                return CallNextHookEx(_windowsHookHandle, nCode, wParam, lParam);
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);

                if (disposing)
                {
                    // because we can unhook only in the same thread, not in garbage collector thread
                    if (_windowsHookHandle != IntPtr.Zero)
                    {
                        if (!UnhookWindowsHookEx(_windowsHookHandle))
                        {
                            int errorCode = Marshal.GetLastWin32Error();
                            throw new Win32Exception(errorCode, "Failed to remove mouse hook");
                        }
                        _windowsHookHandle = IntPtr.Zero;

                        // ReSharper disable once DelegateSubtraction
                        _hookProc -= LowLevelMouseProc;
                    }
                }

                if (_user32LibraryHandle != IntPtr.Zero)
                {
                    if (!FreeLibrary(_user32LibraryHandle)) // reduces reference to library by 1.
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        throw new Win32Exception(errorCode, "Failed to unload library 'User32.dll'");
                    }
                    _user32LibraryHandle = IntPtr.Zero;
                }
            }
        }
    }

    class GlobalMouseHookEventArgs : HandledEventArgs
    {
        public MSLLHOOKSTRUCT MouseData { get; private set; }

        public GlobalMouseHookEventArgs(MSLLHOOKSTRUCT mouseData)
        {
            MouseData = mouseData;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }

        public static implicit operator System.Drawing.Point(POINT p)
        {
            return new System.Drawing.Point(p.X, p.Y);
        }

        public static implicit operator POINT(System.Drawing.Point p)
        {
            return new POINT(p.X, p.Y);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT Point { get; set; }
        public int MouseData { get; set; }
        public int Flags { get; set; }
        public int Time { get; set; }
        public IntPtr DwExtraInfo { get; set; }
    }
}