using Process.NET;
using Process.NET.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ChaosHelper
{
    class ChaosOverlay
    {
        private const string ProcessNameStandAlone = "PathOfExile";
        private const string ProcessNameOnSteam = "PathOfExileSteam";
        private const string ProcessNameOnEpic = "PathOfExileEGS";
        private const string ProcessNameKorean = "PathOfExile_KG";

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private ChaosOverlayPlugin _plugin;
        private ProcessSharp _processSharp;
        private string _requiredProcessName;
        private volatile bool _processExited = false;
        private volatile bool _reloadingConfig = false;
        private bool _haveLoggedWaitingForProcessMessage = false;
        private bool _haveLoggedWaitingForMainWindowMessage = false;
        private int _lastPidFound = 0;

        public void RunOverLay(bool shouOverlay, CancellationToken cancellationToken)
        {
            _requiredProcessName = Config.RequiredProcessName;
            if (Config.ForceSteam)
                _requiredProcessName = ProcessNameOnSteam;

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

                        if (process.Id != _lastPidFound)
                        {
                            logger.Info($"found process '{process.ProcessName}', pid {process.Id}");
                            _lastPidFound = process.Id;
                        }

                        _processExited = false;

                        if (shouOverlay)
                        {
                            int fps = 10; // 30;
                            _plugin = new ChaosOverlayPlugin(fps);
                        }
                        
                        _processSharp = new ProcessSharp(process, MemoryType.Remote);
                        _processSharp.ProcessExited += ProcessExitedDelegate;

                        if (_processExited ||
                            cancellationToken.IsCancellationRequested
                            || !HaveMainWindow(_processSharp))
                        {
                            if (!_haveLoggedWaitingForMainWindowMessage)
                            {
                                logger.Info("waiting for main window");
                                _haveLoggedWaitingForMainWindowMessage = true;
                            }
                            Thread.Sleep(3000);
                            continue;
                        }

                        Config.SetProcessModule(process.MainModule.FileName, process.Id);

                        _plugin?.Initialize(_processSharp.WindowFactory.MainWindow);
                        _plugin?.Enable();

                        while (!cancellationToken.IsCancellationRequested && !_processExited)
                        {
                            _plugin?.Update();
                            int sleepDuration = (_plugin != null && _plugin.OverlayWindow.IsVisible) ? 30 : 2000;
                            Thread.Sleep(sleepDuration);
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

                    if (_reloadingConfig)
                    {
                        _reloadingConfig = false;
                    }
                    else if (_processExited && Config.ExitWhenPoeExits)
                    {
                        logger.Info($"Exiting program since {nameof(Config.ExitWhenPoeExits)} is true");
                        Config.SetProcessModule(string.Empty, 0);
                        break;
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
                _haveLoggedWaitingForMainWindowMessage = false;
            }
        }

        private System.Diagnostics.Process FindProcess()
        {
            System.Diagnostics.Process process;
            if (!string.IsNullOrWhiteSpace(_requiredProcessName))
                process = System.Diagnostics.Process.GetProcessesByName(_requiredProcessName).FirstOrDefault();
            else
            {
                process = System.Diagnostics.Process.GetProcessesByName(ProcessNameStandAlone).FirstOrDefault();
                process ??= System.Diagnostics.Process.GetProcessesByName(ProcessNameOnSteam).FirstOrDefault();
                process ??= System.Diagnostics.Process.GetProcessesByName(ProcessNameOnEpic).FirstOrDefault();
                process ??= System.Diagnostics.Process.GetProcessesByName(ProcessNameKorean).FirstOrDefault();
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

        public void SetItemSetToSell(ItemSet itemSet)
        {
            _plugin?.SetItemSetToSell(itemSet);
        }

        public void SendKey(ConsoleKey key)
        {
            if (key == ConsoleKey.R)
            {
                _reloadingConfig = true;
                _processExited = true;
            }
            else
                _plugin?.SendKey(key);
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
