using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using Overlay.NET.Common;
using Overlay.NET.Directx;

using Process.NET.Windows;

namespace ChaosHelper
{
    class ChaosOverlayPlugin : DirectXOverlayPlugin
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly TickEngine _tickEngine = new();
        private int _font;
        private int _redBrush;
        private int _redOpacityBrush;
        //private Stopwatch _watch;
        private readonly TimeSpan _updateRate;

        private int _whiteBrush;
        private int _goldBrush;
        private string _statusMessage;
        private bool _atMaxSets = false;
        private SharpDX.Mathematics.Interop.RawRectangleF _stashRect;
        private readonly bool _autoDetermineStashRect;
        private Dictionary<Cat, int> _highlightBrushDict;
        private Dictionary<Cat, int> _solidBrushDict;
        private readonly int _numSquares;
        private double _squareWidth;
        private double _squareHeight;
        private double _qualitySquareWidth;
        private double _qualitySquareHeight;
        private int _targetWindowHeight;
        private string _areaName;
        private bool _inATown = true; // assume we start in a town

        private volatile ItemSet _currentItems = null;
        private string _countsMsg;

        private volatile ItemSet _highlightSet = null;
        private bool _showHightlightSet = false;
        private bool _showQualitySet = false;

        private readonly bool _shouldHookMouseEvents = false;
        private bool _haveHookedMouse = false;
        private GlobalMouseHook _mouseHook = null;

        private bool _showStashTest = false;
        private bool _showJunkItems = false;

        private readonly List<Point> _clickList = [];
        private readonly List<ItemRectStruct> _itemsToDraw = [];
        private List<TextStruct> _textToDraw = [];

        public ChaosOverlayPlugin(int fps)
        {
            fps = Math.Max(1, Math.Min(60, fps));
            _updateRate = TimeSpan.FromMilliseconds(1000 / fps);
            _stashRect = ToRaw(Config.StashPageXYWH);
            _autoDetermineStashRect = Config.StashPageXYWH.IsEmpty;
            _numSquares = Config.IsQuadTab ? 24 : 12;
            _shouldHookMouseEvents = Config.ShouldHookMouseEvents;
        }

        ~ChaosOverlayPlugin()
        {
            _mouseHook?.Dispose();
            _mouseHook = null;
        }

        internal void SetCurrentItems(ItemSet currentItems)
        {
            _currentItems = currentItems;
            _countsMsg = _currentItems?.GetCountsMsg() ?? "null";
        }

        internal void SetItemSetToSell(ItemSet itemSet)
        {
            MaybeUpdateStashRect();
            _highlightSet = itemSet;
            _countsMsg = _currentItems?.GetCountsMsg() ?? "null"; // refresh the counts message from the current item set.
        }

        public override void Initialize(IWindow targetWindow)
        {
            // Set target window by calling the base method
            base.Initialize(targetWindow);

            OverlayWindow = new DirectXOverlayWindow(targetWindow.Handle, false);
            //_watch = Stopwatch.StartNew();

            _redBrush = OverlayWindow.Graphics.CreateBrush(0xFF0000);
            _redOpacityBrush = OverlayWindow.Graphics.CreateBrush(Color.FromArgb(80, 255, 0, 0));
            _whiteBrush = OverlayWindow.Graphics.CreateBrush(0xFFFFFF);
            _goldBrush = OverlayWindow.Graphics.CreateBrush(0xFFB300);

            _font = OverlayWindow.Graphics.CreateFont("Arial", 20);

            DetermineStashRect();

            // Set up update interval and register events for the tick engine.

            _tickEngine.PreTick += OnPreTick;
            _tickEngine.Tick += OnTick;

            SetHighlightColors();
        }

