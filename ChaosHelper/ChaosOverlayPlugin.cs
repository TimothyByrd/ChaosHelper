//#define USEMOUSEKEYHOOKS
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
        private readonly TimeSpan _updateRate;

        private int whiteBrush;
        private int goldBrush;
        private string statusMessage;
        private bool atMaxSets = false;
        private System.Drawing.RectangleF stashRect;
        private readonly bool autoDetermineStashRect;
        private Dictionary<Cat, int> highlightBrushDict;
        private Dictionary<Cat, int> solidBrushDict;
        private readonly int numSquares;
        private double squareWidth;
        private double squareHeight;
        private string areaName;
        private bool isTown = true; // assume we start in a town

        private volatile ItemSet currentItems = null;
        private string countsMsg;

        private volatile ItemSet highlightSet = null;
        private bool showHightlightSet = false;

        private readonly bool shouldHookMouseEvents = false;
        private bool haveHookedMouse = false;
#if USEMOUSEKEYHOOKS
        Gma.System.MouseKeyHook.IKeyboardMouseEvents _mouseHook = null;
#else
        private Process.NET.Windows.Mouse.MouseHook _mouseHook = null;
#endif
        private bool showStashTest = false;
        private bool showJunkItems = false;

        public ChaosOverlayPlugin(int fps, System.Drawing.Rectangle stashRect, bool isQuad, bool shouldHookMouseEvents)
        {
            fps = Math.Max(1, Math.Min(60, fps));
            _updateRate = TimeSpan.FromMilliseconds(1000 / fps);
            this.stashRect = stashRect;
            autoDetermineStashRect = stashRect.IsEmpty;
            //this.isQuad = isQuad;
            numSquares = isQuad ? 24 : 12;
            this.shouldHookMouseEvents = shouldHookMouseEvents;
        }

        ~ChaosOverlayPlugin()
        {
            _mouseHook?.Dispose();
        }

        internal void SetCurrentItems(ItemSet currentItems)
        {
            this.currentItems = currentItems;
            countsMsg = currentItems.GetCountsMsg();
        }

        internal void SetItemSetToSell(ItemSet itemSet)
        {
            highlightSet = itemSet;
            countsMsg = currentItems.GetCountsMsg(); // refresh the counts message from the current item set.
        }

        public override void Initialize(IWindow targetWindow)
        {
            // Set target window by calling the base method
            base.Initialize(targetWindow);

            OverlayWindow = new DirectXOverlayWindow(targetWindow.Handle, false);
            //_watch = Stopwatch.StartNew();

            _redBrush = OverlayWindow.Graphics.CreateBrush(0xFF0000);
            _redOpacityBrush = OverlayWindow.Graphics.CreateBrush(Color.FromArgb(80, 255, 0, 0));
            whiteBrush = OverlayWindow.Graphics.CreateBrush(0xFFFFFF);
            goldBrush = OverlayWindow.Graphics.CreateBrush(0xFFFF00);

            _font = OverlayWindow.Graphics.CreateFont("Arial", 20);

            if (autoDetermineStashRect)
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
            var brush0h = OverlayWindow.Graphics.CreateBrush(Color.FromArgb(80, Color.FromArgb(highlightColors[0])));
            var brush1h = OverlayWindow.Graphics.CreateBrush(Color.FromArgb(80, Color.FromArgb(highlightColors[1])));
            var brush2h = OverlayWindow.Graphics.CreateBrush(Color.FromArgb(80, Color.FromArgb(highlightColors[2])));
            var brush3h = OverlayWindow.Graphics.CreateBrush(Color.FromArgb(80, Color.FromArgb(highlightColors[3])));

            highlightBrushDict = new Dictionary<Cat, int>
            {
                { Cat.BodyArmours, brush0h },
                { Cat.Helmets, brush1h },
                { Cat.Gloves, brush1h },
                { Cat.Boots, brush1h },
                { Cat.OneHandWeapons, brush0h },
                { Cat.TwoHandWeapons, brush0h },
                { Cat.Belts, brush2h },
                { Cat.Amulets, brush3h },
                { Cat.Rings, brush3h },
            };

            var brush0s = OverlayWindow.Graphics.CreateBrush(highlightColors[0]);
            var brush1s = OverlayWindow.Graphics.CreateBrush(highlightColors[1]);
            var brush2s = OverlayWindow.Graphics.CreateBrush(highlightColors[2]);
            var brush3s = OverlayWindow.Graphics.CreateBrush(highlightColors[3]);

            solidBrushDict = new Dictionary<Cat, int>
            {
                { Cat.BodyArmours, brush0s },
                { Cat.Helmets, brush1s },
                { Cat.Gloves, brush1s },
                { Cat.Boots, brush1s },
                { Cat.OneHandWeapons, brush0s },
                { Cat.TwoHandWeapons, brush0s },
                { Cat.Belts, brush2s },
                { Cat.Amulets, brush3s },
                { Cat.Rings, brush3s },
            };

        }

        public void SetArea(string areaName, bool isTown)
        {
            this.areaName = areaName;
            this.isTown = isTown;
            showStashTest = false;
            showJunkItems = false;
            showHightlightSet = false;
        }

        public void SetStatus(string msg, bool atMaxSets)
        {
            statusMessage = msg;
            this.atMaxSets = atMaxSets;
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
                {
                    var numJunk = currentItems.GetCategory(Cat.Junk).Count;
                    showStashTest = false;
                    showJunkItems = !showJunkItems && numJunk > 0;
                    showHightlightSet = false;
                    Log.Info($"showJunkItems is now {showJunkItems} ({numJunk} junk items)");
                }
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
                return;

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
            CheckMouseHooks(targetWindowIsActivated);
        }

        public override void Enable()
        {
            _tickEngine.Interval = _updateRate;
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

#if USEMOUSEKEYHOOKS
        private void CheckMouseHooks(bool targetWindowActivated)
        {
            var wantMouseHook = targetWindowActivated && shouldHookMouseEvents && (showHightlightSet || showJunkItems);

            if (wantMouseHook && !haveHookedMouse)
            {
                if (_mouseHook == null)
                    _mouseHook = Gma.System.MouseKeyHook.Hook.GlobalEvents();

                _mouseHook.MouseDown += MouseLeftButtonDown;
                haveHookedMouse = true;
                Console.WriteLine("hooked mouse");
            }
            else if (!wantMouseHook && haveHookedMouse)
            {
                _mouseHook.MouseDown -= MouseLeftButtonDown;
                haveHookedMouse = false;
                Console.WriteLine("unhooked mouse");
            }
        }

        private void MouseLeftButtonDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            Console.WriteLine($"The mouse was at the position: ({e.X}, {e.Y}), location: ({e?.Location.X}, {e?.Location.Y}) when left clicked.");
        }
