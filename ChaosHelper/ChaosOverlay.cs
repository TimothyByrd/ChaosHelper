using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Overlay.NET.Common;

using Process.NET;
using Process.NET.Memory;

namespace ChaosHelper
{
    class ChaosOverlay
    {
        private ChaosOverlayPlugin _plugin;
        private ProcessSharp _processSharp;
        private string _requiredProcessName;
        private volatile bool _processExited = false;
        private bool _haveLoggedWaitingForProcessMessage = false;

        public void RunOverLay(string requiredProcessName, System.Drawing.Rectangle stashRect, bool isQuad, List<int> highlightColors, CancellationToken cancellationToken)
        {
            _requiredProcessName = requiredProcessName;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    _plugin = null;
                    _processSharp = null;

                    var process = FindProcess();
                    if (process == null)
                    {
                        if (!_haveLoggedWaitingForProcessMessage)
                        {
                            Log.Info($"wait for PoE process - required name '{_requiredProcessName}'");
                            _haveLoggedWaitingForProcessMessage = true;
                        }
                        Thread.Sleep(2000);
                        continue;
                    }

                    Log.Info($"found process '{process.ProcessName}', pid {process.Id}");

                    int fps = 30;

                    _processExited = false;
                    _plugin = new ChaosOverlayPlugin(fps, stashRect, isQuad);
                    _processSharp = new ProcessSharp(process, MemoryType.Remote);
                    _processSharp.ProcessExited += ProcessExitedDelegate;

                    while (!_processExited && !cancellationToken.IsCancellationRequested
                        && (_processSharp.WindowFactory.MainWindow == null
                         || _processSharp.WindowFactory.MainWindow.Handle == IntPtr.Zero))
                    {
                        Log.Info("waiting for window");
                        Thread.Sleep(2000);
                    }

                    _plugin.Initialize(_processSharp.WindowFactory.MainWindow);
                    _plugin.SetHighlightColors(highlightColors);
                    _plugin.Enable();

                    while (!cancellationToken.IsCancellationRequested && !_processExited)
                    {
                        _plugin.Update();
                    }
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                // do nothing
            }
        }

        private void ProcessExitedDelegate(object sender, EventArgs e)
        {
            if (!_processExited)
            {
                Log.Info($"PoE process exiting");
                _processExited = true;
                _haveLoggedWaitingForProcessMessage = false;
            }
        }

        private System.Diagnostics.Process FindProcess()
        {
            System.Diagnostics.Process process;
            if (!string.IsNullOrWhiteSpace(_requiredProcessName))
                process = System.Diagnostics.Process.GetProcessesByName(_requiredProcessName).FirstOrDefault();
            else
            {
                process = System.Diagnostics.Process.GetProcessesByName("PathOfExile_x64").FirstOrDefault();
                if (process == null)
                    process = System.Diagnostics.Process.GetProcessesByName("PathOfExile_x64Steam").FirstOrDefault();
            }
            return process;
        }

        public void SetArea(string areaName, bool isTown)
        {
            _plugin?.SetArea(areaName, isTown);
        }

        public void SetStatus(string msg)
        {
            _plugin?.SetStatus(msg);
        }

        public void SetCurrentItems(ItemSet currentItems)
        {
            _plugin?.SetCurrentItems(currentItems);
        }

        public void SetitemSetToSell(ItemSet itemSet)
        {
            _plugin?.SetItemSetToSell(itemSet);
        }

        public void SendKey(ConsoleKey key)
        {
            _plugin?.SendKey(key);
        }

        public bool IsPoeWindowActivated()
        {
            return _processSharp?.WindowFactory.MainWindow.IsActivated ?? false;
        }
    }
}
