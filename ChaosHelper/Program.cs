using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        static bool highlightQualityToSell = false;
        static bool logMatchingNames = false;
        static bool reloadConfig = false;
        static bool itemStatsFromClipboard = false;
        static bool isPaused = false;

        static ChaosOverlay overlay;

        static ItemSet itemsPrevious = null;
        static ItemSet itemsCurrent = null;
        static ItemSet qualityItems = null;
        static string currentArea = string.Empty;

        static void Main(string[] args)
        {
            try
            {
                //ItemRule.RuleEntry.ShowMatch("ES>=150");
                //ItemRule.RuleEntry.ShowMatch("ES*0.5>=150");
                //ItemRule.RuleEntry.ShowMatch("LvlCold*85+DotCold*3.7+SpellPctCold>=100");

                Config.ForceSteam = args.Length > 0 && string.Equals(args[0], "steam", StringComparison.OrdinalIgnoreCase);

                Console.Title = "ChaosHelper.exe";

                MainAsync().Wait();

                Console.WriteLine();
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

#if DEBUG
            //var qualityItems = new ItemSet();
            //await GetQualityTabContents(Config.QualityTabIndex, qualityItems);
            //var keepGoing = true;
            //while (keepGoing)
            //{
            //    var set = qualityItems.MakeQualitySet();
            //    var items = set.GetCategory(Cat.Junk);
            //    var qstr = string.Join(",", items.Select(x => x.Quality));
            //    keepGoing = items.Any();
            //}

            //await CheckDumpTabs();
            //return;

            //itemsCurrent = new ItemSet();
            //await Task.Delay(500);
            //await GetTabContents(Config.RecipeTabIndex, itemsCurrent);
            //return;
#endif


            // Define the cancellation token.
            var source = new CancellationTokenSource();
            CancellationToken token = source.Token;

            var keyboardTask = Task.Run(async () =>
            {
                await Task.Delay(1500);
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
                        else if (keyInfo.Key == ConsoleKey.Q)
                        {
                            if (Config.QualityTabIndex >= 0)
                            {
                                logger.Info("Highlighting quality gems/flasks to sell");
                                highlightQualityToSell = true;
                            }
                        }
                        else if (keyInfo.Key == ConsoleKey.D)
                        {
                            if (Config.DumpTabDictionary.Any())
                            {
                                logger.Info("Checking items in dump tabs");
                                logMatchingNames = true;
                            }
                        }
                        else if (keyInfo.Key == ConsoleKey.P)
                        {
                            isPaused = !isPaused;
                            logger.Info($"Setting isPaused to {isPaused}");
                        }
                        else if (keyInfo.Key == ConsoleKey.S)
                        {
                            itemStatsFromClipboard = true;
                            logger.Info($"Check item stats from clipboard");
                        }
                        else if (keyInfo.KeyChar == '?')
                        {
                            logger.Info("\t'?' for this help");
                            logger.Info("\t'p' to toggle pausing the page checks");
                            logger.Info("\t'h' to highlight a set of items to sell");
                            if (Config.QualityTabIndex >= 0)
                                logger.Info("\t'q' to highlight quality gems/flasks to sell");
                            logger.Info("\t'j' to highlight junk items in the stash tab");
                            logger.Info("\t'f' to force a filter update");
                            logger.Info("\t'c' to switch characters");
                            logger.Info("\t't' to toggle stash test mode (to make sure the rectangle is good)");
                            logger.Info("\t'r' to reload configuration, except hotkeys");
                            logger.Info("\t'z' to list contents of currency stash tab (with prices from poe ninja)");
                            if (Config.DumpTabDictionary.Any())
                                logger.Info("\t'd' to check dump tabs for interesting items");
                            logger.Info("\t's' to check an item copied on the clipboard");
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
            await Task.Delay(500);
            await GetTabContents(Config.RecipeTabIndex, itemsCurrent);
            await Task.Delay(500);
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

                        static void MaybeRegister(HotKeyBinding x)
                        {
                            if (x != null)
                                HotKeyManager.RegisterHotKey(x.Key, x.Modifiers);
                        }
                        MaybeRegister(Config.HighlightItemsHotkey);
                        if (Config.QualityTabIndex >= 0)
                            MaybeRegister(Config.ShowQualityItemsHotkey);
                        MaybeRegister(Config.ShowJunkItemsHotkey);
                        MaybeRegister(Config.ForceUpdateHotkey);
                        MaybeRegister(Config.CharacterCheckHotkey);
                        MaybeRegister(Config.TestPatternHotkey);
                        HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(HotKeyManager_HotKeyPressed);
                    }

                    // This will check for updates as zones are entered
                    while (!token.IsCancellationRequested)
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
            else if (Config.IsShowQualityItemsHotkey(e))
                highlightQualityToSell = true;
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
            else if (Config.IsTestPatternHotkey(e))
                overlay?.SendKey(ConsoleKey.T);
        }

        static async Task CheckForUpdate()
        {
            if (isPaused && !forceFilterUpdate)
                return;

            itemsPrevious = itemsCurrent;
            itemsCurrent = new ItemSet();
            await GetTabContents(Config.RecipeTabIndex, itemsCurrent);
            await Task.Delay(500);
            await GetCurrencyTabContents();
            itemsCurrent.RefreshCounts();
            itemsCurrent.CalculateClassesToShow(Config.MaxSets, Config.IgnoreMaxSets);

            SetOverlayStatusMessage();

            if (forceFilterUpdate || !itemsCurrent.SameClassesToShow(itemsPrevious))
            {
                if (File.Exists(Config.TemplateFileName))
                {
                    var msg = itemsCurrent.GetCountsMsg();
                    logger.Info($"updating filter - {msg}");
                    File.WriteAllLines(Config.FilterFileName, NewFilterContents(itemsCurrent));

                    if (Config.FilterUpdateVolume > 0.0f && File.Exists(Config.FilterUpdateSound))
                    {
                        SharpDxSoundPlayer.PlaySoundFile(Config.FilterUpdateSound, Config.FilterUpdateVolume);
                    }
                }
                else
                    logger.Warn($"source filter not found '{Config.TemplateFileName}'");

                overlay?.SendKey(ConsoleKey.Spacebar);
                forceFilterUpdate = false;
            }
        }

        private static async Task CheckDumpTabs()
        {
            if (!Config.DumpTabDictionary.Any())
            {
                logger.Warn("cannot check dump tabs - not configured/found");
                return;
            }

            if (ItemRule.HaveDynamic)
            {
                logger.Info("reading equipped items to update dynamic rules");
                var inventoryItems = new ItemSet();
                await GetEquippedItems(inventoryItems);
                inventoryItems.UpdateDynamicRules();
            }

            var dumpTabItems = new ItemSet();
            var shouldDelay = false;
            foreach (var kvp in Config.DumpTabDictionary)
            {
                if (shouldDelay) await Task.Delay(300);
                shouldDelay = Config.StashReadMode != Config.StashReading.Playback;
                logger.Info($"retrieving tab '{kvp.Value}' ({kvp.Key})");
                var itemsInThisTab = await GetTabContents(kvp.Key, dumpTabItems, forChaosRecipe: false);
            }

            dumpTabItems.CheckMods(Config.DumpTabDictionary);
            dumpTabItems.LogMatchingNames(Config.DumpTabDictionary);
            logger.Info("done checking dump tabs");
        }

        private static async Task CheckItemStatsFromClipboard()
        {
            var itemStats = new ItemStats(Cat.Junk, null);
            await itemStats.CheckFromClipboard();
            itemStats.DumpValues();
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
            overlay?.SetCurrentItems(itemsCurrent);
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

        private static async Task<int> GetTabContents(int tabIndex, ItemSet items, bool forChaosRecipe = true)
        {
            var count = 0;

            try
            {
                if (tabIndex < 0)
                {
                    logger.Error("ERROR: stash tab index not set");
                    return 0;
                }

                var stashTabUrl = System.Uri.EscapeUriString("https://www.pathofexile.com/character-window/get-stash-items"
                    + $"?league={Config.League}&tabIndex={tabIndex}&accountName={Config.Account}");
                var json = await Config.GetJsonForUrl(stashTabUrl, tabIndex);
                foreach (var item in json.GetProperty("items").EnumerateArray())
                {
                    var frameType = item.GetIntOrDefault("frameType", 0);
                    var identified = item.GetProperty("identified").GetBoolean();
                    var ilvl = item.GetIntOrDefault("ilvl", 0);

                    // only check category for rares of ilvl 60+
                    //
                    var getCat = (!forChaosRecipe && identified && (frameType == 1 || frameType == 2))
                        || (forChaosRecipe && frameType == 2 && ilvl >= Config.MinIlvl && (Config.AllowIDedSets || !identified));

                    var category = !getCat ? Cat.Junk : item.DetermineCategory(forChaosRecipe);

                    items.Add(category, item, tabIndex);
                    ++count;
                }

                items.Sort();

            }
            catch (Exception ex)
            {
                logger.Error(ex, $"HTTP error getting tab contents: {ex.Message}");
            }

            return count;
        }

        private static async Task GetQualityTabContents(int tabIndex, ItemSet items)
        {
            try
            {
                if (tabIndex < 0)
                {
                    logger.Error("ERROR: quality tab index not set");
                    return;
                }

                var category = Cat.Junk;
#if true
                var stashTabUrl = System.Uri.EscapeUriString("https://www.pathofexile.com/character-window/get-stash-items"
                    + $"?league={Config.League}&tabIndex={tabIndex}&accountName={Config.Account}");
                var json = await Config.GetJsonForUrl(stashTabUrl, tabIndex);
#else
                await Task.Yield();
                var text = File.ReadAllText("D:/code/ChaosHelper/quality.json");
                var json = JsonSerializer.Deserialize<JsonElement>(text); ;
#endif
                foreach (var item in json.GetProperty("items").EnumerateArray())
                {
                    var frameType = item.GetIntOrDefault("frameType", 0);
                    var identified = item.GetProperty("identified").GetBoolean();
                    var ilvl = item.GetIntOrDefault("ilvl", 0);
                    var isVaal = false;

                    // Gem
                    //
                    if (frameType == 4)
                    {
                        var typeLine = item.GetStringOrDefault("typeLine");
                        isVaal = typeLine.StartsWith("Vaal", StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        // only normal or magic flasks
                        //
                        if (frameType != 0 && frameType != 1)
                            continue;

                        var w = item.GetIntOrDefault("w", 999);
                        var h = item.GetIntOrDefault("h", 999);

                        if (w == 1 && h == 1)
                        {
                            var baseType = item.GetStringOrDefault("baseType");
                            if (!baseType.EndsWith("Map", StringComparison.OrdinalIgnoreCase))
                                continue;
                        }
                        else if (w != 1 || h != 2)
                            continue;
                    }

                    var quality = GetQuality(item);
                    if (quality > 0 && quality < 20 && (!isVaal || quality <= Config.QualityVaalGemMaxQualityToUse))
                    {
                        items.Add(category, item, tabIndex, quality);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"HTTP error getting quality tab contents: {ex.Message}");
            }

            static int GetQuality(JsonElement item)
            {
                var quality = 0;
                foreach (var prop in item.GetProperty("properties").EnumerateArray())
                {
                    var propType = prop.GetIntOrDefault("type", -1);
                    if (propType != 6)
                        continue;

                    var valueString = prop.GetProperty("values").ToString();

                    var regex = new System.Text.RegularExpressions.Regex("\\+(\\d+)%");
                    var m = regex.Match(valueString);
                    if (m.Success)
                    {
                        var qualityString = m.Groups[1].Captures[0].Value;
                        if (!int.TryParse(qualityString, out quality))
                            quality = 0;
                    }
                }
                return quality;
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
                var json = await Config.GetJsonForUrl(stashTabUrl, Config.CurrencyTabIndex);
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

        private static async Task GetEquippedItems(ItemSet items)
        {
            if (Config.StashReadMode != Config.StashReading.Normal && Config.StashReadMode != Config.StashReading.Record)
                return;

            if (string.IsNullOrWhiteSpace(Config.Character))
            {
                logger.Error($"Error: No character name, cannot get inventory");
                return;
            }

            try
            {
                // for some reason, this is a form POST, not a GET
                //
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
                Config.MaybeSavePageJson(json, "inventory");

                foreach (var item in json.GetProperty("items").EnumerateArray())
                {
                    var inventoryId = item.GetProperty("inventoryId").GetString();
                    if (inventoryId == "MainInventory") continue; // only want equipped items
                    if (inventoryId == "Flask") continue; // only want equipped items

                    var category = item.DetermineCategory();

                    //if (category != Cat.Junk)
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
                var json = await Config.GetJsonForUrl(poeNinjaUrl, "poeNinja");
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

            if (!Config.FilterMarkerChecked)
            {
                Config.PutFilterLineAtTop = !File.ReadAllLines(Config.TemplateFileName)
                    .Any(line => line.StartsWith("#") && line.Contains(Config.FilterMarker, StringComparison.OrdinalIgnoreCase));
                Config.FilterMarkerChecked = true;
                if (Config.PutFilterLineAtTop)
                    logger.Warn("filter marker not found - putting section at top of file");
            }

            var insertGeneratedSection = Config.PutFilterLineAtTop;
            var numGeneratedSections = 0;

            foreach (var line in File.ReadAllLines(Config.TemplateFileName))
            {
                if (insertGeneratedSection)
                {
                    insertGeneratedSection = false;
                    ++numGeneratedSections;

                    if (!string.IsNullOrEmpty(Config.FilterInsertFile)
                        && File.Exists(Config.FilterInsertFile))
                    {
                        yield return "";
                        yield return "# Begin ChaosHelper filter_insert section";
                        yield return "";

                        foreach (var insertLine in File.ReadAllLines(Config.FilterInsertFile))
                        {
                            yield return insertLine;
                        }

                        yield return "";
                        yield return "# End ChaosHelper filter_insert section";
                    }

                    yield return "";
                    yield return "# Begin ChaosHelper generated section";
                    yield return "";

                    foreach (var c in ItemClassForFilter.Iterator())
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
                        else if (c.Category == Cat.BodyArmours)
                        {
                            yield return "Sockets < 6";
                        }

                        yield return "";

                        // Add in bows
                        if (c.Category == Cat.OneHandWeapons)
                        {
                            yield return "# Bows for chaos";
                            yield return "Show";
                            yield return "Class \"Bows\"";
                            yield return "Rarity Rare";
                            yield return "Sockets < 6";
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
                            yield return "Sockets < 6";
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

                    if (Config.CurrencyTabIndex >= 0 && Config.ShowMinimumCurrency)
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

                yield return line;

                insertGeneratedSection = line.Trim().StartsWith("#")
                    && line.Contains(Config.FilterMarker, StringComparison.OrdinalIgnoreCase);
            }

            if (numGeneratedSections == 0)
            {
                logger.Warn("WARNING: marker not found in filter template - chaos recipe items will not be highlighted");
                logger.Warn($"filter template marker is '{Config.FilterMarker}'");
            }
            else if (numGeneratedSections > 1)
            {
                logger.Warn($"WARNING: marker found {numGeneratedSections} times in filter template - this may be a problem - see README.md");
                logger.Warn($"filter template marker is '{Config.FilterMarker}'");
            }
        }

        static async Task TailClientTxt(CancellationToken token)
        {
            var savedClientFileName = Config.ClientFileName;
            using var fs = new FileStream(savedClientFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            sr.BaseStream.Seek(0, SeekOrigin.End);

            logger.Info($"tailing '{savedClientFileName}'");

            try
            {
                const string loginPattern = "login.pathofexile.com";
                var sawLoginLine = false;
                while (!token.IsCancellationRequested && string.Equals(savedClientFileName, Config.ClientFileName))
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
                        await Config.DetermineTabIndicies(forceWebCheck: true);
                        checkCharacter = false;
                        sawLoginLine = false;
                    }

                    bool wasTown = Config.IsTown(currentArea);

                    if (newArea != null)
                    {
                        bool isTown = Config.IsTown(newArea);
                        var status = isPaused ? " (paused)" : !isTown ? string.Empty : !wasTown ? " (to town)" : " (town)";
                        logger.Info($"new area - {newArea}{status}");
                        overlay?.SetArea($"{newArea}{status}", isTown);
                        qualityItems = null;
                        highlightQualityToSell = false;
                        currentArea = newArea;
                    }
                    else if (forceFilterUpdate)
                        logger.Info("forcing filter update");

                    if ((newArea != null && wasTown) || forceFilterUpdate)
                        await CheckForUpdate();
                    else if (newArea != null)
                        SetOverlayStatusMessage();

                    if (highlightSetsToSell)
                    {
                        var setToSell = itemsCurrent.GetSetToSell(Config.AllowIDedSets, Config.ChaosParanoiaLevel);
                        overlay?.SetitemSetToSell(setToSell);
                        overlay?.SendKey(ConsoleKey.H);
                        highlightSetsToSell = false;
                        SetOverlayStatusMessage();
                    }
                    else if (highlightQualityToSell)
                    {
                        if (qualityItems == null)
                        {
                            qualityItems = new ItemSet();
                            await GetQualityTabContents(Config.QualityTabIndex, qualityItems);
                        }
                        var qualitySet = qualityItems.MakeQualitySet();
                        overlay?.SetitemSetToSell(qualitySet);
                        overlay?.SendKey(ConsoleKey.Q);
                        highlightQualityToSell = false;
                    }
                    else if (logMatchingNames)
                    {
                        await CheckDumpTabs();
                        logMatchingNames = false;
                    }
                    else if (itemStatsFromClipboard)
                    {
                        await CheckItemStatsFromClipboard();
                        itemStatsFromClipboard = false;
                    }

                    await Task.Delay(1000, CancellationToken.None);
                }
            }
            catch (TaskCanceledException)
            {
                // do nothing
            }
        }
    }
}
