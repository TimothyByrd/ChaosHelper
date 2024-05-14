using ConsoleHotKey;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ChaosHelper
{
    partial class Program
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
        static bool haveAddedHotkeyEventHandler = false;

        static ChaosOverlay overlay;

        static ItemSet itemsPrevious = null;
        static ItemSet itemsCurrent = null;
        static ItemSet qualityItems = null;
        static string currentArea = string.Empty;

        private class Settings
        {
            public bool ForceSingleUpdate { get; set; } = false;
            public bool RunOverlay { get; set; } = true;
            public bool StartPaused { get; set; } = false;
            public bool FindIlvl100 { get; set; } = false;
            public string FileToCheck { get; set; }
        }

        static void Main(string[] args)
        {
            try
            {
                //ItemRule.RuleEntry.ShowMatch("ES>=150");
                //ItemRule.RuleEntry.ShowMatch("ES*0.5>=150");
                //ItemRule.RuleEntry.ShowMatch("LvlCold*85+DotCold*3.7+SpellPctCold>=100");

                var settings = new Settings();

                bool nextArgIsPoeDir = false;
                bool nextArgIsFileToCheck = false;
                foreach (var arg in args)
                {
                    if (string.Equals(arg, "-steam", StringComparison.Ordinal))
                        Config.ForceSteam = true;
                    else if (string.Equals(arg, "-once", StringComparison.Ordinal) || string.Equals(arg, "-1", StringComparison.Ordinal))
                        settings.ForceSingleUpdate = true;
                    else if (string.Equals(arg, "-nooverlay", StringComparison.Ordinal) || string.Equals(arg, "-no", StringComparison.Ordinal))
                        settings.RunOverlay = false;
                    else if (string.Equals(arg, "-pause", StringComparison.Ordinal) || string.Equals(arg, "-p", StringComparison.Ordinal))
                        settings.StartPaused = true;
                    else if (string.Equals(arg, "-autoexit", StringComparison.Ordinal) || string.Equals(arg, "-x", StringComparison.Ordinal))
                        Config.ForceExitWhenPoeExits = true;
                    else if (string.Equals(arg, "-poe", StringComparison.Ordinal))
                        nextArgIsPoeDir = true;
                    else if (string.Equals(arg, "-check", StringComparison.Ordinal))
                        nextArgIsFileToCheck = true;
                    else if (string.Equals(arg, "-100", StringComparison.Ordinal))
                        settings.FindIlvl100 = true;
                    else if (nextArgIsPoeDir)
                    {
                        nextArgIsPoeDir = false;
                        if (Directory.Exists(arg))
                            Config.SetPoeDir(arg);
                    }
                    else if (nextArgIsFileToCheck)
                    {
                        nextArgIsFileToCheck = false;
                        if (File.Exists(arg))
                        {
                            settings.FileToCheck = arg;
                            Config.OfflineMode = true;
                        }
                        else
                            logger.Warn($"file not found to check for chaos sets '{arg}'");
                    }
                    else if (Directory.Exists(arg))
                        Config.ConfigDir = arg;
                }

                Console.Title = "ChaosHelper.exe";

                MainAsync(settings).Wait();

                //Console.WriteLine();
                //Console.WriteLine("Press 'Enter' to end program");
                //Console.Read();
            }
            finally
            {
                NLog.LogManager.Shutdown();
            }
        }

        static async Task MainAsync(Settings settings)
        {
            logger.Info("******************************************** Startup");

            if (!await Config.ReadConfigFile())
                return;

            if (settings.FindIlvl100)
            {
                await FindIlvl100Items(settings);
                return;
            }

            if (!string.IsNullOrWhiteSpace(settings.FileToCheck))
            {
                CheckSingleFileForSets(settings);
                return;
            }

            if (settings.ForceSingleUpdate)
            {
                logger.Info("Forcing a single filter update");
                forceFilterUpdate = true;
                await CheckForUpdate();
                logger.Info("Exiting after single filter update");
                return;
            }

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

            //await FindAndSaveTab("map");
            //return;
#endif

            // Define the cancellation token.
            var source = new CancellationTokenSource();
            CancellationToken cancellationToken = source.Token;
            isPaused = Config.StartPaused || settings.StartPaused;

            var keyboardTask = Task.Run(async () =>
            {
                await Task.Delay(1500);
                logger.Info("Press 'Escape' to exit, '?' for help");
                while (!cancellationToken.IsCancellationRequested)
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
                            if (Config.DumpTabDictionary.Count != 0)
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
                        else if (keyInfo.Key == ConsoleKey.A)
                        {
                            Config.FilterAutoReload = !Config.FilterAutoReload;
                            logger.Info($"FilterAutoReload is now {Config.FilterAutoReload}");
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
                            logger.Info("\t'f' to force a filter update");
                            logger.Info("\t'p' to toggle automatic filter reload on update");
                            logger.Info("\t'h' to highlight a set of items to sell");
                            if (Config.QualityTabIndex >= 0)
                                logger.Info("\t'q' to highlight quality gems/flasks to sell");
                            logger.Info("\t'j' to highlight junk items in the stash tab");
                            logger.Info("\t'c' to switch/recheck characters");
                            logger.Info("\t'r' to reload configuration");
                            logger.Info("\t't' to toggle stash test mode (to make sure the rectangle is good)");
                            logger.Info("\t'z' to list contents of currency stash tab (with prices from poe ninja)");
                            if (Config.DumpTabDictionary.Count != 0)
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
            if (settings.RunOverlay)
            {
                overlayTask = Task.Run(() =>
                {
                    overlay = new ChaosOverlay();
                    overlay.RunOverLay(cancellationToken);
                    overlay = null;
                });
            }

            // get initial counts
            itemsCurrent = new ItemSet();
            await RateLimit.DelayForRateLimits(500).ConfigureAwait(false);
            await GetTabContents(Config.RecipeTabIndex, itemsCurrent);
            await RateLimit.DelayForRateLimits(500).ConfigureAwait(false);
            await GetCurrencyTabContents();
            itemsCurrent.RefreshCounts();
            itemsCurrent.CalculateClassesToShow();
            overlay?.SetCurrentItems(itemsCurrent);

            bool CheckOverlayTask()
            {
                return !settings.RunOverlay || (overlayTask != null && !overlayTask.IsCompleted);
            }

            if (CheckOverlayTask())
            {
                try
                {
                    CheckHotkeyRegistration();

                    // This will check for updates as zones are entered
                    while (!cancellationToken.IsCancellationRequested && CheckOverlayTask())
                        await TailClientTxt(cancellationToken);
                }
                finally
                {
                    HotKeyManager.UnregisterAllHotKeys();
                }
            }

            source.Cancel();
            if (overlayTask != null) await overlayTask;
            if (keyboardTask != null) await keyboardTask;
            HotKeyManager.UnregisterAllHotKeys();
        }

        private static async Task FindIlvl100Items(Settings settings)
        {
            logger.Info("Checking for ilvl 100 items");

            if (!string.IsNullOrWhiteSpace(settings.FileToCheck))
            {
                try
                {
                    logger.Info($"Checking file '{settings.FileToCheck}'");
                    var json = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(settings.FileToCheck), RawJsonConfiguration.SerializerOptions);
                    LogIlvl100Items(json, Path.GetFileName(settings.FileToCheck));
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Reading '{settings.FileToCheck}'");
                }
                logger.Info("Exiting after checking json file");
                return;
            }

            const int delaySeconds = 20;

            try
            {
                var charList = await Config.GetJsonForUrl("https://www.pathofexile.com/character-window/get-characters", "charlist");

                var numChars = charList.EnumerateArray().Count();
                var guess = 1 + ((numChars * delaySeconds) / 60);
                logger.Info($"There are {numChars} characters - this may take {guess} minutes to run ({delaySeconds} seconds between checks)");

                foreach (var character in charList.EnumerateArray())
                {
                    var characterName = character.GetProperty("name").GetString();
                    var realm = character.GetProperty("realm").GetString();
                    var league = character.GetProperty("league").GetString();

                    //await Task.Delay(delaySeconds * 1000);
                    await RateLimit.DelayForRateLimits(1000).ConfigureAwait(false);

                    var charJson = await GetJsonForCharacter(Config.Account, realm, characterName);

                    LogIlvl100Items(charJson, $"Character {characterName}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"HTTP error getting character or character list: {ex.Message}");
            }

            await CheckTabsForLeague("Hardcore");
            await CheckTabsForLeague("Standard");

            logger.Info("Exiting after checking for items");

            static async Task CheckTabsForLeague(string league)
            {
                try
                {
                    // Set this after a partial run to pick up where it left off.
                    //
                    int startIndex = 0;

                    logger.Info($"Getting list of tabs for {league}");
                    JsonElement tabList = await Config.GetTabList(league);

                    var numTabs = tabList.GetIntOrDefault("numTabs", -1);
                    var guess = 1 + ((numTabs * delaySeconds) / 60);
                    logger.Info($"There are {numTabs} tabs in {league} - this may take {guess} minutes to run ({delaySeconds} seconds between tab checks)");

                    foreach (var tab in tabList.GetProperty("tabs").EnumerateArray())
                    {
                        var tabType = tab.GetProperty("type").GetString();
                        if (string.Equals(tabType, "MapStash", StringComparison.OrdinalIgnoreCase)) continue;

                        var i = tab.GetIntOrDefault("i", -1);
                        if (i < startIndex) continue;

                        var name = tab.GetProperty("n").GetString();
                        var nameWithIndex = $"Tab ({i}) {name}";

                        //await Task.Delay(delaySeconds * 1000);
                        await RateLimit.DelayForRateLimits(1000).ConfigureAwait(false);
                        JsonElement json = await GetJsonByTabIndex(i, league);
                        LogIlvl100Items(json, nameWithIndex);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"HTTP error getting tab or tab list: {ex.Message}");
                }
            }
        }

        private static void LogIlvl100Items(JsonElement json, string location)
        {
            logger.Info($"Checking '{location}'");

            if (json.ValueKind == JsonValueKind.Undefined)
            {
                logger.Error("ERROR: empty stash returned");
                return;
            }

            if (!json.TryGetProperty("items", out var itemsProp))
            {
                logger.Error("ERROR: no 'items' property");
                return;
            }

            foreach (var item in itemsProp.EnumerateArray())
            {
                var ilvl = item.GetIntOrDefault("ilvl", 0);

                if (ilvl < 100) continue;

                var x = item.GetProperty("x").GetInt32();
                var y = item.GetProperty("y").GetInt32();
                var name = item.GetStringOrDefault("name");
                if (string.IsNullOrWhiteSpace(name))
                    name = item.GetStringOrDefault("baseType");
                logger.Info($"{location} - x: {x}, y: {y}, name: {name}");
            }
        }

        private static void CheckSingleFileForSets(Settings settings)
        {
            logger.Info($"Checking file for chaos sets '{settings.FileToCheck}'");
            try
            {
                ItemSet.DetailedLogging = true;
                logger.Info($"config: AllowIDedSets {Config.AllowIDedSets}, ChaosParanoiaLevel: {Config.ChaosParanoiaLevel}");
                var json = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(settings.FileToCheck), RawJsonConfiguration.SerializerOptions);
                var items = new ItemSet();
                var count = AddItemsToItemSetFromJson(json, items);
                logger.Info($"set has {count} items");
                items.RefreshCounts();

                ItemSet setToSell = null;
                do
                {
                    setToSell = items.GetSetToSell(Config.AllowIDedSets, Config.ChaosParanoiaLevel);

                } while (setToSell != null && setToSell.HasAnyItems());
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Reading save tab data for '{settings.FileToCheck}'");
            }

            logger.Info("Exiting after checking json file");
        }

        private static void CheckHotkeyRegistration()
        {
            HotKeyManager.UnregisterAllHotKeys();
            if (Config.HaveAHotKey())
            {
                int numRegistered = 0;
                foreach (HotkeyEntry hotkey in Config.Hotkeys)
                {
                    if (!hotkey.Enabled)
                        continue;
                    if (hotkey.CommandIs(Constants.ShowQualityItems) && Config.QualityTabIndex == 0)
                        continue;
                    HotKeyManager.RegisterHotKey(hotkey.Binding.Key, hotkey.Binding.Modifiers);
                    ++numRegistered;
                }
                logger.Info($"registered {numRegistered}/{Config.Hotkeys.Count} hotkeys");

                if (!haveAddedHotkeyEventHandler)
                    HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(HotKeyManager_HotKeyPressed);
                haveAddedHotkeyEventHandler = true;
            }
        }

        static void HotKeyManager_HotKeyPressed(object sender, HotKeyEventArgs e)
        {
            if (!PoeIsActiveWindow())
                return;

            if (Config.IsClosePortsHotkey(e))
                Config.CloseTcpPorts();

            var hotkey = Config.GetHotKey(e);
            if (hotkey == null || !hotkey.Enabled) return;

            if (!string.IsNullOrEmpty(hotkey.Command))
            {
                ProcessHotkeyCommand(hotkey);
                return;
            }

            if (!string.IsNullOrEmpty(hotkey.Text))
            {
                try
                {
                    var (keyEntries, didSubstitution) = KeyboardUtils.StringToKeys(hotkey.Text);
                    KeyboardUtils.SendKeys(keyEntries);
                    var sent = KeyboardUtils.ToString(keyEntries);
                    logger.Debug($"sent {sent}");
                }
                catch
                {
                    logger.Warn($"disabling hotkey after error sending keys: {hotkey.Text}");
                    hotkey.Enabled = false;
                    hotkey.Keys = null;
                }

                return;
            }
        }

        private static void ProcessHotkeyCommand(HotkeyEntry hotkey)
        {
            switch (hotkey.Command)
            {
                case Constants.ClosePorts:
                    Config.CloseTcpPorts();
                    break;
                case Constants.HighlightItems:
                    logger.Info("Highlighting sets to sell");
                    highlightSetsToSell = true;
                    break;
                case Constants.ShowQualityItems:
                    highlightQualityToSell = true;
                    break;
                case Constants.ShowJunkItems:
                    overlay?.SendKey(ConsoleKey.J);
                    break;
                case Constants.ForceUpdate:
                    logger.Info("Forcing a filter update");
                    forceFilterUpdate = true;
                    break;
                case Constants.CharacterCheck:
                    logger.Info("Rechecking character and league");
                    checkCharacter = true;
                    break;
                case Constants.TestPattern:
                    overlay?.SendKey(ConsoleKey.T);
                    break;
                default:
                    logger.Warn($"Unknown command {hotkey.Command}");
                    break;
            }
        }

        static async Task CheckForUpdate()
        {
            Config.GetCloseTcpPortsArgument();

            if (isPaused && !forceFilterUpdate)
                return;

            if (forceFilterUpdate)
                qualityItems = null;

            itemsPrevious = itemsCurrent;
            itemsCurrent = new ItemSet();
            await GetTabContents(Config.RecipeTabIndex, itemsCurrent);
            //await Task.Delay(500);
            await RateLimit.DelayForRateLimits(500).ConfigureAwait(false);
            await GetCurrencyTabContents();
            itemsCurrent.RefreshCounts();
            itemsCurrent.CalculateClassesToShow();

            SetOverlayStatusMessage();

            if (forceFilterUpdate || !itemsCurrent.SameClassesToShow(itemsPrevious))
            {
                logger.Info($"updating filter - {itemsCurrent.GetCountsMsg()}");
                File.WriteAllLines(Config.FilterFileName, NewFilterContents(itemsCurrent));

                if (Config.FilterUpdateVolume > 0.0f && File.Exists(Config.FilterUpdateSound))
                {
                    SharpDxSoundPlayer.PlaySoundFile(Config.FilterUpdateSound, Config.FilterUpdateVolume);
                }
                if (Config.FilterAutoReload && PoeIsActiveWindow())
                {
                    var keyString = $"{{Enter}}/itemfilter {Config.FilterFileBaseName}{{Enter}}";
                    var (keyEntries, _) = KeyboardUtils.StringToKeys(keyString);
                    KeyboardUtils.SendKeys(keyEntries);
                    var sent = KeyboardUtils.ToString(keyEntries);
                    logger.Debug($"sent {sent}");
                }

                overlay?.SendKey(ConsoleKey.Spacebar);
                forceFilterUpdate = false;
            }
            else if (overlay == null)
            {
                logger.Info($"item counts - {itemsCurrent.GetCountsMsg()}");
            }
        }

        private static async Task CheckDumpTabs()
        {
            await Config.DetermineTabIndicies(forceWebCheck: true);

            if (Config.DumpTabDictionary.Count == 0)
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
            var delayMS = 300;
            foreach (var kvp in Config.DumpTabDictionary)
            {
                if (shouldDelay)
                {
                    //await Task.Delay(delayMS);
                    //delayMS += 30;
                    await RateLimit.DelayForRateLimits(delayMS).ConfigureAwait(false);
                }
                shouldDelay = Config.StashReadMode != Config.StashReading.Playback;
                logger.Info($"retrieving tab '{kvp.Value}' ({kvp.Key})");
                var itemsInThisTab = await GetTabContents(kvp.Key, dumpTabItems, forChaosRecipe: false);
            }

            var interestingItems = dumpTabItems.CheckMods(Config.DumpTabDictionary);
            var matches = dumpTabItems.LogMatchingNames(Config.DumpTabDictionary);
            overlay?.DrawTextMessages(interestingItems.Concat(matches));
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
            {
                var s = numSets == 1 ? "" : "s";
                msg = $"you can make {numSets} un-IDed set{s}";
            }
            else
            {
                numSets = Config.AllowIDedSets ? itemsCurrent.CountPossible(true) : 0;
                if (numSets > 0)
                {
                    var s = numSets == 1 ? "" : "s";
                    msg = $"you can make {numSets} IDed set{s}";
                }
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
            try
            {
                if (tabIndex < 0)
                {
                    logger.Error("ERROR: stash tab index not set");
                    return 0;
                }

                JsonElement json = await GetJsonByTabIndex(tabIndex);
                return AddItemsToItemSetFromJson(json, items, tabIndex, forChaosRecipe);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"HTTP error getting tab contents: {ex.Message}");
            }

            return 0;
        }

        public static int AddItemsToItemSetFromJson(JsonElement json, ItemSet items, int tabIndex = 0, bool forChaosRecipe = true)
        {
            if (json.ValueKind == JsonValueKind.Undefined)
            {
                logger.Error("ERROR: empty stash returned");
                return 0;
            }

            var count = 0;
            foreach (var item in json.GetProperty("items").EnumerateArray())
            {
                var frameType = item.GetIntOrDefault("frameType", 0);
                var identified = item.GetProperty("identified").GetBoolean();
                var ilvl = item.GetIntOrDefault("ilvl", 0);

                // only check category for rares of ilvl 60+
                //
                var getCat = !forChaosRecipe && identified && (frameType == 1 || frameType == 2)
                    || forChaosRecipe && frameType == 2 && ilvl >= Config.MinIlvl && (Config.AllowIDedSets || !identified);

                var category = getCat ? item.DetermineCategory(forChaosRecipe) : Cat.Junk;

                items.Add(category, item, tabIndex);
                ++count;
            }

            items.Sort();
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

#if true
                JsonElement json = await GetJsonByTabIndex(tabIndex);
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
                    var category = Cat.Junk;

                    // normal, magic, rare
                    //
                    if (frameType == 0 || frameType == 1 || frameType == 2)
                    {
                        var w = item.GetIntOrDefault("w", 999);
                        var h = item.GetIntOrDefault("h", 999);

                        if (w == 1 && h == 1)
                        {
                            var baseType = item.GetStringOrDefault("baseType");
                            if (!baseType.EndsWith("Map", StringComparison.OrdinalIgnoreCase))
                                continue;
                        }
                        else if (w != 1 || h != 2)
                        {
                            // for non-flask quality items
                            //
                            category = item.DetermineCategory();
                            if (category == Cat.Junk)
                                continue;
                        }
                    }
                    // otherwise, skip all non-gems
                    //
                    else if (frameType != 4)
                    {
                        continue;
                    }

                    var quality = GetQuality(item);
                    if (quality > 0 && (frameType != 0 || quality != 20))
                        items.Add(category, item, tabIndex, quality);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"HTTP error getting quality tab contents: {ex.Message}");
            }

            static int GetQuality(JsonElement item)
            {
                if (!item.TryGetProperty("properties", out var properties))
                    return 0;
                var quality = 0;
                foreach (var prop in properties.EnumerateArray())
                {
                    var propType = prop.GetIntOrDefault("type", -1);
                    if (propType != 6)
                        continue;

                    var valueString = prop.GetProperty("values").ToString();

                    var m = RegexPlusDigitsPercent().Match(valueString);
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

                if (Config.CurrencyTabIndex < 0 || Currency.CurrencyList.Count == 0)
                    return;

                if (listCurrencyToConsole)
                    Console.WriteLine("currency;count;ratio;value");

                var currencyDict = Currency.GetWebDictionary();
                JsonElement json = await GetJsonByTabIndex(Config.CurrencyTabIndex);
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

        public static async Task<bool> FindAndSaveTab(string tabName)
        {
            var foundTabIndex = -1;
            try
            {
                JsonElement tabList = await Config.GetTabList();
                foreach (var tab in tabList.GetProperty("tabs").EnumerateArray())
                {
                    var name = tab.GetProperty("n").GetString();
                    var i = tab.GetIntOrDefault("i", -1);

                    if (string.Equals(tabName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        foundTabIndex = i;
                        logger.Info($"Found stash tab '{tabName}' at index {i}");
                        JsonElement json = await GetJsonByTabIndex(i);
                        var jsonString = JsonSerializer.Serialize(json, RawJsonConfiguration.SerializerOptions);
                        var fileName = Config.TabFileName(tabName);
                        File.WriteAllText(fileName, jsonString);
                        logger.Info($"Contents of tab '{tabName}' written to {fileName}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"HTTP error getting tab or tab list: {ex.Message}");
            }
            if (foundTabIndex < 0)
            {
                logger.Error($"ERROR: stash tab '{tabName}' not found");
                return false;
            }
            return true;
        }

        private static Task<JsonElement> GetJsonByTabIndex(int tabIndex)
        {
            var stashTabUrl = "https://www.pathofexile.com/character-window/get-stash-items"
                + $"?league={Uri.EscapeDataString(Config.League)}&tabIndex={tabIndex}&accountName={Uri.EscapeDataString(Config.Account)}";
            return Config.GetJsonForUrl(stashTabUrl, tabIndex);
        }

        private static Task<JsonElement> GetJsonByTabIndex(int tabIndex, string league)
        {
            var stashTabUrl = "https://www.pathofexile.com/character-window/get-stash-items"
                + $"?league={Uri.EscapeDataString(league)}&tabIndex={tabIndex}&accountName={Uri.EscapeDataString(Config.Account)}";
            return Config.GetJsonForUrl(stashTabUrl, tabIndex);
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
                var account = Config.Account;
                var realm = "pc";
                var characterName = Config.Character;

                JsonElement json = await GetJsonForCharacter(account, realm, characterName);

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

        private static async Task<JsonElement> GetJsonForCharacter(string account, string realm, string characterName)
        {
            // for some reason, this is a form POST, not a GET
            //
            var formContent = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("accountName", account),
                    new KeyValuePair<string, string>("realm", realm),
                    new KeyValuePair<string, string>("character", characterName),
                ]);
            var inventoryUrl = "https://www.pathofexile.com/character-window/get-items";
            var response = await Config.HttpClient.PostAsync(inventoryUrl, formContent);
            RateLimit.UpdateLimits(response);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Config.MaybeSavePageJson(json, $"inventory_{characterName}");
            return json;
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
                if (league.EndsWith("HC"))
                    league = string.Concat("Hardcore ", league.AsSpan(0, league.Length - 3));
                else if (league.StartsWith("SSF"))
                    league = string.Concat("Hardcore ", league.AsSpan(3));

                var poeNinjaUrl = $"https://poe.ninja/api/data/currencyoverview?league={Uri.EscapeDataString(league)}&type=Currency";
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
            if (!string.IsNullOrWhiteSpace(Config.InitialImportFile))
            {
                yield return $"Import \"{Config.InitialImportFile}\" Optional";
                yield return "";
            }

            foreach (var c in ItemClassForFilter.Iterator())
            {
                if (c.Skip)
                    continue;

                var counts = items.GetCounts(c.Category);

                //if (!counts.ShouldShowInFilter)
                //    continue;
                var prefix = counts.ShouldShowInFilter ? "" : "# ";

                var canBeIded = Config.AllowIDedSets && (c.Category == Cat.Rings || c.Category == Cat.Amulets);
                var fontSize = Config.FilterDisplay.FontSize > 10 ? Config.FilterDisplay.FontSize : c.DefaultFontSize;

                var catLines = GetLinesForCategory(c.Category, c.CategoryStr, c.FilterClass, fontSize, canBeIded, counts);
                foreach (var catLine in catLines)
                    yield return prefix + catLine;

                // Add in 2x3 bows and 1x4 2-handed weapons
                //
                if (c.Category == Cat.OneHandWeapons)
                {
                    catLines = GetLinesForCategory(Cat.TwoHandWeapons, "2x3 Bows", "Bows", fontSize, canBeIded, counts);
                    foreach (var catLine in catLines)
                        yield return prefix + catLine;

                    catLines = GetLinesForCategory(Cat.TwoHandWeapons, "1x4 2hd weapons", "Two Hand\" \"Staves", fontSize, canBeIded, counts);
                    foreach (var catLine in catLines)
                        yield return prefix + catLine;
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
                        yield return $"SetFontSize {c.ItemDisplay.FontSize}";
                        yield return $"SetTextColor {c.ItemDisplay.TextColor}";
                        yield return $"SetBorderColor {c.ItemDisplay.BorderColor}";
                        yield return $"SetBackgroundColor {c.ItemDisplay.BackGroundColor}";
                        yield return "";
                    }
                }
            }

            yield return "";
            yield return $"Import \"{Config.SourceFilterName}\"";

            static List<string> GetLinesForCategory(Cat category, string catDescription, string filterString, int fontSize, bool canBeIded, ItemSet.ItemCounts counts)
            {
                var result = new List<string>();
                if ((counts.MaxItemLevelToShow == 0 || counts.MaxItemLevelToShow >= 75)
                    && Config.FilterDisplay.TextColor != Config.FilterDisplay.TextColor75)
                    GetLinesForCategory2(75, Config.FilterDisplay.TextColor75);
                GetLinesForCategory2(Config.MinIlvl, Config.FilterDisplay.TextColor);
                return result;

                void GetLinesForCategory2(int minIlvl, string textColor)
                {
                    result.Add($"# {catDescription} for chaos");
                    result.Add("Show");
                    result.Add($"   Class \"{filterString}\"");
                    result.Add("   Rarity Rare");
                    result.Add($"   SetFontSize {fontSize}");
                    result.Add($"   SetTextColor {textColor}");
                    result.Add($"   SetBackgroundColor {Config.FilterDisplay.BackGroundColor}");
                    result.Add($"   SetBorderColor {Config.FilterDisplay.BorderColor}");
                    result.Add($"   ItemLevel >= {minIlvl}");
                    if (counts.MaxItemLevelToShow > 0)
                        result.Add($"   ItemLevel <= {counts.MaxItemLevelToShow}");
                    if (!canBeIded)
                        result.Add("   Identified False");

                    if (filterString == "Bows")
                    {
                        result.Add("   Height = 3");
                        result.Add("   Sockets < 6");

                    }
                    else if (category == Cat.TwoHandWeapons)
                    {
                        result.Add("   Width = 1");
                    }
                    else if (category == Cat.OneHandWeapons)
                    {
                        result.Add("   Height = 3");
                        result.Add("   Width = 1");
                    }
                    else if (category == Cat.BodyArmours)
                    {
                        result.Add("   Sockets < 6");
                    }

                    result.Add("");
                }
            }
        }


        static async Task TailClientTxt(CancellationToken token)
        {
            var savedClientFileName = Config.ClientFileName;
            if (!File.Exists(savedClientFileName))
            {
                await Task.Delay(1000, CancellationToken.None);
                return;
            }

            using var fs = new FileStream(savedClientFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            sr.BaseStream.Seek(0, SeekOrigin.End);

            logger.Info($"tailing '{savedClientFileName}'");

            try
            {
                const string loginPattern = "login.pathofexile.com";
                const string maybeWhisperPattern = "] @";
                var sawLoginLine = false;
                while (!token.IsCancellationRequested && string.Equals(savedClientFileName, Config.ClientFileName))
                {
                    string newArea = null;
                    string line = await sr.ReadLineAsync(token);
                    while (line != null)
                    {
                        var i = line.IndexOf(Config.AreaEnteredPattern);
                        if (i >= 0)
                            newArea = line.Substring(i + Config.AreaEnteredPattern.Length).Trim().TrimEnd('.');
                        else if (line.Contains(loginPattern, StringComparison.OrdinalIgnoreCase))
                            sawLoginLine = true;
                        else if (line.Contains(maybeWhisperPattern))
                        {
                            var m = RegexWhisperPattern().Match(line);
                            if (m.Success)
                            {
                                Config.LastWhisper = m.Groups[1].Captures[0].Value ?? "";
                                if (Config.LastWhisper.Contains(' '))
                                    Config.LastWhisper = Config.LastWhisper.Substring(Config.LastWhisper.LastIndexOf(' ') + 1);
                                logger.Info($"whisper: '{Config.LastWhisper}'");
                            }
                        }
                        line = await sr.ReadLineAsync(token);
                    }

                    if (reloadConfig)
                    {
                        await Config.ReadConfigFile();
                        CheckHotkeyRegistration();
                        overlay?.SendKey(ConsoleKey.R);
                        reloadConfig = false;
                    }

                    if (checkCharacter || sawLoginLine && newArea != null && !isPaused)
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

                    if (newArea != null && wasTown || forceFilterUpdate)
                        await CheckForUpdate();
                    else if (newArea != null)
                        SetOverlayStatusMessage();

                    if (highlightSetsToSell)
                    {
                        var setToSell = itemsCurrent.GetSetToSell(Config.AllowIDedSets, Config.ChaosParanoiaLevel);
                        overlay?.SetItemSetToSell(setToSell);
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
                        overlay?.SetItemSetToSell(qualitySet);
                        overlay?.SendKey(ConsoleKey.Q);
                        if (qualitySet == null || !qualitySet.HasAnyItems())
                            overlay?.SetStatus("No quality sets", false);
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

        static bool PoeIsActiveWindow()
        {
            if (overlay != null) return overlay.IsPoeWindowActivated();
            var activeWindowTitle = GetActiveWindowTitle();
            //logger.Warn($"no overlay, but active window is '{activeWindowTitle}'");
            return string.Equals(activeWindowTitle, "Path of Exile", StringComparison.OrdinalIgnoreCase);
        }

        [LibraryImport("user32.dll")]
        private static partial IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private static string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }

        [GeneratedRegex("\\+(\\d+)%")]
        private static partial Regex RegexPlusDigitsPercent();

        [GeneratedRegex("\\[INFO Client \\d+] @[^ ]+ ([^:]+):")]
        private static partial Regex RegexWhisperPattern();
    }
}
