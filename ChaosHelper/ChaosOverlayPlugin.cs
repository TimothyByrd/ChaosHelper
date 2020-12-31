using System;
using System.Collections.Generic;
using System.Drawing;

using Overlay.NET.Common;
using Overlay.NET.Directx;

using Process.NET.Windows;

namespace ChaosHelper
{
    class ChaosOverlayPlugin : DirectXOverlayPlugin
    {
        private readonly TickEngine _tickEngine = new TickEngine();
        private int _font;
        private int _redBrush;
        private int _redOpacityBrush;
        //private Stopwatch _watch;
        private readonly int _updateRate;

        private int whiteBrush;
        private string statusMessage;
        private System.Drawing.RectangleF stashRect;
        private Dictionary<string, int> highlightDict;
        private readonly int numSquares;
        private bool showStashTest = false;
        private bool showJunkItems = false;
        private double squareWidth;
        private double squareHeight;
        private string areaName;
        private bool isTown;

        private volatile ItemSet currentCounts = null;
        private string countsMsg;

        private volatile ItemSet highlightSet = null;
        private bool showHightlightSet = false;

        public ChaosOverlayPlugin(int fps, System.Drawing.Rectangle stashRect, bool isQuad)
        {
            fps = Math.Max(1, Math.Min(60, fps));
            _updateRate = 1000 / fps;
            this.stashRect = stashRect;
            //this.isQuad = isQuad;
            numSquares = isQuad ? 24 : 12;
        }

        internal void SetCounts(ItemSet currentCounts)
        {
            this.currentCounts = currentCounts;
            countsMsg = currentCounts.GetCountsMsg();
        }

        internal void SetItemSet(ItemSet itemSet)
        {
            highlightSet = itemSet;
        }

        public override void Initialize(IWindow targetWindow)
        {
            // Set target window by calling the base method
            base.Initialize(targetWindow);

            OverlayWindow = new DirectXOverlayWindow(targetWindow.Handle, false);
            //_watch = Stopwatch.StartNew();

            _redBrush = OverlayWindow.Graphics.CreateBrush(0x7FFF0000);
            _redOpacityBrush = OverlayWindow.Graphics.CreateBrush(Color.FromArgb(80, 255, 0, 0));
            whiteBrush = OverlayWindow.Graphics.CreateBrush(0x7FFFFFFF);

            _font = OverlayWindow.Graphics.CreateFont("Arial", 20);

            if (stashRect.IsEmpty)
            {
                var x = 22.0f / 1440.0f * TargetWindow.Height;
                var y = 170.0f / 1440.0f * TargetWindow.Height;
                var size = 844.0f / 1440.0f * TargetWindow.Height;

                stashRect = new RectangleF(x, y, size, size);
            }

            squareWidth = stashRect.Width * 1.0 / numSquares;
            squareHeight = stashRect.Height * 1.0 / numSquares;

            // Set up update interval and register events for the tick engine.

            _tickEngine.PreTick += OnPreTick;
            _tickEngine.Tick += OnTick;
        }

        public void SetHighlightColors(List<int> highlightColors)
        {
            var brush0 = OverlayWindow.Graphics.CreateBrush(Color.FromArgb(80, Color.FromArgb(highlightColors[0])));
            var brush1 = OverlayWindow.Graphics.CreateBrush(Color.FromArgb(80, Color.FromArgb(highlightColors[1])));
            var brush2 = OverlayWindow.Graphics.CreateBrush(Color.FromArgb(80, Color.FromArgb(highlightColors[2])));
            var brush3 = OverlayWindow.Graphics.CreateBrush(Color.FromArgb(80, Color.FromArgb(highlightColors[3])));

            highlightDict = new Dictionary<string, int>
            {
                { "BodyArmours", brush0 },
                { "Helmets", brush1 },
                { "Gloves", brush1 },
                { "Boots", brush1 },
                { "OneHandWeapons", brush0 },
                { "TwoHandWeapons", brush0 },
                { "Belts", brush2 },
                { "Amulets", brush3 },
                { "Rings", brush3 },
            };
        }


        public void SetArea(string areaName, bool isTown)
        {
            this.areaName = areaName;
            this.isTown = isTown;
            showStashTest = false;
            showJunkItems = false;
        }

        public void SetStatus(string msg)
        {
            statusMessage = msg;
        }

        public void SendKey(ConsoleKey key)
        {
            switch (key)
            {
                case ConsoleKey.Spacebar:
                    showStashTest = false;
                    showJunkItems = false;
                    showHightlightSet = false;
                    break;
                case ConsoleKey.T:
                    showStashTest = !showStashTest;
                    showJunkItems = false;
                    showHightlightSet = false;
                    Log.Info($"showStashTest is now {showStashTest}");
                    break;
                case ConsoleKey.J:
                    showStashTest = false;
                    showJunkItems = !showJunkItems;
                    showHightlightSet = false;
                    Log.Info($"showJunkItems is now {showJunkItems}");
                    break;
                case ConsoleKey.N:
                    showStashTest = false;
                    showJunkItems = false;
                    showHightlightSet = highlightSet != null;
                    Log.Info($"showHightlightSet is now {showHightlightSet}");
                    break;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!OverlayWindow.IsVisible)
            {
                return;
            }

            OverlayWindow.Update();
            InternalRender();
        }

