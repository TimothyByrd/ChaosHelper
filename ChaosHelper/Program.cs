using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConsoleHotKey;

using Overlay.NET.Common;

namespace ChaosHelper
{
    class Program
    {
        static RawJsonConfiguration configuration;
        static HttpClient httpClient;

        static string account;
        static string league;
        static string character;
        static string templateFileName;
        static string filterFileName;
        static string clientFileName;
        static int maxSets;
        static int maxIlvl;
        static int tabIndex;
        static bool isQuadTab = true;
        static bool allowIDedSets;
        static bool singleSetsForChaos;
        static bool levelLimitJewelry;
        static bool includeInventoryOnForce;
        static List<string> townZones;
        static List<int> highlightColors;
        static string filterColor;
        static string filterMarker;

        static HotKeyBinding highlightItemsHotkey;
        static HotKeyBinding showJunkItemsHotkey;
        static HotKeyBinding forceUpdateHotkey;
        static HotKeyBinding characterCheckHotkey;
        static HotKeyBinding testModeHotkey;

        static bool forceFilterUpdate = false;
        static bool checkCharacter = false;
        static bool highlightSetsToSell = false;

        static ChaosOverlay overlay;

        static ItemSet itemsPrevious = null;
        static ItemSet itemsCurrent = null;

        const int ilvl60 = 60;

        static void Main()
        {
            Console.Title = "ChaosHelper.exe";
            MainAsync().Wait();

            Console.WriteLine("Press 'Enter' to end program");
            Console.Read();
        }

