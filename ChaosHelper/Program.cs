using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConsoleHotKey;

namespace ChaosHelper
{
    class Program
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        static bool forceFilterUpdate = false;
        static bool checkCharacter = false;
        static bool highlightSetsToSell = false;
        static bool reloadConfig = false;
        static bool isPaused = false;

        static ChaosOverlay overlay;

        static ItemSet itemsPrevious = null;
        static ItemSet itemsCurrent = null;

        static void Main()
        {
            try
            {
                Console.Title = "ChaosHelper.exe";

                MainAsync().Wait();

                Console.WriteLine("Press 'Enter' to end program");
                Console.Read();
            }
            finally
            {
                NLog.LogManager.Shutdown();
            }
        }

        static async Task MainAsync()
        {
            logger.Info("******************************************** Startup");

            if (!await Config.ReadConfigFile())
                return;

            // Define the cancellation token.
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;

            var keyboardTask = Task.Run(async () =>
            {
                await Task.Delay(2000);
                logger.Info("Press 'Escape' to exit, '?' for help");
                while (!token.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        var keyInfo = Console.ReadKey();
                        Console.Write('\r');
                        if (keyInfo.Key == ConsoleKey.Escape)
                        {
                            source.Cancel();
                            break;
                        }
                        else if (keyInfo.Key == ConsoleKey.F)
                        {
                            logger.Info("Forcing a filter update");
                            forceFilterUpdate = true;
                        }
                        else if (keyInfo.Key == ConsoleKey.C)
                        {
                            logger.Info("Rechecking character and league");
                            checkCharacter = true;
                        }
                        else if (keyInfo.Key == ConsoleKey.H)
                        {
                            logger.Info("Highlighting sets to sell");
                            highlightSetsToSell = true;
                        }
                        else if (keyInfo.Key == ConsoleKey.R)
                        {
                            logger.Info("Reloading config");
                            reloadConfig = true;
                        }
                        else if (keyInfo.Key == ConsoleKey.Z)
                        {
                            logger.Info("Getting currency prices from poe ninja");
                            await GetPricesFromPoeNinja();
                            logger.Info("Getting currency tab contents");
                            await GetCurrencyTabContents(true);
                        }
                        else if (keyInfo.Key == ConsoleKey.P)
                        {
                            isPaused = !isPaused;
                            logger.Info($"Setting isPaused to {isPaused}");
                        }
                        else if (keyInfo.KeyChar == '?')
                        {
                            logger.Info("\t'?' for this help");
                            logger.Info("\t'h' to highlight a set of items to sell");
                            logger.Info("\t'j' to highlight junk items in the stash tab");
                            logger.Info("\t'f' to force a filter update");
                            logger.Info("\t'c' to switch characters");
                            logger.Info("\t't' to toggle stash test mode (to make sure the rectangle is good)");
                            logger.Info("\t'z' to list contents of currency stash tab (with prices from poe ninja)");
                            logger.Info("\t'r' to reload configuration, except hotkeys");
                            logger.Info("\t'p' to toggle pausing the page checks");
                        }
                        else
                            overlay?.SendKey(keyInfo.Key);
                        logger.Info("Press 'Escape' to exit, '?' for help");
                    }
                    else
                        await Task.Delay(100);
                }
            });

            Task overlayTask = null;
            overlayTask = Task.Run(() =>
            {
                overlay = new ChaosOverlay();
                overlay.RunOverLay(token);
                overlay = null;
            });

            // get initial counts
            itemsCurrent = new ItemSet();
            await GetTabContents(Config.TabIndex, itemsCurrent);
            await GetCurrencyTabContents();
            itemsCurrent.RefreshCounts();
            itemsCurrent.CalculateClassesToShow(Config.MaxSets, Config.IgnoreMaxSets);
            overlay?.SetCurrentItems(itemsCurrent);