        private void OnPreTick(object sender, EventArgs e)
        {
            var targetWindowIsActivated = TargetWindow.IsActivated;
            if (!targetWindowIsActivated && OverlayWindow.IsVisible)
            {
                //_watch.Stop();
                ClearScreen();
                OverlayWindow.Hide();
            }
            else if (targetWindowIsActivated && !OverlayWindow.IsVisible)
            {
                OverlayWindow.Show();
            }
        }

        public override void Enable()
        {
            _tickEngine.Interval = _updateRate.Milliseconds();
            _tickEngine.IsTicking = true;
            base.Enable();
        }

        public override void Disable()
        {
            _tickEngine.IsTicking = false;
            base.Disable();
        }

        public override void Update() => _tickEngine.Pulse();

        protected void InternalRender()
        {
            OverlayWindow.Graphics.BeginScene();
            OverlayWindow.Graphics.ClearScene();

            if (showHightlightSet && highlightSet != null)
                ShowItemSet();
            else if (showJunkItems)
                ShowJunkItems();
            else if (showStashTest)
                ShowStashTest();

            ShowTextLines();

            OverlayWindow.Graphics.EndScene();
        }

        private void ShowItemSet()
        {
            if (!isTown) return;

            var y = (int)(stashRect.Bottom + squareHeight * numSquares / 16);
            var width = (int)squareWidth;
            var height = (int)squareHeight;
            RectAndArrow(width, y, width, height, highlightDict["BodyArmours"]);
            RectAndArrow(width * 3, y, width, height, highlightDict["Helmets"]);
            RectAndArrow(width * 5, y, width, height, highlightDict["Belts"]);
            RectAndArrow(width * 7, y, width, height, highlightDict["Rings"], false);

            HighlightItems(highlightSet);
        }

        private void RectAndArrow(int x, int y, int width, int height, int brush, bool drawArrow = true)
        {
            OverlayWindow.Graphics.FillRectangle(x, y, width, height, brush);
            if (drawArrow)
            {
                var arrowX1 = (int) (x + width * 1.1);
                var arrowX2 = (int) (x + width * 1.9);
                var arrowYc = y + height / 2;
                var arrowX3 = (arrowX1 + arrowX2 + arrowX2) / 3;
                OverlayWindow.Graphics.DrawLine(arrowX1, arrowYc, arrowX2, arrowYc, 2, whiteBrush);
                OverlayWindow.Graphics.DrawLine(arrowX3, y + 10, arrowX2, arrowYc, 2, whiteBrush);
                OverlayWindow.Graphics.DrawLine(arrowX3, y + height - 10, arrowX2, arrowYc, 2, whiteBrush);
            }
        }

        private void ShowTextLines()
        {
            var h = TargetWindow.Height;

            if (!string.IsNullOrWhiteSpace(countsMsg))
                OverlayWindow.Graphics.DrawText(countsMsg, _font, whiteBrush, 50, h - 30);

            if (!string.IsNullOrWhiteSpace(statusMessage))
                OverlayWindow.Graphics.DrawText(statusMessage, _font, whiteBrush, 50, h - 60);
            else
                OverlayWindow.Graphics.DrawText("Change zones to initialize", _font, _redBrush, 50, h - 60);

            if (!string.IsNullOrWhiteSpace(areaName))
                OverlayWindow.Graphics.DrawText(areaName, _font, whiteBrush, 50, h - 90);
        }

        protected void ShowStashTest()
        {
            if (!isTown) return;

            bool drawThis = true;
            var width = 1;
            var height = 1;
            var brush = _redBrush;

            for (var row = 0; row < numSquares; ++row)
            {
                drawThis = !drawThis;
                for (var col = 0; col < numSquares; ++col)
                {
                    drawThis = !drawThis;
                    if (!drawThis) continue;

                    ItemHighlightRectangle(col, row, width, height, brush);
                }
            }
        }

        private void ShowJunkItems()
        {
            if (!isTown || currentCounts == null) return;

            var junkItems = currentCounts.GetCategory("Junk");

            foreach (var item in junkItems)
            {
                ItemHighlightRectangle(item, _redOpacityBrush);
            }
        }

        private void HighlightItems(ItemSet itemSet)
        {
            foreach (var c in ItemClass.Iterator())
            {
                if (!highlightDict.ContainsKey(c.Category))
                    continue;
                var brush = highlightDict[c.Category];
                var items = itemSet.GetCategory(c.Category);
                foreach (var item in items)
                {
                    ItemHighlightRectangle(item, brush);
                }

            }
        }

        private void ItemHighlightRectangle(ItemPosition item, int brush)
        {
            ItemHighlightRectangle(item.X, item.Y, item.W, item.H, brush);
        }

        private void ItemHighlightRectangle(int col, int row, int width, int height, int brush)
        {
            var x = (int)(stashRect.X + squareWidth * col);
            var x2 = (int)(stashRect.X + squareWidth * (col + width));
            var y = (int)(stashRect.Y + squareHeight * row);
            var y2 = (int)(stashRect.Y + squareHeight * (row + height));
            OverlayWindow.Graphics.FillRectangle(x, y, x2 - x, y2 - y, brush);
        }

        public override void Dispose()
        {
            OverlayWindow.Dispose();
            base.Dispose();
        }

        private void ClearScreen()
        {
            OverlayWindow.Graphics.BeginScene();
            OverlayWindow.Graphics.ClearScene();
            OverlayWindow.Graphics.EndScene();
        }
    }
}