#else
        private void CheckMouseHooks(bool targetWindowActivated)
        {
            var wantMouseHook = targetWindowActivated && shouldHookMouseEvents && (showHightlightSet || showJunkItems);

            if (wantMouseHook && !haveHookedMouse)
            {
                if (_mouseHook == null)
                {
                    _mouseHook = new Process.NET.Windows.Mouse.MouseHook("Chaos");
                    _mouseHook.LeftButtonDown += MouseLeftButtonDown;
                }

                _mouseHook.Enable();
                haveHookedMouse = true;
                Console.WriteLine("hooked mouse");
            }
            else if (!wantMouseHook && haveHookedMouse)
            {
                _mouseHook.Disable();
                haveHookedMouse = false;
                Console.WriteLine("unhooked mouse");
            }
        }

        private void MouseLeftButtonDown(object sender, Process.NET.Windows.Mouse.MouseHookEventArgs e)
        {
            Console.WriteLine($"The mouse was at the position: {e.Position} when left clicked.");
        }
#endif

        private void ShowItemSet()
        {
            if (!isTown) return;

            var y = (int)(stashRect.Bottom + squareHeight * numSquares / 16);
            var width = (int)squareWidth;
            var height = (int)squareHeight;
            RectAndArrow(width, y, width, height, highlightBrushDict[Cat.BodyArmours], solidBrushDict[Cat.BodyArmours]);
            RectAndArrow(width * 3, y, width, height, highlightBrushDict[Cat.Helmets], solidBrushDict[Cat.Helmets]);
            RectAndArrow(width * 5, y, width, height, highlightBrushDict[Cat.Belts], solidBrushDict[Cat.Belts]);
            RectAndArrow(width * 7, y, width, height, highlightBrushDict[Cat.Rings], solidBrushDict[Cat.Rings], false);

            HighlightItems(highlightSet);
        }

        private void RectAndArrow(int x, int y, int width, int height, int brushF, int brushS, bool drawArrow = true)
        {
            OverlayWindow.Graphics.FillRectangle(x, y, width, height, brushF);
            OverlayWindow.Graphics.DrawRectangle(x, y, width, height, 3, brushS);
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
                OverlayWindow.Graphics.DrawText(statusMessage, _font, atMaxSets ? goldBrush : whiteBrush, 50, h - 60);
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

                    ItemHighlightRectangle(col, row, width, height, brush, brush);
                }
            }
        }

        private void ShowJunkItems()
        {
            if (!isTown || currentItems == null) return;

            var junkItems = currentItems.GetCategory(Cat.Junk);

            foreach (var item in junkItems)
                ItemHighlightRectangle(item, _redOpacityBrush, _redBrush);
        }

        private void HighlightItems(ItemSet itemSet)
        {
            foreach (var c in ItemClass.Iterator())
            {
                if (!highlightBrushDict.ContainsKey(c.Category))
                    continue;
                var brushH = highlightBrushDict[c.Category];
                var brushS = solidBrushDict[c.Category];
                var items = itemSet.GetCategory(c.Category);
                foreach (var item in items)
                    ItemHighlightRectangle(item, brushH, brushS);
            }
        }

        private void ItemHighlightRectangle(ItemPosition item, int brushH, int brushS)
        {
            ItemHighlightRectangle(item.X, item.Y, item.W, item.H, brushH, brushS);
        }

        private void ItemHighlightRectangle(int col, int row, int width, int height, int brushH, int brushS)
        {
            var x = (int)(stashRect.X + squareWidth * col);
            var x2 = (int)(stashRect.X + squareWidth * (col + width));
            var y = (int)(stashRect.Y + squareHeight * row);
            var y2 = (int)(stashRect.Y + squareHeight * (row + height));
            var stroke = 3;
            OverlayWindow.Graphics.FillRectangle(x, y, x2 - x, y2 - y, brushH);
            OverlayWindow.Graphics.DrawRectangle(x, y, x2 - x, y2 - y, stroke, brushS);
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
