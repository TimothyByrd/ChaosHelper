using ConsoleHotKey;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ChaosHelper
{
    class GlobalKeyboardHook : IDisposable
    {
        public static event EventHandler<GlobalKeyboardHookEventArgs> KeyDown;

        MessageWindow messageWindow = null;

        public GlobalKeyboardHook()
        {
            var messageLoop = new Thread(delegate ()
            {
                messageWindow = new MessageWindow();
                Application.Run(messageWindow);
            })
            {
                Name = "KeyboardHookMessageLoopThread",
                IsBackground = true
            };
            messageLoop.Start();
        }

        ~GlobalKeyboardHook()
        {
            messageWindow?.Close();
            messageWindow = null;
        }

        public void Dispose()
        {
            messageWindow?.Close();
            messageWindow = null;
            GC.SuppressFinalize(this);
        }

        static Keys[] _keysToMonitor = [];
        
        public static void SetKeysToMonitor(IEnumerable<Keys> keysToMonitor)
        {
            _keysToMonitor = [.. keysToMonitor];
        }

        delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("USER32", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);

        [DllImport("USER32", SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hHook);

        [DllImport("USER32", SetLastError = true)]
        static extern IntPtr CallNextHookEx(IntPtr hHook, int code, IntPtr wParam, IntPtr lParam);

        [DllImport("USER32", SetLastError = true)]
        static extern short GetAsyncKeyState(int vKey);

        const int HC_ACTION = 0;
        const int WH_KEYBOARD_LL = 13;
        const int WM_KEYDOWN = 0x0100;
        const int WM_SYSKEYDOWN = 0x0104;

        private static readonly ManualResetEvent _windowReadyEvent = new(false);

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
                base.SetVisibleCore(false);
            }

            private void SetHook()
            {
                _windowsHookHandle = IntPtr.Zero;
                _user32LibraryHandle = IntPtr.Zero;
                _hookProc = LowLevelKeyboardProc;

                _user32LibraryHandle = LoadLibrary("User32");
                if (_user32LibraryHandle == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new Win32Exception(errorCode, "Failed to load library 'User32.dll'");
                }

                _windowsHookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, _user32LibraryHandle, 0);
                if (_windowsHookHandle == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new Win32Exception(errorCode, "Failed to set keyboard hook");
                }
            }

            public IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
            {
                if (nCode == HC_ACTION)
                {
                    var wparamTyped = wParam.ToInt32();
                    if (wparamTyped == WM_KEYDOWN || wparamTyped == WM_SYSKEYDOWN)
                    {
                        int vkCode = Marshal.ReadInt32(lParam);
                        Keys key = (Keys)vkCode;
                        if (!_keysToMonitor.Contains(key))
                            return CallNextHookEx(_windowsHookHandle, nCode, wParam, lParam);
                        var modifiers = GetKeyboardModifiers();
                        var eventArguments = new GlobalKeyboardHookEventArgs(key, modifiers);
                        EventHandler<GlobalKeyboardHookEventArgs> handler = GlobalKeyboardHook.KeyDown;
                        handler?.Invoke(this, eventArguments);
                        if (eventArguments.Handled)
                            return (IntPtr)1;
                    }
                }
                return CallNextHookEx(_windowsHookHandle, nCode, wParam, lParam);
            }

            public static KeyModifiers GetKeyboardModifiers()
            {
                KeyModifiers modifiers = 0;
                if ((GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0)
                    modifiers |= KeyModifiers.Shift;
                if ((GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0)
                    modifiers |= KeyModifiers.Control;
                if ((GetAsyncKeyState((int)Keys.Menu) & 0x8000) != 0
                    || (GetAsyncKeyState((int)Keys.LMenu) & 0x8000) != 0
                    || (GetAsyncKeyState((int)Keys.RMenu) & 0x8000) != 0)
                    modifiers |= KeyModifiers.Alt;
                return modifiers;
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);

                if (disposing)
                {
                    if (_windowsHookHandle != IntPtr.Zero)
                    {
                        if (!UnhookWindowsHookEx(_windowsHookHandle))
                        {
                            int errorCode = Marshal.GetLastWin32Error();
                            throw new Win32Exception(errorCode, "Failed to remove keyboard hook");
                        }
                        _windowsHookHandle = IntPtr.Zero;

                        _hookProc -= LowLevelKeyboardProc;
                    }
                }

                if (_user32LibraryHandle != IntPtr.Zero)
                {
                    if (!FreeLibrary(_user32LibraryHandle))
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        throw new Win32Exception(errorCode, "Failed to unload library 'User32.dll'");
                    }
                    _user32LibraryHandle = IntPtr.Zero;
                }
            }
        }
    }

    class GlobalKeyboardHookEventArgs(Keys keyData, KeyModifiers modifiers) : HandledEventArgs
    {
        public Keys KeyData { get; private set; } = keyData;
        public KeyModifiers Modifiers { get; private set; } = modifiers;
    }
}