            if (overlayTask != null && !overlayTask.IsCompleted)
            {
                try
                {
                    if (Config.HaveAHotKey())
                    {
                        logger.Info("registering hotkeys");

                        void MaybeRegister(HotKeyBinding x)
                        {
                            if (x != null)
                                HotKeyManager.RegisterHotKey(x.Key, x.Modifiers);
                        }
                        MaybeRegister(Config.HighlightItemsHotkey);
                        MaybeRegister(Config.ShowJunkItemsHotkey);
                        MaybeRegister(Config.ForceUpdateHotkey);
                        MaybeRegister(Config.CharacterCheckHotkey);
                        MaybeRegister(Config.TestModeHotkey);
                        HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(HotKeyManager_HotKeyPressed);
                    }

                    // This will check for updates as zones are entered
                    await TailClientTxt(token);
                }
                finally
                {
                }
            }

            source.Cancel();
            if (overlayTask != null) await overlayTask;
            if (keyboardTask != null) await keyboardTask;
        }

        static void HotKeyManager_HotKeyPressed(object sender, ConsoleHotKey.HotKeyEventArgs e)
        {
            var activated = overlay.IsPoeWindowActivated();
            if (!activated)
                return;
            if (Config.IsHighlightItemsHotkey(e))
            {
                logger.Info("Highlighting sets to sell");
                highlightSetsToSell = true;
            }
            else if (Config.IsShowJunkItemsHotkey(e))
                overlay?.SendKey(ConsoleKey.J);
            else if (Config.IsForceUpdateHotkey(e))
            {
                logger.Info("Forcing a filter update");
                forceFilterUpdate = true;
            }
            else if (Config.IsCharacterCheckHotkey(e))
            {
                logger.Info("Rechecking character and league");
                checkCharacter = true;
            }
            else if (Config.IsTestModeHotkey(e))
                overlay?.SendKey(ConsoleKey.T);
        }

        static async Task CheckForUpdate()
        {
            if (isPaused && !forceFilterUpdate)
                return;

            itemsPrevious = itemsCurrent;
            itemsCurrent = new ItemSet();
            await GetTabContents(Config.TabIndex, itemsCurrent);
            await GetCurrencyTabContents();
            if (Config.IncludeInventoryOnForce && forceFilterUpdate)
                await GetInventoryContents(itemsCurrent);
            itemsCurrent.RefreshCounts();
            itemsCurrent.CalculateClassesToShow(Config.MaxSets, Config.IgnoreMaxSets);

            overlay?.SetCurrentItems(itemsCurrent);
            SetOverlayStatusMessage();

            if (forceFilterUpdate || !itemsCurrent.SameClassesToShow(itemsPrevious))
            {
                var msg = itemsCurrent.GetCountsMsg();
                logger.Info($"updating filter - {msg}");
                File.WriteAllLines(Config.FilterFileName, NewFilterContents(itemsCurrent));

                if (Config.FilterUpdateVolume > 0.0f && File.Exists(Config.FilterUpdateSound))
                {
                    SharpDxSoundPlayer.PlaySoundFile(Config.FilterUpdateSound, Config.FilterUpdateVolume);
                }
                overlay?.SendKey(ConsoleKey.Spacebar);
                forceFilterUpdate = false;
            }
        }

        private static void SetOverlayStatusMessage()
        {
            string msg;
            var numSets = itemsCurrent.CountPossible(false);
            if (numSets > 0)
                msg = $"you can make {numSets} un-IDed sets";
            else
            {
                numSets = Config.AllowIDedSets ? itemsCurrent.CountPossible(true) : 0;
                if (numSets > 0)
                    msg = $"you can make {numSets} IDed sets";
                else
                    msg = $"no sets, yet";
            }
            overlay?.SetStatus(msg, numSets >= Config.MaxSets);
        }

        // frameType:
        // 0 normal
        // 1 magic
        // 2 rare
        // 3 unique
        // 4 gem
        // 5 currency
        // 6 divination card
        // 7 quest item
        // 8 prophecy
        // 9 relic

        private static async Task GetTabContents(int tabIndex, ItemSet items)
        {
            try
            {
                if (tabIndex < 0)
                {
                    logger.Error("ERROR: stash tab index not set");
                    return;
                }

                var stashTabUrl = System.Uri.EscapeUriString("https://www.pathofexile.com/character-window/get-stash-items"
                    + $"?league={Config.League}&tabIndex={tabIndex}&accountName={Config.Account}");
                var json = await Config.GetJsonForUrl(stashTabUrl);
                foreach (var item in json.GetProperty("items").EnumerateArray())
                {
                    var frameType = item.GetIntOrDefault("frameType", 0);
                    var identified = item.GetProperty("identified").GetBoolean();
                    var ilvl = item.GetIntOrDefault("ilvl", 0);

                    var category = Cat.Junk;

                    if (frameType == 2 && ilvl >= Config.MinIlvl && (Config.AllowIDedSets || !identified)) // only look at rares of ilvl 60+
                    {
                        var iconUrl = item.GetProperty("icon").GetString();
                        foreach (var c in ItemClass.Iterator())
                        {
                            if (iconUrl.Contains(c.CategoryStr))
                            {
                                category = c.Category;
                                if (c.Category == Cat.OneHandWeapons)
                                {
                                    if (item.GetIntOrDefault("w", 999) > 1
                                        || item.GetIntOrDefault("h", 999) > 3)
                                        category = Cat.Junk;
                                }
                                break;
                            }
                        }
                    }

                    items.Add(category, item, tabIndex);
                }

                items.Sort();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"HTTP error getting tab contents: {ex.Message}");
            }
        }

        private static async Task GetCurrencyTabContents(bool listCurrencyToConsole = false)
        {
            try
            {
                Currency.ResetCounts();

                if (Config.CurrencyTabIndex < 0 || !Currency.CurrencyList.Any())
                    return;

                if (listCurrencyToConsole)
                    Console.WriteLine("currency;count;ratio;value");

                var currencyDict = Currency.GetWebDictionary();

                var stashTabUrl = System.Uri.EscapeUriString("https://www.pathofexile.com/character-window/get-stash-items"
                    + $"?league={Config.League}&tabIndex={Config.CurrencyTabIndex}&accountName={Config.Account}");
                var json = await Config.GetJsonForUrl(stashTabUrl);
                foreach (var item in json.GetProperty("items").EnumerateArray())
                {
                    var stackSize = item.GetIntOrDefault("stackSize", 0);
                    if (stackSize == 0)
                        continue;

                    var typeLine = item.GetProperty("typeLine").GetString();
                    if (currencyDict.TryGetValue(typeLine, out Currency currentItem))
                        currentItem.CurrentCount += stackSize;
                    else
                    {
                        var c = Currency.SetValueRatio(typeLine, 0.0);
                        currencyDict[typeLine] = c;
                    }
                }

                if (listCurrencyToConsole)
                {
                    var lineList = new List<string>();
                    foreach (var kvp in currencyDict)
                    {
                        var c = kvp.Value;
                        if (c.CurrentCount > 0)
                        {
                            var line = c.ValueRatio > 0.0
                                ? $"{c.Name}; {c.CurrentCount}; {c.ValueRatio}; {c.Value}"
                                : $"{c.Name}; {c.CurrentCount}";
                            lineList.Add(line);
                        }
                    }
                    lineList.Sort();
                    foreach (var line in lineList)
                        Console.WriteLine(line);
                    Console.WriteLine($"total value = {Currency.GetTotalValue():F2}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"HTTP error getting currency tab contents: {ex.Message}");
            }
        }

        private static async Task GetInventoryContents(ItemSet items)
        {
            if (Config.ManualMode)
                return;

            if (string.IsNullOrWhiteSpace(Config.Character))
            {
                logger.Error($"Error: No character name, cannot get inventory");
                return;
            }
            try
            {
                var formContent = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("accountName", Config.Account),
                new KeyValuePair<string, string>("realm", "pc"),
                new KeyValuePair<string, string>("character", Config.Character),
            });
                var inventoryUrl = "https://www.pathofexile.com/character-window/get-items";
                var response = await Config.HttpClient.PostAsync(inventoryUrl, formContent);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                foreach (var item in json.GetProperty("items").EnumerateArray())
                {
                    var inventoryId = item.GetProperty("inventoryId").GetString();
                    if (inventoryId != "MainInventory")
                        continue; // skip equipped items

                    var frameType = item.GetIntOrDefault("frameType", 0);
                    var identified = item.GetProperty("identified").GetBoolean();
                    var ilvl = item.GetIntOrDefault("ilvl", 0);

                    if (identified || frameType != 2 || ilvl < Config.MinIlvl)
                        continue; // only look at un-IDed rares of ilvl 60+

                    var category = Cat.Junk;

                    var iconUrl = item.GetProperty("icon").GetString();
                    foreach (var c in ItemClass.Iterator())
                    {
                        if (iconUrl.Contains(c.CategoryStr))
                        {
                            category = c.Category;
                            if (c.Category == Cat.OneHandWeapons)
                            {
                                if (item.GetIntOrDefault("w", 999) > 1
                                    || item.GetIntOrDefault("h", 999) > 3)
                                    category = Cat.Junk;
                            }
                            break;
                        }
                    }

                    if (category != Cat.Junk)
                        items.Add(category, item, -1);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"HTTP error getting inventory contents: {ex.Message}");
            }
        }

        static async Task GetPricesFromPoeNinja()
        {
            try
            {
                Currency.ResetValueRatios();
                Currency.SetValueRatio("Chaos Orb", 1.0);
                var league = Config.League;
                if (league.EndsWith("SSF"))
                    league = league.Substring(0, league.Length - 4);
                if (league.StartsWith("SSF"))
                    league = league.Substring(4);

                var poeNinjaUrl = System.Uri.EscapeUriString($"https://poe.ninja/api/data/currencyoverview?league={league}&type=Currency");
                var json = await Config.GetJsonForUrl(poeNinjaUrl);
                foreach (var item in json.GetProperty("lines").EnumerateArray())
                {
                    var currencyTypeName = item.GetProperty("currencyTypeName").GetString();
                    var chaosEquivalent = item.GetProperty("chaosEquivalent").GetDouble();
                    Currency.SetValueRatio(currencyTypeName, chaosEquivalent);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"HTTP error getting currency values from Poe.Ninja: {ex.Message}");
            }
        }

        static IEnumerable<string> NewFilterContents(ItemSet items)
        {
            var numMarkerlinesFound = 0;
            foreach (var line in File.ReadAllLines(Config.TemplateFileName))
            {
                yield return line;

                if (!line.StartsWith("#") || !line.Contains(Config.FilterMarker, StringComparison.OrdinalIgnoreCase))
                    continue;

                ++numMarkerlinesFound;

                yield return "";
                yield return "# Begin ChaosHelper generated section";
                yield return "";

                foreach (var c in ItemClass.Iterator())
                {
                    if (c.Skip || !items.ShouldShow(c.Category))
                        continue;

                    var canBeIded = Config.AllowIDedSets && (c.Category == Cat.Rings || c.Category == Cat.Amulets);
                    var limitIlvl = Config.LimitIlvl(c);

                    yield return $"# {c.Category} for chaos";
                    yield return "Show";
                    yield return $"Class \"{c.FilterClass}\"";
                    yield return "Rarity Rare";
                    yield return $"SetBorderColor {Config.FilterColor}";
                    yield return $"SetTextColor {Config.FilterColor}";
                    yield return $"SetFontSize {c.FontSize}";
                    if (!canBeIded)
                        yield return "Identified False";
                    yield return $"ItemLevel >= {Config.MinIlvl}";
                    if (limitIlvl)
                        yield return $"ItemLevel <= {Config.MaxIlvl}";
                    if (c.Category == Cat.OneHandWeapons)
                    {
                        yield return "Height = 3";
                        yield return "Width = 1";
                    }
                    yield return "";

                    // Add in bows
                    if (c.Category == Cat.OneHandWeapons)
                    {
                        yield return "# Bows for chaos";
                        yield return "Show";
                        yield return "Class \"Bows\"";
                        yield return "Rarity Rare";
                        yield return $"SetBorderColor {Config.FilterColor}";
                        yield return $"SetTextColor {Config.FilterColor}";
                        yield return $"SetFontSize {c.FontSize}";
                        if (!canBeIded)
                            yield return "Identified False";
                        yield return $"ItemLevel >= {Config.MinIlvl}";
                        if (limitIlvl)
                            yield return $"ItemLevel <= {Config.MaxIlvl}";
                        yield return "Height = 3";
                        yield return "";

                        yield return "# 1x4 2hd weapons for chaos";
                        yield return "Show";
                        yield return "Class \"Two Hand\" \"Staves\"";
                        yield return "Rarity Rare";
                        yield return $"SetBorderColor {Config.FilterColor}";
                        yield return $"SetTextColor {Config.FilterColor}";
                        yield return $"SetFontSize {c.FontSize}";
                        if (!canBeIded)
                            yield return "Identified False";
                        yield return $"ItemLevel >= {Config.MinIlvl}";
                        if (limitIlvl)
                            yield return $"ItemLevel <= {Config.MaxIlvl}";
                        yield return "Width = 1";
                        yield return "";
                    }
                }

                if (Config.CurrencyTabIndex >= 0)
                {
                    foreach (var c in Currency.CurrencyList)
                    {
                        if (c.CurrentCount < c.Desired)
                        {
                            yield return $"# Because we want at least {c.Desired} {c.Name} in reserve";
                            yield return "Show";
                            yield return "Class Currency";
                            yield return $"BaseType \"{c.Name}\"";
                            yield return $"SetFontSize {c.FontSize}";
                            yield return $"SetTextColor {c.TextColor}";
                            yield return $"SetBorderColor {c.BorderColor}";
                            yield return $"SetBackgroundColor {c.BackGroundColor}";
                            yield return "";
                        }
                    }
                }

                yield return "# End ChaosHelper generated section";
                yield return "";
            }

            if (numMarkerlinesFound == 0)
            {
                logger.Warn("WARNING: marker not found in filter template - chaos recipe items will not be highlighted");
                logger.Warn($"filter template marker is '{Config.FilterMarker}'");
            }
            else if (numMarkerlinesFound > 1)
            {
                logger.Warn($"WARNING: marker found {numMarkerlinesFound} times in filter template - this may be a problem - see README.md");
                logger.Warn($"filter template marker is '{Config.FilterMarker}'");
            }
        }

        static async Task TailClientTxt(CancellationToken token)
        {
            using (var fs = new FileStream(Config.ClientFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
            {
                sr.BaseStream.Seek(0, SeekOrigin.End);

                try
                {
                    const string loginPattern = "login.pathofexile.com";
                    var sawLoginLine = false;
                    while (!token.IsCancellationRequested)
                    {
                        string newArea = null;
                        string line = await sr.ReadLineAsync();
                        while (line != null)
                        {
                            var i = line.IndexOf(Config.AreaEnteredPattern);
                            if (i > 0)
                                newArea = line.Substring(i + Config.AreaEnteredPattern.Length).Trim().TrimEnd('.');
                            else if (line.IndexOf(loginPattern) > 0)
                                sawLoginLine = true;
                            line = await sr.ReadLineAsync();
                        }

                        if (reloadConfig)
                        {
                            await Config.ReadConfigFile();
                            overlay?.SendKey(ConsoleKey.R);
                            reloadConfig = false;
                        }

                        if (checkCharacter || sawLoginLine && newArea != null)
                        {
                            await Config.CheckAccount(forceWebCheck: true);
                            await Config.DetermineTabIndex(forceWebCheck: true);
                            checkCharacter = false;
                            sawLoginLine = false;
                        }



                        if (newArea != null)
                        {
                            bool isTown = Config.IsTown(newArea);
                            logger.Info($"new area - {newArea} - town: {isTown} - paused: {isPaused}");
                            overlay?.SetArea(newArea, isTown);
                        }
                        else if (forceFilterUpdate)
                            logger.Info("forcing filter update");

                        if (newArea != null || forceFilterUpdate)
                            await CheckForUpdate();

                        if (highlightSetsToSell)
                        {
                            var setToSell = itemsCurrent.GetSetToSell(Config.AllowIDedSets, Config.ChaosParanoiaLevel);
                            overlay?.SetitemSetToSell(setToSell);
                            overlay?.SendKey(ConsoleKey.H);
                            highlightSetsToSell = false;
                            SetOverlayStatusMessage();
                        }

                        await Task.Delay(1000, token);
                        if (token.IsCancellationRequested)
                            break;
                    }
                }
                catch (TaskCanceledException)
                {
                    // do nothing
                }
            }
        }
    }
}
