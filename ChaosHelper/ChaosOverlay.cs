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

        public void RunOverLay(string processName, System.Drawing.Rectangle stashRect, bool isQuad, List<int> highlightColors, CancellationToken cancellationToken)
        {
            var process = System.Diagnostics.Process.GetProcessesByName(processName).FirstOrDefault();
            if (process == null)
            {
                Log.Warn($"ERROR: PathOfExile process '{processName}' not found.");
                return;
            }

            int fps = 30;

            _plugin = new ChaosOverlayPlugin(fps, stashRect, isQuad);
            _processSharp = new ProcessSharp(process, MemoryType.Remote);

            _plugin.Initialize(_processSharp.WindowFactory.MainWindow);
            _plugin.SetHighlightColors(highlightColors);
            _plugin.Enable();

            while (!cancellationToken.IsCancellationRequested)
            {
                _plugin.Update();
            }
        }

        public void SetArea(string areaName, bool isTown)
        {
            _plugin.SetArea(areaName, isTown);
        }

        public void SetStatus(string msg)
        {
            _plugin.SetStatus(msg);
        }

        public void SetCounts(ItemSet currentCounts)
        {
            _plugin.SetCounts(currentCounts);
        }

        public void SetItemSet(ItemSet itemSet)
        {
            _plugin.SetItemSet(itemSet);
        }

        public void SendKey(ConsoleKey key)
        {
            _plugin.SendKey(key);
        }

        public bool IsPoeWindowActivated()
        {
            return _processSharp?.WindowFactory.MainWindow.IsActivated ?? false;
        }
    }
}