        static async Task MainAsync()
        {
            Log.Register("Console", new ConsoleLog());
            Log.Info("******************************************** Startup");

            if (!await ConfigureSettings())
                return;

            // Define the cancellation token.
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;

            var keyboardTask = Task.Run(() =>
            {
                Task.Delay(2000);
                Log.Info("Press 'Escape' to exit, '?' for help");
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
                            Log.Info("Forcing a filter update");
                            forceFilterUpdate = true;
                        }
                        else if (keyInfo.Key == ConsoleKey.C)
                        {
                            Log.Info("Rechecking character and league");
                            checkCharacter = true;
                        }
                        else if (keyInfo.Key == ConsoleKey.H)
                        {
                            Log.Info("Highlighting sets to sell");
                            highlightSetsToSell = true;
                        }
                        else if (keyInfo.KeyChar == '?')
                        {
                            Log.Info("\t'?' for this help");
                            Log.Info("\t'n' to highlight a set of items to sell");
                            Log.Info("\t'j' to highlight junk items in the stash tab");
                            Log.Info("\t'f' to force a filter update");
                            Log.Info("\t'c' to switch characters");
                            Log.Info("\t't' to toggle stash test mode (to make sure the rectangle is good)");
                            //Log.Info("\t'h' to highlight in tab");
                        }
                        else
                            overlay?.SendKey(keyInfo.Key);
                        Log.Info("Press 'Escape' to exit, '?' for help");
                    }
                    else
                        Task.Delay(100);
                }
            });

            Task overlayTask = null;
            var processName = configuration["processName"];
            if (!string.IsNullOrWhiteSpace(processName))
            {
                overlayTask = Task.Run(() =>
                {
                    overlay = new ChaosOverlay();
                    var stashPageXYWH = configuration.GetRectangle("stashPageXYWH");
                    overlay.RunOverLay(processName, stashPageXYWH, isQuadTab, highlightColors, token);
                    overlay = null;
                });
            }

            // get initial counts
            itemsCurrent = new ItemSet();
            await GetTabContents(tabIndex, itemsCurrent);
            itemsCurrent.RefreshCounts();
            itemsCurrent.CalculateClassesToShow(maxSets);
            overlay?.SetCurrentItems(itemsCurrent);

            if (overlayTask != null && !overlayTask.IsCompleted)
            {
                try
                {
                    if (highlightItemsHotkey != null || showJunkItemsHotkey != null
                        || forceUpdateHotkey != null || characterCheckHotkey != null
                        || testModeHotkey != null)
                    {
                        Log.Info("registering hotkeys");

                        void MaybeRegister(HotKeyBinding x)
                        {
                            if (x != null)
                                HotKeyManager.RegisterHotKey(x.Key, x.Modifiers);
                        }
                        MaybeRegister(highlightItemsHotkey);
                        MaybeRegister(showJunkItemsHotkey);
                        MaybeRegister(forceUpdateHotkey);
                        MaybeRegister(characterCheckHotkey);
                        MaybeRegister(testModeHotkey);
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
            if (e.Key == highlightItemsHotkey?.Key && e.Modifiers == highlightItemsHotkey?.Modifiers)
            {
                Log.Info("Highlighting sets to sell");
                highlightSetsToSell = true;
            }
            else if (e.Key == showJunkItemsHotkey?.Key && e.Modifiers == showJunkItemsHotkey?.Modifiers)
                overlay?.SendKey(ConsoleKey.J);
            else if (e.Key == forceUpdateHotkey?.Key && e.Modifiers == forceUpdateHotkey?.Modifiers)
            {
                Log.Info("Forcing a filter update");
                forceFilterUpdate = true;
            }
            else if (e.Key == characterCheckHotkey?.Key && e.Modifiers == characterCheckHotkey?.Modifiers)
            {
                Log.Info("Rechecking character and league");
                checkCharacter = true;
            }
            else if (e.Key == testModeHotkey?.Key && e.Modifiers == testModeHotkey?.Modifiers)
                overlay?.SendKey(ConsoleKey.T);
        }

        static async Task<bool> ConfigureSettings()
        {
            var configFile = Path.GetFullPath("./settings.jsonc");
            if (!File.Exists(configFile))
            {
                Log.Error($"ERROR: config file 'settings.jsonc' not found");
                return false;
            }

            try
            {
                configuration = new RawJsonConfiguration(configFile);
            }
            catch (Exception ex)
            {
                Log.Error($"ERROR cannot read settings.jsonc: {ex.Message}");
                return false;
            }

            account = configuration["account"];
            if (string.IsNullOrWhiteSpace(account))
            {
                Log.Error($"ERROR: account not configured");
                return false;
            }

            var poesessid = configuration["poesessid"];
            if (string.IsNullOrWhiteSpace(poesessid))
            {
                Log.Error($"ERROR: poesessid not configured");
                return false;
            }

            var cookieContainer = new CookieContainer();
            cookieContainer.Add(new Cookie("POESESSID", poesessid, "/", "pathofexile.com"));

            var handler = new HttpClientHandler() { CookieContainer = cookieContainer };
            httpClient = new HttpClient(handler);

            if (!await CheckAccount())
                return false;

            maxSets = configuration.GetInt("maxSets", 12);
            maxIlvl = configuration.GetInt("maxIlvl", -1);
            allowIDedSets = configuration.GetBoolean("allowIDedSets", false);
            singleSetsForChaos = configuration.GetBoolean("singleSetsForChaos", false);
            levelLimitJewelry = configuration.GetBoolean("levelLimitJewelry", false);
            includeInventoryOnForce = configuration.GetBoolean("includeInventoryOnForce", false);
            townZones = configuration.GetStringList("townZones");

            highlightItemsHotkey = configuration.GetHotKey("highlightItemsHotkey");
            showJunkItemsHotkey = configuration.GetHotKey("showJunkItemsHotkey");
            forceUpdateHotkey = configuration.GetHotKey("forceUpdateHotkey");
            characterCheckHotkey = configuration.GetHotKey("characterCheckHotkey");
            testModeHotkey = configuration.GetHotKey("testModeHotkey");

            highlightColors = configuration.GetIntList("highlightColors");
            if (highlightColors.Count == 0)
            {
                highlightColors = new List<int> { 0xffffff, 0xffff00, 0x00ff00, 0x6060ff };
            }
            else if (highlightColors.Count != 4)
            {
                Log.Error($"ERROR: highlightColors must be empty or exactly 4 numbers");
                return false;
            }

            const string defaultFilterColor = "106 77 255";
            filterColor = configuration["filterColor"];
            if (string.IsNullOrWhiteSpace(filterColor))
                filterColor = defaultFilterColor;
            else if (filterColor.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(filterColor.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var parsedInt))
                {
                    var color = System.Drawing.Color.FromArgb(parsedInt);
                    filterColor = $"{color.R} {color.G} {color.B}";
                }
                else
                {
                    Log.Warn($"filterColor '{filterColor}' not valid - using default");
                    filterColor = defaultFilterColor;
                }
            }
            Log.Info($"filterColor is '{filterColor}'");

            filterMarker = configuration["filterMarker"];
            if (string.IsNullOrWhiteSpace(filterMarker))
                filterMarker = "section displays 20% quality rares";
            Log.Info($"filterMarker is '{filterMarker}'");

            var poeDocDir = Environment.ExpandEnvironmentVariables($"%USERPROFILE%\\Documents\\My Games\\Path of Exile\\");

            var templateBase = Environment.ExpandEnvironmentVariables(configuration["template"]);
            if (string.IsNullOrWhiteSpace(templateBase))
                templateBase = "simplesample.template";
            if (File.Exists(templateBase))
                templateFileName = templateBase;
            else
                templateFileName = Path.Combine(poeDocDir, templateBase);
            if (!File.Exists(templateFileName))
                templateFileName = Path.ChangeExtension(Path.Combine(poeDocDir, templateBase), "template");
            if (!File.Exists(templateFileName))
                templateFileName = Path.ChangeExtension(Path.Combine(poeDocDir, templateBase), "filter");
            if (!File.Exists(templateFileName))
            {
                Log.Error($"ERROR: template file '{templateBase}' not found");
                return false;
            }
            templateFileName = Path.GetFullPath(templateFileName);
            Log.Info($"using template file '{templateFileName}'");

            var filterBase = Environment.ExpandEnvironmentVariables(configuration["filter"]);
            if (string.IsNullOrWhiteSpace(filterBase))
                filterBase = "Chaos Helper";
            filterFileName = Path.ChangeExtension(Path.Combine(poeDocDir, filterBase), "filter");
            Log.Info($"will write filter file '{filterFileName}'");

            if (string.Equals(Path.GetFileName(templateFileName), Path.GetFileName(filterFileName), StringComparison.OrdinalIgnoreCase))
            {
                Log.Error("ERROR: template file and filter file must have different names");
                return false;
            }

            var clientTxtBase = configuration["clientTxt"];
            clientFileName = Environment.ExpandEnvironmentVariables(clientTxtBase);
            if (!File.Exists(clientFileName))
                clientFileName = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\Games\\Grinding Gear Games\\Path of Exile\\logs\\Client.txt");
            if (!File.Exists(clientFileName))
                clientFileName = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\Grinding Gear Games\\Path of Exile\\logs\\Client.txt");
            if (!File.Exists(clientFileName))
                clientFileName = Environment.ExpandEnvironmentVariables("%ProgramFiles%\\Grinding Gear Games\\Path of Exile\\logs\\Client.txt");
            if (!File.Exists(clientFileName))
                clientFileName = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\Steam\\steamapps\\common\\Path of Exile\\logs\\Client.txt");
            if (!File.Exists(clientFileName))
            {
                Log.Error($"ERROR: client.txt file '{clientTxtBase}' not found");
                return false;
            }

            if (!await DetermineTabIndex())
                return false;

            return true;
        }

        static void SetLeague(string s)
        {
            league = s.Replace(' ', '+');
        }

        static async Task CheckForUpdate()
        {
            itemsPrevious = itemsCurrent;
            itemsCurrent = new ItemSet();
            await GetTabContents(tabIndex, itemsCurrent);
            if (includeInventoryOnForce && forceFilterUpdate)
                await GetInventoryContents(itemsCurrent);
            itemsCurrent.RefreshCounts();
            itemsCurrent.CalculateClassesToShow(maxSets);
            
            overlay?.SetCurrentItems(itemsCurrent);
            SetOverlayStatusMessage();

            if (forceFilterUpdate || !itemsCurrent.SameClassesToShow(itemsPrevious))
            {
                var msg = itemsCurrent.GetCountsMsg();
                Log.Info($"updating filter - {msg}");
                File.WriteAllLines(filterFileName, NewFilterContents(itemsCurrent));

                var filterUpdateSound = Path.GetFullPath(".\\FilterUpdateSound.wav");
                if (File.Exists(filterUpdateSound))
                {
                    using (var player = new System.Media.SoundPlayer(filterUpdateSound))
                    {
                        player.Play();
                    }
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
                numSets = allowIDedSets ? itemsCurrent.CountPossible(true) : 0;
                if (numSets > 0)
                    msg = $"you can make {numSets} IDed sets";
                else
                    msg = $"no sets, yet";
            }
            overlay?.SetStatus(msg);
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
                    Log.Error("ERROR: stash tab index not set");
                    return;
                }

                var stashTabUrl = "https://www.pathofexile.com/character-window/get-stash-items"
                    + $"?league={league}&tabIndex={tabIndex}&accountName={account}";
                var json = await httpClient.GetFromJsonAsync<JsonElement>(stashTabUrl);
                foreach (var item in json.GetProperty("items").EnumerateArray())
                {
                    var frameType = item.GetProperty("frameType").GetInt32();
                    var identified = item.GetProperty("identified").GetBoolean();
                    var ilvl = item.GetProperty("ilvl").GetInt32();

                    var category = Cat.Junk;

                    if (frameType == 2 && ilvl >= ilvl60 && (allowIDedSets || !identified)) // only look at rares of ilvl 60+
                    {
                        var iconUrl = item.GetProperty("icon").GetString();
                        foreach (var c in ItemClass.Iterator())
                        {
                            if (iconUrl.Contains(c.CategoryStr))
                            {
                                category = c.Category;
                                if (c.Category == Cat.OneHandWeapons)
                                {
                                    if (item.GetProperty("w").GetInt32() > 1
                                        || item.GetProperty("h").GetInt32() > 3)
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
                Log.Error($"HTTP error getting tab contents: {ex.Message}");
            }
        }

        private static async Task GetInventoryContents(ItemSet items)
        {
            if (string.IsNullOrWhiteSpace(character))
            {
                Log.Error($"Error: No character name, cannot get inventory");
                return;
            }
            try
            {
                var formContent = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("accountName", account),
                new KeyValuePair<string, string>("realm", "pc"),
                new KeyValuePair<string, string>("character", character),
            });
                var inventoryUrl = "https://www.pathofexile.com/character-window/get-items";
                var response = await httpClient.PostAsync(inventoryUrl, formContent);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                foreach (var item in json.GetProperty("items").EnumerateArray())
                {
                    var inventoryId = item.GetProperty("inventoryId").GetString();
                    if (inventoryId != "MainInventory")
                        continue; // skip equipped items

                    var frameType = item.GetProperty("frameType").GetInt32();
                    var identified = item.GetProperty("identified").GetBoolean();
                    var ilvl = item.GetProperty("ilvl").GetInt32();

                    if (identified || frameType != 2 || ilvl < ilvl60)
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
                                if (item.GetProperty("w").GetInt32() > 1
                                    || item.GetProperty("h").GetInt32() > 3)
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
                Log.Error($"HTTP error getting inventory contents: {ex.Message}");
            }
        }

        static IEnumerable<string> NewFilterContents(ItemSet items)
        {
            foreach (var line in File.ReadAllLines(templateFileName))
            {
                yield return line;

                if (!line.StartsWith("#") || !line.Contains(filterMarker, StringComparison.OrdinalIgnoreCase))
                    continue;

                yield return "";
                yield return "# Begin ChaosHelper generated section";

                // always show jewelry
                //
                yield return "";
                yield return "# Jewelry for chaos";
                yield return "Show";
                yield return $"SetBorderColor {filterColor}";
                yield return $"SetTextColor {filterColor}";
                yield return "SetFontSize 45";
                if (!allowIDedSets)
                    yield return "Identified False";
                yield return $"ItemLevel >= {ilvl60}";
                if (maxIlvl > 60 && maxIlvl < 100 && levelLimitJewelry)
                    yield return $"ItemLevel <= {maxIlvl}";
                yield return "Rarity Rare";
                yield return "Class \"Rings\" \"Amulets\"";
                yield return "";

                yield return "# Belts for chaos";
                yield return "Show";
                yield return $"SetBorderColor {filterColor}";
                yield return $"SetTextColor {filterColor}";
                yield return "SetFontSize 45";
                yield return "Identified False";
                yield return $"ItemLevel >= {ilvl60}";
                if (maxIlvl > 60 && maxIlvl < 100 && levelLimitJewelry)
                    yield return $"ItemLevel <= {maxIlvl}";
                yield return "Rarity Rare";
                yield return "Class \"Belts\"";
                yield return "";

                bool Show(ItemClass c)
                {
                    return !c.Skip && items.ShouldShow(c.Category);
                }

                foreach (var c in ItemClass.Iterator())
                {
                    if (!Show(c)) continue;

                    yield return $"# {c.Category} for chaos";
                    yield return "Show";
                    yield return $"Class \"{c.FilterClass}\"";
                    yield return "Rarity Rare";
                    yield return $"SetBorderColor {filterColor}";
                    yield return $"SetTextColor {filterColor}";
                    yield return "SetFontSize 38";
                    yield return "Identified False";
                    yield return $"ItemLevel >= {ilvl60}";
                    if (maxIlvl > 60 && maxIlvl < 100)
                        yield return $"ItemLevel <= {maxIlvl}";
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
                        yield return $"SetBorderColor {filterColor}";
                        yield return $"SetTextColor {filterColor}";
                        yield return "SetFontSize 38";
                        yield return "Identified False";
                        yield return $"ItemLevel >= {ilvl60}";
                        if (maxIlvl > 60 && maxIlvl < 100)
                            yield return $"ItemLevel <= {maxIlvl}";
                        yield return "Height = 3";
                        yield return "";
                    }
                }

                yield return "# End ChaosHelper generated section";
                yield return "";
            }
        }

        static async Task<bool> DetermineTabIndex(bool forceWebCheck = false)
        {
            // see if we have configured a tab index
            //
            if (!forceWebCheck)
            {
                tabIndex = configuration.GetInt("tabIndex", -1);
                if (tabIndex >= 0)
                {
                    Log.Info($"using configured tab index = {tabIndex}");
                    isQuadTab = configuration.GetBoolean("isQuadTab", true);
                    return true;
                }
            }

            tabIndex = -1;

            var tabNameFromConfig = configuration["tabName"];
            var checkTabNames = !string.IsNullOrWhiteSpace(tabNameFromConfig);

            try
            {
                var stashTabListUrl = "https://www.pathofexile.com/character-window/get-stash-items"
                    + $"?league={league}&tabs=1&accountName={account}";
                var json = await httpClient.GetFromJsonAsync<JsonElement>(stashTabListUrl);

                foreach (var tab in json.GetProperty("tabs").EnumerateArray())
                {
                    var name = tab.GetProperty("n").GetString();
                    var tabType = tab.GetProperty("type").GetString();
                    var i = tab.GetProperty("i").GetInt32();
                    bool found = false;
                    if (checkTabNames)
                    {
                        found = string.Equals(name, tabNameFromConfig, StringComparison.OrdinalIgnoreCase);
                    }
                    else if (!name.Contains("remove-only", StringComparison.OrdinalIgnoreCase))
                    {
                        found = string.Equals(tabType, "QuadStash", StringComparison.OrdinalIgnoreCase);
                    }
                    if (found)
                    {
                        Log.Info($"found tab '{name}', index = {i}, type = {tabType}");
                        tabIndex = i;
                        isQuadTab = string.Equals(tabType, "QuadStash", StringComparison.OrdinalIgnoreCase);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"HTTP error getting tab list: {ex.Message}");
            }
            if (tabIndex < 0)
            {
                Log.Error("ERROR: stash tab not found");
                return false;
            }
            return true;
        }

        static async Task<bool> CheckAccount(bool forceWebCheck = false)
        {
            try
            {
                SetLeague(configuration["league"]);
                character = configuration["character"];

                if (forceWebCheck || string.IsNullOrEmpty(league) || league == "auto")
                {
                    const string characterPrefixPattern = "{var c = new C(";
                    const string checkUrl = "https://www.pathofexile.com/shop/redeem-key"; // because it's a relatively simple page.
                    var responseString = await httpClient.GetStringAsync(checkUrl);
                    if (string.IsNullOrWhiteSpace(responseString))
                    {
                        Log.Warn("Determine character failed to get page");
                        return false;
                    }
                    var startPos = responseString.IndexOf(characterPrefixPattern);
                    if (startPos < 0)
                    {
                        Log.Warn("Determine character failed to find structure start");
                        return false;
                    }
                    startPos += characterPrefixPattern.Length;
                    var endPos = responseString.IndexOf(");", startPos);
                    if (endPos < 0)
                    {
                        Log.Warn("Determine character failed to find structure end");
                        return false;
                    }

                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(responseString.Substring(startPos, endPos - startPos));
                    if (jsonElement.TryGetProperty("league", out var leagueElement))
                        SetLeague(leagueElement.GetString());

                    if (jsonElement.TryGetProperty("name", out var nameElement))
                        character = nameElement.GetString();
                }

                Log.Info($"account = {account}, league = '{league}', character = '{character}'");
            }
            catch (Exception ex)
            {
                Log.Error($"Determine character failed: {ex.Message}");
                return false;
            }
            return true;
        }

        static async Task TailClientTxt(CancellationToken token)
        {
            using (var fs = new FileStream(clientFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
            {
                sr.BaseStream.Seek(0, SeekOrigin.End);

                try
                {
                    const string areaPattern = "] : You have entered ";
                    const string loginPattern = "login.pathofexile.com";
                    var sawLoginLine = false;
                    while (!token.IsCancellationRequested)
                    {
                        string newArea = null;
                        string line = await sr.ReadLineAsync();
                        while (line != null)
                        {
                            var i = line.IndexOf(areaPattern);
                            if (i > 0)
                                newArea = line.Substring(i + areaPattern.Length).TrimEnd('.');
                            else if (line.IndexOf(loginPattern) > 0)
                                sawLoginLine = true;
                            line = await sr.ReadLineAsync();
                        }

                        if (checkCharacter || sawLoginLine && newArea != null)
                        {
                            await CheckAccount(forceWebCheck: true);
                            await DetermineTabIndex(forceWebCheck: true);
                            checkCharacter = false;
                            sawLoginLine = false;
                        }

                        if (newArea != null)
                        {
                            bool isTown = townZones == null || townZones.Count == 0
                                || townZones.Any(x => string.Equals(x, newArea, StringComparison.OrdinalIgnoreCase));
                            Log.Info($"new area - {newArea} - town: {isTown}");
                            overlay?.SetArea(newArea, isTown);
                        }
                        else if (forceFilterUpdate)
                            Log.Info("forcing filter update");

                        if (newArea != null || forceFilterUpdate)
                            await CheckForUpdate();

                        if (highlightSetsToSell)
                        {
                            var setToSell = itemsCurrent.GetSetToSell(allowIDedSets, singleSetsForChaos);
                            overlay?.SetitemSetToSell(setToSell);
                            overlay?.SendKey(ConsoleKey.N);
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