        private void DetermineStashRect()
        {
            _targetWindowHeight = TargetWindow.Height;
            if (_autoDetermineStashRect)
            {
                var x = 22.0f / 1440.0f * _targetWindowHeight;
                var y = 170.0f / 1440.0f * _targetWindowHeight;
                var size = 844.0f / 1440.0f * _targetWindowHeight;

                y += Config.StashPageVerticalOffset;

                _stashRect = new SharpDX.Mathematics.Interop.RawRectangleF(x, y, x + size, y + size);
            }

            _squareWidth = (_stashRect.Right - _stashRect.Left) / _numSquares;
            _squareHeight = (_stashRect.Bottom - _stashRect.Top) / _numSquares;

            var numQualitySquares = Config.QualityIsQuadTab ? 24 : 12;
            _qualitySquareWidth = (_stashRect.Right - _stashRect.Left) / numQualitySquares;
            _qualitySquareHeight = (_stashRect.Bottom - _stashRect.Top) / numQualitySquares;

            logger.Info($"stashrect targetWindowHeight {_targetWindowHeight}");
            logger.Info($"stashrect left {_stashRect.Left} top {_stashRect.Top}  right {_stashRect.Right} bottom {_stashRect.Bottom}");
            logger.Info($"stashrect squareWidth {_squareWidth}  squareHeight {_squareHeight}  height {_stashRect.Bottom - _stashRect.Top}");
        }

        private void MaybeUpdateStashRect()
        {
            if (_autoDetermineStashRect && _targetWindowHeight != TargetWindow.Height)
                DetermineStashRect();
        }

        private void SetHighlightColors()
        {
            var highlightColors = Config.HighlightColors;
            var brush0h = OverlayWindow.Graphics.CreateBrush(Color.FromArgb(80, Color.FromArgb(highlightColors[0])));
            var brush1h = OverlayWindow.Graphics.CreateBrush(Color.FromArgb(80, Color.FromArgb(highlightColors[1])));
            var brush2h = OverlayWindow.Graphics.CreateBrush(Color.FromArgb(80, Color.FromArgb(highlightColors[2])));
            var brush3h = OverlayWindow.Graphics.CreateBrush(Color.FromArgb(80, Color.FromArgb(highlightColors[3])));

            _highlightBrushDict = new Dictionary<Cat, int>
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

            _solidBrushDict = new Dictionary<Cat, int>
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
            _areaName = areaName;
            _inATown = isTown;
            _showStashTest = false;
            _showJunkItems = false;
            _showHightlightSet = false;
            _showQualitySet = false;
            _textToDraw = [];
        }

        public void SetStatus(string msg, bool atMaxSets)
        {
            _statusMessage = msg;
            _atMaxSets = atMaxSets;
        }

