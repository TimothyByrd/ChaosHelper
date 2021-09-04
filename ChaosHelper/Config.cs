using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChaosHelper
{
    class Config
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private static RawJsonConfiguration rawConfig;
        private static string exePath;
        private static bool forcingClientTxt = false;

        public enum StashReading
        {
            /// <summary>Get stash tab data from PoE site.</summary>
            Normal,
            /// <summary>Get stash tab data from PoE site, and save to files.</summary>
            Record,
            /// <summary>Get stash tab data by copying json to clipboard.</summary>
            Manual,
            /// <summary>Get stash tab data from saved data files.</summary>
            Playback,
        }

        public static HttpClient HttpClient { get; private set; }

        public static string Account { get; private set; }
        public static string League { get; private set; }
        public static string Character { get; private set; }
        public static string TemplateFileName { get; private set; }
        public static string FilterFileName { get; private set; }
        public static string ClientFileName { get; private set; }
        public static string FilterUpdateSound { get; private set; }
        public static float FilterUpdateVolume { get; private set; }
        public static int MaxSets { get; private set; }
        public static int MaxIlvl { get; private set; }
        public static int MinIlvl { get; private set; }
        public static int RecipeTabIndex { get; private set; }
        public static bool IsQuadTab { get; private set; }
        public static bool QualityIsQuadTab { get; private set; }
        public static int QualityFlaskRecipeSlop { get; private set; }
        public static int QualityGemRecipeSlop { get; private set; }
        public static int QualityVaalGemMaxQualityToUse { get; private set; }
        public static Dictionary<int, string> DumpTabDictionary { get; private set; } = new Dictionary<int, string>();
        public static bool AllowIDedSets { get; private set; }
        public static int ChaosParanoiaLevel { get; private set; }
        public static string IgnoreMaxSets { get; private set; }
        public static string IgnoreMaxIlvl { get; private set; }
        public static List<string> TownZones { get; private set; }
        public static List<int> HighlightColors { get; private set; }
        public static string FilterColor { get; private set; }
        public static string FilterMarker { get; private set; }
        public static string AreaEnteredPattern { get; private set; }
        public static HotKeyBinding HighlightItemsHotkey { get; private set; }
        public static HotKeyBinding ShowQualityItemsHotkey { get; private set; }
        public static HotKeyBinding ShowJunkItemsHotkey { get; private set; }
        public static HotKeyBinding ForceUpdateHotkey { get; private set; }
        public static HotKeyBinding CharacterCheckHotkey { get; private set; }
        public static HotKeyBinding TestPatternHotkey { get; private set; }
        public static bool ShouldHookMouseEvents { get; private set; }
        public static string RequiredProcessName { get; private set; }
        public static System.Drawing.Rectangle StashPageXYWH { get; private set; }
        public static int StashPageVerticalOffset { get; private set; }
        public static StashReading StashReadMode { get; private set; }
        public static bool ForceSteam { get; set; }
        public static int CurrencyTabIndex { get; set; } = -1;
        public static int QualityTabIndex { get; set; } = -1;
        public static bool FilterMarkerChecked { get; set; }
        public static bool PutFilterLineAtTop { get; set; }
        public static string FilterInsertFile { get; private set; }
        public static double DefenseVariance { get; private set; }
        public static bool ShowMinimumCurrency { get; private set; }

        public static bool IsTown(string newArea)
        {
            bool isTown = TownZones == null || TownZones.Count == 0
                || TownZones.Any(x => string.Equals(x, newArea, StringComparison.OrdinalIgnoreCase));
            return isTown;
        }

        public static bool HaveAHotKey()
        {
            var haveAHotKey = HighlightItemsHotkey != null
                || ShowQualityItemsHotkey != null
                || ShowJunkItemsHotkey != null
                || ForceUpdateHotkey != null
                || CharacterCheckHotkey != null
                || TestPatternHotkey != null;
            return haveAHotKey;
        }

        private static bool HotKeyMatches(HotKeyBinding hk, ConsoleHotKey.HotKeyEventArgs e)
        {
            return (e.Key == hk?.Key && e.Modifiers == hk?.Modifiers);
        }

        public static bool IsHighlightItemsHotkey(ConsoleHotKey.HotKeyEventArgs e)
        {
            return HotKeyMatches(HighlightItemsHotkey, e);
        }

        public static bool IsShowQualityItemsHotkey(ConsoleHotKey.HotKeyEventArgs e)
        {
            return HotKeyMatches(ShowQualityItemsHotkey, e);
        }

        public static bool IsShowJunkItemsHotkey(ConsoleHotKey.HotKeyEventArgs e)
        {
            return HotKeyMatches(ShowJunkItemsHotkey, e);
        }

        public static bool IsForceUpdateHotkey(ConsoleHotKey.HotKeyEventArgs e)
        {
            return HotKeyMatches(ForceUpdateHotkey, e);
        }

        public static bool IsCharacterCheckHotkey(ConsoleHotKey.HotKeyEventArgs e)
        {
            return HotKeyMatches(CharacterCheckHotkey, e);
        }

        public static bool IsTestPatternHotkey(ConsoleHotKey.HotKeyEventArgs e)
        {
            return HotKeyMatches(TestPatternHotkey, e);
        }

        public static bool LimitIlvl(ItemClassForFilter c)
        {
            var limitIlvl = MaxIlvl > 60 && MaxIlvl < 100 && IgnoreMaxIlvl.IndexOf(c.CategoryStr, StringComparison.OrdinalIgnoreCase) < 0;
            return limitIlvl;
        }

        public static async Task<bool> ReadConfigFile()
        {
            exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var configFile = Path.Combine(exePath, "settings.jsonc");
            if (!File.Exists(configFile))
            {
                logger.Error("ERROR: config file 'settings.jsonc' not found");
                return false;
            }

            try
            {
                rawConfig = new RawJsonConfiguration(configFile);
            }
            catch (Exception ex)
            {
                logger.Error($"ERROR cannot read settings.jsonc: {ex.Message}");
                return false;
            }

            FilterUpdateSound = Path.Combine(exePath, "FilterUpdateSound.wav");
            var filterUpdateVolumeInt = rawConfig.GetInt("soundFileVolume", 50).Clamp(0, 100);
            FilterUpdateVolume = filterUpdateVolumeInt / 100.0f;

            FilterInsertFile = Path.Combine(exePath, "filter_insert.txt");
            if (!File.Exists(FilterInsertFile))
                FilterInsertFile = null;

            Account = rawConfig["account"];
            if (string.IsNullOrWhiteSpace(Account))
            {
                logger.Error("ERROR: account not configured");
                return false;
            }

            var poesessid = rawConfig["poesessid"];
            if (string.IsNullOrWhiteSpace(poesessid))
            {
                logger.Error("ERROR: poesessid not configured");
                return false;
            }

            var cookieContainer = new CookieContainer();
            cookieContainer.Add(new Cookie("POESESSID", poesessid, "/", "pathofexile.com"));

            var handler = new HttpClientHandler() { CookieContainer = cookieContainer };
            HttpClient = new HttpClient(handler);
            HttpClient.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("ChaosHelper", "1.0"));

            var stashReadModeStr = rawConfig["stashReadMode"];
            if (Enum.TryParse<StashReading>(stashReadModeStr, true, out var stashReadMode)
                && Enum.IsDefined(typeof(StashReading), stashReadMode))
                StashReadMode = stashReadMode;
            else
                StashReadMode = StashReading.Normal;

            if (!await CheckAccount())
                return false;

            MaxSets = rawConfig.GetInt("maxSets", 12);
            MaxIlvl = rawConfig.GetInt("maxIlvl", -1);
            MinIlvl = rawConfig.GetInt("minIlvl", 60);
            AllowIDedSets = rawConfig.GetBoolean("allowIDedSets", false);
            ChaosParanoiaLevel = rawConfig.GetInt("chaosParanoiaLevel", 0);
            IgnoreMaxSets = rawConfig["ignoreMaxSets"];
            IgnoreMaxIlvl = rawConfig["ignoreMaxIlvl"];
            TownZones = rawConfig.GetStringList("townZones");

            AreaEnteredPattern = rawConfig["areaEnteredPattern "];
            if (string.IsNullOrWhiteSpace(AreaEnteredPattern))
                AreaEnteredPattern = "] : You have entered ";

            ShowMinimumCurrency = rawConfig.GetBoolean("showMinimumCurrency", false);
            Currency.SetArray(rawConfig.GetArray("currency"));

            static HotKeyBinding GetHotKey(string s)
            {
                var result = rawConfig.GetHotKey(s);
                if (result != null)
                {
                    logger.Info($"hot key '{s}' is '{rawConfig[s]}'");
                }
                return result;
            }

            HighlightItemsHotkey = GetHotKey("highlightItemsHotkey");
            ShowQualityItemsHotkey = GetHotKey("showQualityItemsHotkey");
            ShowJunkItemsHotkey = GetHotKey("showJunkItemsHotkey");
            ForceUpdateHotkey = GetHotKey("forceUpdateHotkey");
            CharacterCheckHotkey = GetHotKey("characterCheckHotkey");
            TestPatternHotkey = GetHotKey("testPatternHotkey");

            ShouldHookMouseEvents = rawConfig.GetBoolean("hookMouseEvents", false);
            RequiredProcessName = rawConfig["processName"];
            StashPageXYWH = rawConfig.GetRectangle("stashPageXYWH");
            StashPageVerticalOffset = rawConfig.GetInt("stashPageVerticalOffset", 0);

            HighlightColors = rawConfig.GetColorList("highlightColors");
            if (HighlightColors.Count == 0)
            {
                HighlightColors = new List<int> { 0xffffff, 0xffff00, 0x00ff00, 0x6060ff };
            }
            else if (HighlightColors.Count != 4)
            {
                logger.Error("ERROR: highlightColors must be empty or exactly 4 numbers");
                return false;
            }

            const string defaultFilterColor = "106 77 255";
            FilterColor = rawConfig["filterColor"].CheckColorString(defaultFilterColor);
            logger.Info($"filterColor is '{FilterColor}'");

            FilterMarker = rawConfig["filterMarker"];
            if (string.IsNullOrWhiteSpace(FilterMarker))
                FilterMarker = "Override 270";
            logger.Info($"filterMarker is '{FilterMarker}'");

            var poeDocDir = Environment.ExpandEnvironmentVariables($"%USERPROFILE%\\Documents\\My Games\\Path of Exile\\");

            var templateBase = Environment.ExpandEnvironmentVariables(rawConfig["template"]);
            if (string.IsNullOrWhiteSpace(templateBase))
                templateBase = "simplesample.template";
            if (File.Exists(templateBase))
                TemplateFileName = templateBase;
            else
                TemplateFileName = Path.Combine(poeDocDir, templateBase);
            if (!File.Exists(TemplateFileName))
                TemplateFileName = Path.ChangeExtension(Path.Combine(poeDocDir, templateBase), "template");
            if (!File.Exists(TemplateFileName))
                TemplateFileName = Path.ChangeExtension(Path.Combine(poeDocDir, templateBase), "filter");
            if (!File.Exists(TemplateFileName))
            {
                logger.Error($"ERROR: template file '{templateBase}' not found");
                return false;
            }
            TemplateFileName = Path.GetFullPath(TemplateFileName);
            logger.Info($"using template file '{TemplateFileName}'");

            var filterBase = Environment.ExpandEnvironmentVariables(rawConfig["filter"]);
            if (string.IsNullOrWhiteSpace(filterBase))
                filterBase = "Chaos Helper";
            FilterFileName = Path.ChangeExtension(Path.Combine(poeDocDir, filterBase), "filter");
            logger.Info($"will write filter file '{FilterFileName}'");

            if (string.Equals(Path.GetFileName(TemplateFileName), Path.GetFileName(FilterFileName), StringComparison.OrdinalIgnoreCase))
            {
                logger.Error("ERROR: template file and filter file must have different names");
                return false;
            }

            var itemModFile = Path.Combine(exePath, "itemMods.csv");
            if (File.Exists(itemModFile))
            {
                ItemMod.ReadItemModFile(itemModFile);
                logger.Info($"item mod file - there are {ItemMod.PossibleMods.Count} mods");
            }

            var itemRuleFile = Path.Combine(exePath, "itemRules.csv");
            if (File.Exists(itemRuleFile))
            {
                ItemRule.ReadRuleFile(itemRuleFile);
                logger.Info($"item rule file - there are {ItemRule.Rules.Count} rules");
            }

            var clientTxtBase = rawConfig["clientTxt"];
            ClientFileName = Environment.ExpandEnvironmentVariables(clientTxtBase);
            forcingClientTxt = File.Exists(ClientFileName);
            if (File.Exists(ClientFileName))
            {
                forcingClientTxt = true;
            }
            else if (ForceSteam)
            {
                ClientFileName = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\Steam\\steamapps\\common\\Path of Exile\\logs\\Client.txt");
                if (!File.Exists(ClientFileName))
                    ClientFileName = "D:/pf_games/Steam/steamapps/common/Path of Exile/logs/client.txt";
                forcingClientTxt = File.Exists(ClientFileName);
            }
            else
            {
                ClientFileName = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\Games\\Grinding Gear Games\\Path of Exile\\logs\\Client.txt");
                if (!File.Exists(ClientFileName))
                    ClientFileName = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\Grinding Gear Games\\Path of Exile\\logs\\Client.txt");
                if (!File.Exists(ClientFileName))
                    ClientFileName = Environment.ExpandEnvironmentVariables("%ProgramFiles%\\Grinding Gear Games\\Path of Exile\\logs\\Client.txt");
                if (!File.Exists(ClientFileName))
                    ClientFileName = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\Steam\\steamapps\\common\\Path of Exile\\logs\\Client.txt");
            }

            if (!File.Exists(ClientFileName))
            {
                logger.Error($"ERROR: client.txt file '{clientTxtBase}' not found");
                return false;
            }
            logger.Info($"will tail client file '{ClientFileName}'");

            if (!await DetermineTabIndicies())
                return false;

            Helpers.ReadBaseItemsJson();

            FilterMarkerChecked = false;
            PutFilterLineAtTop = false;

            return true;
        }

        public static async Task<bool> CheckAccount(bool forceWebCheck = false)
        {
            try
            {
                League = rawConfig["league"];
                Character = rawConfig["character"];

                if (forceWebCheck || string.IsNullOrEmpty(League) || League == "auto")
                {
                    const string characterPrefixPattern = "{var c = new C(";
                    const string checkUrl = "https://www.pathofexile.com/shop/redeem-key"; // because it's a relatively simple page.
                    var responseString = await HttpClient.GetStringAsync(checkUrl);
                    if (string.IsNullOrWhiteSpace(responseString))
                    {
                        logger.Warn("Determine character failed to get page");
                        return false;
                    }
                    var startPos = responseString.IndexOf(characterPrefixPattern);
                    if (startPos < 0)
                    {
                        logger.Warn("Determine character failed to find structure start");
                        return false;
                    }
                    startPos += characterPrefixPattern.Length;
                    var endPos = responseString.IndexOf(");", startPos);
                    if (endPos < 0)
                    {
                        logger.Warn("Determine character failed to find structure end");
                        return false;
                    }

                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(responseString.Substring(startPos, endPos - startPos));
                    if (jsonElement.TryGetProperty("league", out var leagueElement))
                        League = leagueElement.GetString();

                    if (jsonElement.TryGetProperty("name", out var nameElement))
                        Character = nameElement.GetString();
                }

                logger.Info($"account = {Account}, league = '{League}', character = '{Character}'");
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "Determine character failed");
                return false;
            }
            return true;
        }

        public static async Task<bool> DetermineTabIndicies(bool forceWebCheck = false)
        {
            // see if we have configured a tab index
            //
            if (!forceWebCheck)
            {
                RecipeTabIndex = rawConfig.GetInt("tabIndex", -1);
                if (RecipeTabIndex >= 0)
                {
                    logger.Info($"using configured tab index = {RecipeTabIndex}");
                    IsQuadTab = rawConfig.GetBoolean("isQuadTab", true);
                    return true;
                }
            }

            RecipeTabIndex = -1;
            CurrencyTabIndex = -1;
            QualityTabIndex = -1;

            var tabNameFromConfig = rawConfig["tabName"];
            var qualityTabNameFromConfig = rawConfig["qualityTab"];
            var checkTabNames = !string.IsNullOrWhiteSpace(tabNameFromConfig);

            var dumpTabNames = rawConfig.GetStringList("dumpTabs");
            DumpTabDictionary.Clear();
            logger.Info($"dumpTabs has {dumpTabNames.Count} entries");
            DefenseVariance = Math.Clamp(rawConfig.GetDouble("defenseVariance", 0.7), 0.1, 1.0);

            try
            {
                var stashTabListUrl = System.Uri.EscapeUriString("https://www.pathofexile.com/character-window/get-stash-items"
                    + $"?league={League}&tabs=1&accountName={Account}");
                JsonElement json = await GetJsonForUrl(stashTabListUrl, "tabList");

                var lookForCurrencyTab = Currency.CurrencyList.Any();
                var lookForQualityTab = !string.IsNullOrWhiteSpace(qualityTabNameFromConfig);

                const string removeOnlyTabPattern = "remove-only";

                bool Done()
                {
                    return RecipeTabIndex >= 0
                        && (!lookForCurrencyTab || CurrencyTabIndex >= 0)
                        && (!lookForQualityTab || QualityTabIndex >= 0)
                        && !dumpTabNames.Any();
                }

                foreach (var tab in json.GetProperty("tabs").EnumerateArray())
                {
                    var name = tab.GetProperty("n").GetString();
                    var isRemoveOnly = name.Contains(removeOnlyTabPattern, StringComparison.OrdinalIgnoreCase);
                    var tabType = tab.GetProperty("type").GetString();
                    var i = tab.GetIntOrDefault("i", -1);
                    bool found = false;

                    if (RecipeTabIndex == -1)
                    {
                        if (checkTabNames)
                            found = string.Equals(name, tabNameFromConfig, StringComparison.OrdinalIgnoreCase);
                        else if (!isRemoveOnly)
                            found = string.Equals(tabType, "QuadStash", StringComparison.OrdinalIgnoreCase);

                        if (found)
                        {
                            RecipeTabIndex = i;
                            logger.Info($"found chaos recipe tab '{name}', index = {RecipeTabIndex}, type = {tabType}");
                            IsQuadTab = string.Equals(tabType, "QuadStash", StringComparison.OrdinalIgnoreCase);
                        }
                    }

                    if (lookForCurrencyTab && CurrencyTabIndex == -1 && !isRemoveOnly
                        && string.Equals(tabType, "CurrencyStash", StringComparison.OrdinalIgnoreCase))
                    {
                            CurrencyTabIndex = i;
                            logger.Info($"found currency tab '{name}', index = {CurrencyTabIndex}");
                    }

                    if (lookForQualityTab && QualityTabIndex == -1
                        && string.Equals(name, qualityTabNameFromConfig, StringComparison.OrdinalIgnoreCase))
                    {
                        QualityTabIndex = i;
                        QualityIsQuadTab = string.Equals(tabType, "QuadStash", StringComparison.OrdinalIgnoreCase);
                        QualityFlaskRecipeSlop = rawConfig.GetInt("qualityFlaskRecipeSlop");
                        if (QualityFlaskRecipeSlop >= 40)
                            QualityFlaskRecipeSlop -= 40;
                        QualityGemRecipeSlop = rawConfig.GetInt("qualityGemRecipeSlop");
                        if (QualityGemRecipeSlop >= 40)
                            QualityGemRecipeSlop -= 40;
                        QualityVaalGemMaxQualityToUse = rawConfig.GetInt("qualityVaalGemMaxQualityToUse");
                        logger.Info($"found quality tab '{name}', index = {QualityTabIndex}");
                    }

                    if (dumpTabNames.Contains(name))
                    {
                        DumpTabDictionary[i] = name;
                        dumpTabNames.Remove(name);
                    }

                    if (Done())
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"HTTP error getting tab list: {ex.Message}");
            }
            if (RecipeTabIndex < 0)
            {
                logger.Error("ERROR: stash tab not found");
                return false;
            }
            return true;
        }

        public static Task<JsonElement> GetJsonForUrl(string theUrl, int tabIndex)
        {
            return GetJsonForUrl(theUrl, tabIndex.ToString());
        }

        public static async Task<JsonElement> GetJsonForUrl(string theUrl, string tabName)
        {
            var fileName = TabFileName(tabName);
            if (StashReadMode == StashReading.Playback && File.Exists(fileName))
            {
                var options = new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                };
                try
                {
                    return JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(fileName), options);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Reading save tab data for '{tabName}'");
                }
            }

            if (StashReadMode != StashReading.Manual)
            {
                try
                {
                    var result = await HttpClient.GetFromJsonAsync<JsonElement>(theUrl);
                    MaybeSavePageJson(result, tabName);
                    return result;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Getting URL '{theUrl}'");
                }
            }

            return await ManualGetUrlJson(theUrl);
        }

        private static string TabFileName(string tabName)
        {
            return Path.Combine(exePath, $"json_{tabName}.json");
        }

        public static void MaybeSavePageJson(JsonElement result, string tabName)
        {

            if (StashReadMode == StashReading.Record)
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                };
                var jsonString = JsonSerializer.Serialize(result, options);
                var fileName = TabFileName(tabName);
                File.WriteAllText(fileName, jsonString);
            }
        }

        private static async Task<JsonElement> ManualGetUrlJson(string theUrl)
        {
            await WindowsClipboard.SetTextAsync(theUrl, default);
            Console.WriteLine();
            Console.WriteLine("Go to the following URL in your browser and then copy the results to the clipboard:");
            Console.WriteLine();
            Console.WriteLine(theUrl);
            Console.WriteLine();
            await Task.Delay(3000);
            var text = await WindowsClipboard.GetTextAsync(default);
            while (text == null || text == theUrl)
            {
                await Task.Delay(1000);
                text = await WindowsClipboard.GetTextAsync(default);
            }

            text = text.Trim();
            if (text.StartsWith("{") && text.EndsWith("}"))
                return JsonSerializer.Deserialize<JsonElement>(text); ;
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }

        public static void SetProcessModule(string processModulePath)
        {
            if (!forcingClientTxt)
            {
                var path = Path.GetDirectoryName(processModulePath);
                var clientFile = Path.Combine(path, "logs\\Client.txt");
                if (File.Exists(clientFile))
                    ClientFileName = clientFile;
            }
        }
    }
}
