using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Overlay.NET.Common;

using Process.NET;
using Process.NET.Memory;
using Process.NET.Windows.Keyboard;
using Process.NET.Windows;
using System.Windows.Input;

namespace ChaosHelper
{
    class ChaosOverlay
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private ChaosOverlayPlugin _plugin;
        private ProcessSharp _processSharp;
        private string _requiredProcessName;
        private volatile bool _processExited = false;
        private bool _haveLoggedWaitingForProcessMessage = false;

        public void RunOverLay(CancellationToken cancellationToken)
        {
            _requiredProcessName = Config.RequiredProcessName;
            if (Config.ForceSteam)
                _requiredProcessName = "PathOfExileSteam";

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        _plugin?.SendKey(ConsoleKey.Spacebar);
                        _plugin = null;
                        _processSharp = null;

                        var process = FindProcess();
                        if (process == null)
                        {
                            if (!_haveLoggedWaitingForProcessMessage)
                            {
                                logger.Info($"wait for PoE process - required name '{_requiredProcessName}'");
                                _haveLoggedWaitingForProcessMessage = true;
                            }
                            Thread.Sleep(2000);
                            continue;
                        }

                        logger.Info($"found process '{process.ProcessName}', pid {process.Id}");

                        int fps = 10; // 30;

                        _processExited = false;
                        _plugin = new ChaosOverlayPlugin(fps);
                        _processSharp = new ProcessSharp(process, MemoryType.Remote);
                        _processSharp.ProcessExited += ProcessExitedDelegate;

                        if (_processExited ||
                            cancellationToken.IsCancellationRequested
                            || !HaveMainWindow(_processSharp))
                        {
                            logger.Info("no main window");
                            Thread.Sleep(2000);
                            continue;
                        }

                        Config.SetProcessModule(process.MainModule.FileName, process.Id);

                        _plugin.Initialize(_processSharp.WindowFactory.MainWindow);
                        _plugin.Enable();

                        while (!cancellationToken.IsCancellationRequested && !_processExited)
                        {
                            _plugin.Update();
                        }
                    }
                    catch (System.Threading.Tasks.TaskCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex);
                    }
                }

            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                // do nothing
            }

            static bool HaveMainWindow(ProcessSharp process)
            {
                try
                {
                    return process.WindowFactory.MainWindow != null
                        && process.WindowFactory.MainWindow.Handle != IntPtr.Zero
                        && process.WindowFactory.MainWindow.Width > 640
                        && process.WindowFactory.MainWindow.Height > 480;
                }
                catch { return false; }
            }
        }

        private void ProcessExitedDelegate(object sender, EventArgs e)
        {
            if (!_processExited)
            {
                logger.Info($"PoE process exiting");
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
                process = System.Diagnostics.Process.GetProcessesByName("PathOfExile").FirstOrDefault();
                process ??= System.Diagnostics.Process.GetProcessesByName("PathOfExileSteam").FirstOrDefault();
                process ??= System.Diagnostics.Process.GetProcessesByName("PathOfExileEGS").FirstOrDefault();
                process ??= System.Diagnostics.Process.GetProcessesByName("PathOfExile_KG").FirstOrDefault();
            }
            return process;
        }

        public void SetArea(string areaName, bool isTown)
        {
            _plugin?.SetArea(areaName, isTown);
        }

        public void SetStatus(string msg, bool atMaxSets)
        {
            _plugin?.SetStatus(msg, atMaxSets);
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
            if (key == ConsoleKey.R)
                _processExited = true;
            else
                _plugin?.SendKey(key);
        }

        public bool SendTextToPoE(IEnumerable<Process.NET.Native.Types.Keys> keys)
        {
            if (keys == null || !keys.Any())
                return false;
            if (_processSharp?.WindowFactory?.MainWindow == null)
                return false;
            var keyboard = new MessageKeyboard(_processSharp.WindowFactory.MainWindow);
            foreach (var key in keys)
            {
                if (key == Process.NET.Native.Types.Keys.None)
                    Thread.Sleep(2);
                else
                {
                    keyboard.Press(key);
                    Thread.Sleep(2);
                    keyboard.Release(key);
                    //Thread.Sleep(2);
                }
            }
            return true;
        }

        public void DrawTextMessages(IEnumerable<string> lines)
        {
            _plugin?.DrawTextMessages(lines);
        }

        public bool IsPoeWindowActivated()
        {
            return _processSharp?.WindowFactory?.MainWindow?.IsActivated ?? false;
        }
    }
}