        public void SendKey(ConsoleKey key)
        {
            switch (key)
            {
            case ConsoleKey.Spacebar:
                _showStashTest = false;
                _showJunkItems = false;
                _showHightlightSet = false;
                _showQualitySet = false;
                _textToDraw = [];
                break;
            case ConsoleKey.T:
                MaybeUpdateStashRect();
                _showStashTest = !_showStashTest;
                _showJunkItems = false;
                _showHightlightSet = false;
                _showQualitySet = false;
                _textToDraw = [];
                logger.Info($"showStashTest is now {_showStashTest}");
                break;
            case ConsoleKey.J:
            {
                MaybeUpdateStashRect();
                var junk = _currentItems?.GetCategory(Cat.Junk);
                var numJunk = junk?.Count ?? 0;
                _showStashTest = false;
                _showJunkItems = !_showJunkItems && numJunk > 0;
                _showHightlightSet = false;
                _showQualitySet = false;
                _textToDraw = [];
                if (_showJunkItems)
                    FillJunkItemsToDraw(junk);
                logger.Info($"showJunkItems is now {_showJunkItems} ({numJunk} junk items)");
            }
                break;
            case ConsoleKey.H:
                MaybeUpdateStashRect();
                _showStashTest = false;
                _showJunkItems = false;
                _showHightlightSet = _highlightSet != null;
                _showQualitySet = false;
                _textToDraw = [];
                if (_showHightlightSet)
                    FillItemsToDraw(_highlightSet);
                logger.Info($"showHightlightSet is now {_showHightlightSet}");
                break;
            case ConsoleKey.Q:
                MaybeUpdateStashRect();
                _showStashTest = false;
                _showJunkItems = false;
                _showHightlightSet = false;
                _textToDraw = [];
                var qualitySet = _highlightSet?.GetCategory(Cat.Junk);

                _showQualitySet = qualitySet?.Count > 0;
                if (_showQualitySet)
                    FillQualityItemsToDraw(qualitySet);
                logger.Info($"showQualitySet is now {_showQualitySet}");
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
            var targetWindowIsActivated = TargetWindow?.IsActivated ?? false;
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
            CheckItemsClicked();
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

            if (_showHightlightSet)
                ShowItemSet();
            else if (_showQualitySet)
                ShowQualityItems();
            else if (_showJunkItems)
                ShowJunkItems();
            else if (_showStashTest)
                ShowStashTest();

            ShowTextLines();

            OverlayWindow.Graphics.EndScene();
        }

        private void CheckMouseHooks(bool targetWindowActivated)
        {
            var wantMouseHook = targetWindowActivated && _shouldHookMouseEvents && (_showHightlightSet || _showQualitySet || _showJunkItems);

            if (wantMouseHook && !_haveHookedMouse)
            {
                _mouseHook ??= new GlobalMouseHook();

                GlobalMouseHook.MouseLButtonDown += MouseLeftButtonDown;
                _haveHookedMouse = true;
                //Console.WriteLine("hooked mouse");
            }
            else if (!wantMouseHook && _haveHookedMouse)
            {
                GlobalMouseHook.MouseLButtonDown -= MouseLeftButtonDown;
                _haveHookedMouse = false;
                //Console.WriteLine("unhooked mouse");
            }
        }

        private void MouseLeftButtonDown(object sender, GlobalMouseHookEventArgs e)
        {
            lock (_clickList)
            {
                if (_showHightlightSet || _showQualitySet || _showJunkItems)
                    _clickList.Add(e.MouseData.Point);
            }
        }

        private void CheckItemsClicked()
        {
            lock (_clickList)
            {
                _itemsToDraw.RemoveAll(x => _clickList.Any(y => x.Contains(y)));
                _clickList.Clear();
            }
        }

        private void ShowItemSet()
        {
            if (!_inATown || _itemsToDraw.Count == 0)
            {
                _showHightlightSet = false;
                return;
            }

            var y = (int)(_stashRect.Bottom + _squareHeight * _numSquares / 16);
            var width = (int)_squareWidth;
            var height = (int)_squareHeight;

            bool drawArrow = false;
            int offset = 1;
            void DrawForCategory(Cat cat)
            {
                bool drawRect = _itemsToDraw.Any(x => x.BrushH == _highlightBrushDict[cat]);
                if (drawRect)
                {
                    ArrowAndRect(width * offset, y, width, height, _highlightBrushDict[cat], _solidBrushDict[cat], drawArrow);
                    offset += 2;
                    drawArrow = true;
                }
            }

            DrawForCategory(Cat.BodyArmours);
            DrawForCategory(Cat.Helmets);
            DrawForCategory(Cat.Belts);
            DrawForCategory(Cat.Rings);

            foreach (var item in _itemsToDraw)
                HightlightItem(item);
        }

        private void ShowQualityItems()
        {
            if (!_inATown || _itemsToDraw.Count == 0)
            {
                _showQualitySet = false;
                return;
            }

            foreach (var item in _itemsToDraw)
                HightlightItem(item);
        }

        private void ArrowAndRect(int x, int y, int width, int height, int brushF, int brushS, bool drawArrow = true)
        {
            OverlayWindow.Graphics.FillRectangle(x, y, width, height, brushF);
            OverlayWindow.Graphics.DrawRectangle(x, y, width, height, 3, brushS);
            if (drawArrow)
            {
                x -= width * 2; // move arrow to left of rect
                var arrowX1 = (int)(x + width * 1.1);
                var arrowX2 = (int)(x + width * 1.9);
                var arrowX3 = (arrowX1 + arrowX2 + arrowX2) / 3;
                var arrowYc = y + height / 2;
                OverlayWindow.Graphics.DrawLine(arrowX1, arrowYc, arrowX2, arrowYc, 2, _whiteBrush);
                OverlayWindow.Graphics.DrawLine(arrowX3, y + 10, arrowX2, arrowYc, 2, _whiteBrush);
                OverlayWindow.Graphics.DrawLine(arrowX3, y + height - 10, arrowX2, arrowYc, 2, _whiteBrush);
            }
        }

        private void ShowTextLines()
        {
            var h = _targetWindowHeight;
            if (h < 0)
                return;

            const int bottomRowOffset = 60;
            const int spaceBetweenRows = 30;
            const int xForText = 50;

            int YForRow(int zeroBase4dRowFromBottom)
            {
                return h - bottomRowOffset - zeroBase4dRowFromBottom * spaceBetweenRows;
            }

            if (!string.IsNullOrWhiteSpace(_countsMsg))
                OverlayWindow.Graphics.DrawText(_countsMsg, _font, _whiteBrush, xForText, YForRow(0));

            if (!string.IsNullOrWhiteSpace(_statusMessage))
                OverlayWindow.Graphics.DrawText(_statusMessage, _font, _atMaxSets ? _goldBrush : _whiteBrush, xForText, YForRow(1));
            else
                OverlayWindow.Graphics.DrawText("Change zones or force an update to initialize", _font, _goldBrush, xForText, YForRow(1));

            if (!string.IsNullOrWhiteSpace(_areaName))
                OverlayWindow.Graphics.DrawText(_areaName, _font, _whiteBrush, xForText, YForRow(2));

            foreach (var t in _textToDraw)
                OverlayWindow.Graphics.DrawText(t.Text, _font, t.BrushS, t.X, t.Y);
        }

        protected void ShowStashTest()
        {
            if (!_inATown) return;

            bool drawThis = true;
            var width = 1;
            var height = 1;
            var brush = _redBrush;

            for (var row = 0; row < _numSquares; ++row)
            {
                drawThis = !drawThis;
                for (var col = 0; col < _numSquares; ++col)
                {
                    drawThis = !drawThis;
                    if (!drawThis) continue;

                    ItemHighlightRectangle(col, row, width, height, brush, brush);
                }
            }
        }

        private void ShowJunkItems()
        {
            if (!_inATown || _currentItems == null) return;

            if (_itemsToDraw.Count == 0)
                _showJunkItems = false;

            foreach (var item in _itemsToDraw)
                HightlightItem(item);
        }

        private void FillJunkItemsToDraw(List<ItemPosition> items)
        {
            _itemsToDraw.Clear();
            var brushH = _redOpacityBrush;
            var brushS = _redBrush;
            foreach (var item in items)
                _itemsToDraw.Add(GetHighlightRectangle(item, brushH, brushS));
        }

        private void FillQualityItemsToDraw(List<ItemPosition> items)
        {
            _itemsToDraw.Clear();
            var brushH = _highlightBrushDict[Cat.BodyArmours];
            var brushS = _solidBrushDict[Cat.BodyArmours];
            foreach (var item in items)
                _itemsToDraw.Add(GetQualityRectangle(item, brushH, brushS));
        }

        private void FillItemsToDraw(ItemSet itemSet)
        {
            _itemsToDraw.Clear();
            foreach (var c in ItemClassForFilter.Iterator())
            {
                if (!_highlightBrushDict.ContainsKey(c.Category))
                    continue;
                var brushH = _highlightBrushDict[c.Category];
                var brushS = _solidBrushDict[c.Category];
                var items = itemSet.GetCategory(c.Category);
                foreach (var item in items)
                    _itemsToDraw.Add(GetHighlightRectangle(item, brushH, brushS));
            }
        }

        private void ItemHighlightRectangle(int col, int row, int width, int height, int brushH, int brushS)
        {
            var x = (int)(_stashRect.Left + _squareWidth * col);
            var x2 = (int)(_stashRect.Left + _squareWidth * (col + width));
            var y = (int)(_stashRect.Top + _squareHeight * row);
            var y2 = (int)(_stashRect.Top + _squareHeight * (row + height));
            var stroke = 3;
            OverlayWindow.Graphics.FillRectangle(x, y, x2 - x, y2 - y, brushH);
            OverlayWindow.Graphics.DrawRectangle(x, y, x2 - x, y2 - y, stroke, brushS);
        }

        private ItemRectStruct GetHighlightRectangle(ItemPosition item, int brushH, int brushS)
        {
            return GetHighlightRectangle(item.X, item.Y, item.W, item.H, brushH, brushS);
        }

        private ItemRectStruct GetHighlightRectangle(int col, int row, int width, int height, int brushH, int brushS)
        {
            return new ItemRectStruct
            {
                Rect = new SharpDX.Mathematics.Interop.RawRectangleF
                {
                    Left = (float)(_stashRect.Left + _squareWidth * col) + 1.0f,
                    Right = (float)(_stashRect.Left + _squareWidth * (col + width)) - 1.0f,
                    Top = (float)(_stashRect.Top + _squareHeight * row) + 1.0f,
                    Bottom = (float)(_stashRect.Top + _squareHeight * (row + height)) - 1.0f,
                },
                Stroke = 3,
                BrushH = brushH,
                BrushS = brushS,
            };
        }

        private ItemRectStruct GetQualityRectangle(ItemPosition item, int brushH, int brushS)
        {
            return GetQualityRectangle(item.X, item.Y, item.W, item.H, brushH, brushS);
        }

        private ItemRectStruct GetQualityRectangle(int col, int row, int width, int height, int brushH, int brushS)
        {
            return new ItemRectStruct
            {
                Rect = new SharpDX.Mathematics.Interop.RawRectangleF
                {
                    Left = (float)(_stashRect.Left + _qualitySquareWidth * col) + 1.0f,
                    Right = (float)(_stashRect.Left + _qualitySquareWidth * (col + width)) - 1.0f,
                    Top = (float)(_stashRect.Top + _qualitySquareHeight * row) + 1.0f,
                    Bottom = (float)(_stashRect.Top + _qualitySquareHeight * (row + height)) - 1.0f,
                },
                Stroke = 3,
                BrushH = brushH,
                BrushS = brushS,
            };
        }

        private void HightlightItem(ItemRectStruct s)
        {
            OverlayWindow.Graphics.FillRectangle(s.Rect, s.BrushH);
            OverlayWindow.Graphics.DrawRectangle(s.Rect, s.Stroke, s.BrushS);
        }

        public override void Dispose()
        {
            OverlayWindow.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        private void ClearScreen()
        {
            OverlayWindow.Graphics.BeginScene();
            OverlayWindow.Graphics.ClearScene();
            OverlayWindow.Graphics.EndScene();
        }

        private static SharpDX.Mathematics.Interop.RawRectangleF ToRaw(System.Drawing.Rectangle r)
        {
            return new SharpDX.Mathematics.Interop.RawRectangleF
            {
                Left = r.Left,
                Top = r.Top,
                Right = r.Right,
                Bottom = r.Bottom,
            };
        }

        internal void DrawTextMessages(IEnumerable<string> lines)
        {
            _textToDraw = [];
            var h = _targetWindowHeight;
            if (h < 0)
                return;

            const int yIncrement = 30;

            var yStart = (h - lines.Count() * yIncrement) / 2;

            var x = (int) (_stashRect.Right + _squareWidth * _numSquares / 4);

            foreach ( var line in lines ) 
            {
                if (!string.IsNullOrWhiteSpace(line))
                    _textToDraw.Add(new TextStruct(line, _whiteBrush, x, yStart));
                yStart += yIncrement;
            }
        }

        private class TextStruct(string text, int brushS, int x, int y)
        {
            public string Text { get; set; } = text;
            public int BrushS { get; set; } = brushS;
            public int X { get; set; } = x;
            public int Y { get; set; } = y;
        }

        private class ItemRectStruct
        {
            public SharpDX.Mathematics.Interop.RawRectangleF Rect { get; set; }
            public int BrushH { get; set; }
            public int BrushS { get; set; }
            public int Stroke{ get; set; }

            public bool Contains(Point p)
            {
                return Rect.Left < p.X && p.X < Rect.Right && Rect.Top < p.Y && p.Y < Rect.Bottom;
            }
        }
    }
}
