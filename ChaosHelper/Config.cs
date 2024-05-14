using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChaosHelper
{
    class Config
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private static RawJsonConfiguration rawConfig;
        private static int _poeProcessId = 0;
        private static string _closePortsArg = null;
        private static string _exePath;

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

        public static bool OfflineMode { get; set; } = false;
        public static HttpClient HttpClient { get; private set; }
        public static string Account { get; private set; }
        public static string Poesessid { get; private set; }
        public static string League { get; private set; }
        public static string Character { get; private set; }
        public static string SourceFilterName { get; private set; }
        public static string FilterFileName { get; private set; }
        public static string FilterFileBaseName { get; private set; }
        public static string InitialImportFile { get; private set; }

        public static string ClientFileName { get; private set; }
        public static string FilterUpdateSound { get; private set; }
        public static float FilterUpdateVolume { get; private set; }
        public static bool FilterAutoReload { get; private set; }
        public static int MaxSets { get; private set; }
        public static int MaxIlvl { get; private set; }
        public static int MinIlvl { get; private set; }
        public static int RecipeTabIndex { get; private set; }
        public static bool IsQuadTab { get; private set; }
        public static bool QualityIsQuadTab { get; private set; }
        public static int QualityFlaskRecipeSlop { get; private set; }
        public static int QualityGemMapRecipeSlop { get; private set; }
        public static int QualityScrapRecipeSlop { get; private set; }
        public static Dictionary<int, string> DumpTabDictionary { get; private set; } = [];
        public static bool AllowIDedSets { get; private set; }
        public static int ChaosParanoiaLevel { get; private set; }
        public static string IgnoreMaxSets { get; private set; }
        public static string IgnoreMaxSetsUnder75 { get; private set; }
        public static string IgnoreMaxIlvl { get; private set; }
        public static List<string> TownZones { get; private set; }
        public static List<int> HighlightColors { get; private set; }
        public static ItemDisplay FilterDisplay { get; private set; }
        public static string AreaEnteredPattern { get; private set; }
        public static List<HotkeyEntry> Hotkeys { get; private set; }
        public static HotkeyEntry ClosePortsHotkey { get; private set; }
        public static bool ShouldHookMouseEvents { get; private set; }
        public static string RequiredProcessName { get; private set; }
        public static System.Drawing.Rectangle StashPageXYWH { get; private set; }
        public static int StashPageVerticalOffset { get; private set; }
        public static StashReading StashReadMode { get; private set; }
        public static bool StashCanDoManualRead { get; private set; }
        public static bool ForceSteam { get; set; }
        public static int CurrencyTabIndex { get; set; } = -1;
        public static int QualityTabIndex { get; set; } = -1;

        public static double DefenseVariance { get; private set; }
        public static bool ShowMinimumCurrency { get; private set; }
        public static string ClosePortsForPidPath { get; private set; }
        public static bool RunningAsAdmin { get; private set; }

        private static bool _exitWhenPoeExits; 
        public static bool ExitWhenPoeExits { get { return _exitWhenPoeExits || ForceExitWhenPoeExits; } }
        public static bool ForceExitWhenPoeExits { get; set; }

        public static bool StartPaused { get; private set; }
        public static string ConfigDir { get; set; }
        public static string LastWhisper {  get; set; }

        public static bool IsTown(string newArea)
        {
            bool isTown = TownZones == null || TownZones.Count == 0
                || TownZones.Any(x => string.Equals(x, newArea, StringComparison.OrdinalIgnoreCase));
            return isTown;
        }

        public static bool HaveAHotKey()
        {
            return Hotkeys.Count > 0;
        }

        public static HotkeyEntry GetHotKey(ConsoleHotKey.HotKeyEventArgs e)
        {
            return Hotkeys.FirstOrDefault(x => x.Enabled && x.Matches(e));
        }

        public static bool IsClosePortsHotkey(ConsoleHotKey.HotKeyEventArgs e)
        {
            return ClosePortsHotkey != null && ClosePortsHotkey.Matches(e);
        }

        public static async Task<bool> ReadConfigFile()
        {
            if (OperatingSystem.IsWindows())
            {
                var principal = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent());
                RunningAsAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            else
                RunningAsAdmin = false;

            string configFile = GetConfigFilePath("settings.jsonc");
            if (!File.Exists(configFile))
            {
                logger.Error("ERROR: config file 'settings.jsonc' not found");
                return false;
            }
            logger.Info($"reading config file {configFile}");

            try
            {
                rawConfig = new RawJsonConfiguration(configFile);
            }
            catch (Exception ex)
            {
                logger.Error($"ERROR cannot read config file: {ex.Message}");
                return false;
            }

            var poeDocDir = Environment.ExpandEnvironmentVariables($"%USERPROFILE%\\Documents\\My Games\\Path of Exile\\");
            if (!Directory.Exists(poeDocDir))
                poeDocDir = Environment.ExpandEnvironmentVariables($"%USERPROFILE%\\OneDrive\\Documents\\My Games\\Path of Exile\\");

            Account = rawConfig["account"];
            if (string.IsNullOrWhiteSpace(Account))
            {
                logger.Error("ERROR: account not configured in settings.jsonc");
                return false;
            }

            Poesessid = rawConfig["poesessid"];
            if (string.IsNullOrWhiteSpace(Poesessid))
            {
                logger.Error("ERROR: poesessid not configured in settings.jsonc");
                return false;
            }

            var sourceFilterBase = rawConfig["sourceFilter"];
            if (string.IsNullOrWhiteSpace(sourceFilterBase))
            {
                logger.Error($"ERROR: sourceFilter not configured in settings.jsonc");
                return false;
            }

            SourceFilterName = Path.Combine(poeDocDir, sourceFilterBase);
            if (!File.Exists(SourceFilterName))
                SourceFilterName = Path.ChangeExtension(SourceFilterName, "filter");
            if (!File.Exists(SourceFilterName))
            {
                logger.Error($"ERROR: source filter file '{sourceFilterBase}' not found");
                return false;
            }
            SourceFilterName = Path.GetFileName(SourceFilterName);
            logger.Info($"using '{SourceFilterName}' as source filter");

            var filterBase = rawConfig["filter"];
            if (string.IsNullOrWhiteSpace(filterBase))
                filterBase = "ChaosHelper";
            FilterFileName = Path.ChangeExtension(Path.Combine(poeDocDir, filterBase), "filter");
            logger.Info($"will write filter file '{FilterFileName}'");
            FilterFileBaseName = Path.GetFileNameWithoutExtension(FilterFileName);

            if (string.Equals(SourceFilterName, Path.GetFileName(FilterFileName), StringComparison.OrdinalIgnoreCase))
            {
                logger.Error("ERROR: source filter file and generated filter file must have different names");
                return false;
            }

            InitialImportFile = rawConfig["initialImport"];
            if (!string.IsNullOrWhiteSpace(InitialImportFile))
                InitialImportFile = Path.ChangeExtension(InitialImportFile, "filter");

            CreateNewHttpClient();

            var stashReadModeStr = rawConfig["stashReadMode"];
            if (Enum.TryParse<StashReading>(stashReadModeStr, true, out var stashReadMode)
                && Enum.IsDefined(typeof(StashReading), stashReadMode))
                StashReadMode = stashReadMode;
            else
                StashReadMode = StashReading.Normal;
            StashCanDoManualRead = StashReadMode == StashReading.Manual || rawConfig.GetBoolean("stashCanDoManualRead", true);

            if (!OfflineMode && !await CheckAccount())
                return false;

            FilterUpdateSound = GetConfigFilePath("FilterUpdateSound.wav");
            var filterUpdateVolumeInt = rawConfig.GetInt("soundFileVolume", 50).Clamp(0, 100);
            FilterUpdateVolume = filterUpdateVolumeInt / 100.0f;

            FilterAutoReload = rawConfig.GetBoolean("filterAutoReload", false);

            _exitWhenPoeExits = rawConfig.GetBoolean("exitWhenPoeExits", false);
            StartPaused = rawConfig.GetBoolean("startPaused", false);

            MaxSets = rawConfig.GetInt("maxSets", 12);
            MaxIlvl = rawConfig.GetInt("maxIlvl", -1);
            MinIlvl = rawConfig.GetInt("minIlvl", 60);
            AllowIDedSets = rawConfig.GetBoolean("allowIDedSets", true);
            ChaosParanoiaLevel = rawConfig.GetInt("chaosParanoiaLevel", 0);
            IgnoreMaxSets = rawConfig["ignoreMaxSets"];
            IgnoreMaxSetsUnder75 = rawConfig["ignoreMaxSetsUnder75"];
            IgnoreMaxIlvl = rawConfig["ignoreMaxIlvl"];
            var sortOrderStr = rawConfig["itemSortOrder"];
            if (Enum.TryParse<ItemPosition.SortBy>(sortOrderStr, true, out var sortOrder)
                && Enum.IsDefined(typeof(ItemPosition.SortBy), sortOrder))
                ItemPosition.SortOrder = sortOrder;
            else
                ItemPosition.SortOrder = MinIlvl < 60 ? ItemPosition.SortBy.IlvlBottomFirst : ItemPosition.SortBy.Default;

            TownZones = rawConfig.GetStringList("townZones");

            AreaEnteredPattern = rawConfig["areaEnteredPattern "];
            if (string.IsNullOrWhiteSpace(AreaEnteredPattern))
                AreaEnteredPattern = "] : You have entered ";

            ShowMinimumCurrency = rawConfig.GetBoolean("showMinimumCurrency", false);
            Currency.SetArray(rawConfig.GetArray("currency"));

            Hotkeys = rawConfig.GetHotkeys();

            ClosePortsHotkey = Hotkeys.FirstOrDefault(x => x.CommandIs(Constants.ClosePorts));
            if (ClosePortsHotkey != null)
            {
                if (!RunningAsAdmin)
                {
                    logger.Warn("Not running as admin - cannot close ports");
                    ClosePortsForPidPath = null;
                    ClosePortsHotkey = null;
                }
                else
                {
                    ClosePortsForPidPath = GetConfigFilePath("ClosePortsForPid.exe");
                    if (!File.Exists(ClosePortsForPidPath))
                    {
                        logger.Warn("ClosePortsForPid.exe not found - cannot close ports");
                        ClosePortsForPidPath = null;
                        ClosePortsHotkey = null;
                    }
                }
                if (ClosePortsHotkey == null)
                    Hotkeys = Hotkeys.Where(x => !x.CommandIs(Constants.ClosePorts)).ToList();
            }

            ShouldHookMouseEvents = rawConfig.GetBoolean("hookMouseEvents", false);
            RequiredProcessName = rawConfig["processName"];
            StashPageXYWH = rawConfig.GetRectangle("stashPageXYWH");
            StashPageVerticalOffset = rawConfig.GetInt("stashPageVerticalOffset", 0);

            HighlightColors = rawConfig.GetColorList("highlightColors");
            if (HighlightColors.Count == 0)
            {
                HighlightColors = [0xffffff, 0xffff00, 0x00ff00, 0x6060ff];
            }
            else if (HighlightColors.Count != 4)
            {
                logger.Error("ERROR: highlightColors must be empty or exactly 4 numbers");
                return false;
            }

            FilterDisplay = null;
            if (rawConfig.TryGetProperty("filterDisplay", out var filterDisplayElement))
                FilterDisplay = ItemDisplay.Parse(filterDisplayElement);
            FilterDisplay ??= new ItemDisplay
            {
                FontSize = 0,
                TextColor = "106 77 255",
                BackGroundColor = "70 70 70",
                BorderColor = "106 77 255",
            };

            var itemModFile = GetConfigFilePath("itemMods.csv");
            if (File.Exists(itemModFile))
            {
                ItemMod.ReadItemModFile(itemModFile);
                logger.Info($"item mod file - there are {ItemMod.PossibleMods.Count} mods");
            }

            var itemRuleFile = GetConfigFilePath("itemRules.csv");
            if (File.Exists(itemRuleFile))
            {
                ItemRule.ReadRuleFile(itemRuleFile);
                logger.Info($"item rule file - there are {ItemRule.Rules.Count} rules");
            }

            if (!OfflineMode && !await DetermineTabIndicies())
                return false;

            Helpers.ReadBaseItemsJson();

            return true;
        }

        public static void CreateNewHttpClient()
        {
            var cookieContainer = new CookieContainer();
            cookieContainer.Add(new Cookie("POESESSID", Poesessid, "/", "pathofexile.com"));

            var handler = new HttpClientHandler() { CookieContainer = cookieContainer };
            var client = new HttpClient(handler);

            var productValue = new ProductInfoHeaderValue("ChaosHelper", "1.0");
            var commentValue = new ProductInfoHeaderValue("(+https://github.com/TimothyByrd/ChaosHelper)");
            client.DefaultRequestHeaders.UserAgent.Add(productValue);
            client.DefaultRequestHeaders.UserAgent.Add(commentValue);

            HttpClient = client;
        }

        private static string GetConfigFilePath(string filename)
        {
            if (!string.IsNullOrWhiteSpace(ConfigDir))
            {
                var result = Path.Combine(ConfigDir, filename);
                if (File.Exists(result))
                    return result;
            }

            if (string.IsNullOrWhiteSpace(_exePath))
                _exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            return Path.Combine(_exePath, filename);
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
                JsonElement tablist = await GetTabList();

                var lookForCurrencyTab = Currency.CurrencyList.Count != 0;
                var lookForQualityTab = !string.IsNullOrWhiteSpace(qualityTabNameFromConfig);

                const string removeOnlyTabPattern = "remove-only";

                bool Done()
                {
                    return RecipeTabIndex >= 0
                        && (!lookForCurrencyTab || CurrencyTabIndex >= 0)
                        && (!lookForQualityTab || QualityTabIndex >= 0)
                        && dumpTabNames.Count == 0;
                }

                foreach (var tab in tablist.GetProperty("tabs").EnumerateArray())
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
                        QualityScrapRecipeSlop = rawConfig.GetInt("qualityScrapRecipeSlop");
                        if (QualityScrapRecipeSlop >= 40)
                            QualityScrapRecipeSlop -= 40;
                        QualityFlaskRecipeSlop = rawConfig.GetInt("qualityFlaskRecipeSlop");
                        if (QualityFlaskRecipeSlop >= 40)
                            QualityFlaskRecipeSlop -= 40;
                        QualityGemMapRecipeSlop = rawConfig.GetInt("qualityGemMapRecipeSlop");
                        if (QualityGemMapRecipeSlop >= 40)
                            QualityGemMapRecipeSlop -= 40;
                        logger.Info($"found quality tab '{name}', index = {QualityTabIndex}, gem/map slop {QualityGemMapRecipeSlop}, flask slop = {QualityFlaskRecipeSlop}, scrap slop = {QualityScrapRecipeSlop}");
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

        public static Task<JsonElement> GetTabList()
        {
            return GetTabList(League);
        }

        public static async Task<JsonElement> GetTabList(string league)
        {
            var stashTabListUrl = "https://www.pathofexile.com/character-window/get-stash-items"
                + $"?league={Uri.EscapeDataString(league)}&tabs=1&accountName={Uri.EscapeDataString(Account)}&realm=pc&tabIndex=0";
            JsonElement json = await GetJsonForUrl(stashTabListUrl, "tabList");
            return json;
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
                try
                {
                    return JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(fileName), RawJsonConfiguration.SerializerOptions);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Reading save tab data for '{tabName}'");
                }
            }

            if (StashReadMode != StashReading.Manual)
            {
                var baseDelaySeconds = 60 * 10;
                var triesSoFar = 0;
                while (triesSoFar < 4)
                {
                    ++triesSoFar;
                    try
                    {
                        HttpResponseMessage response = await HttpClient.GetAsync(theUrl);
                        RateLimit.UpdateLimits(response);
                        if (response.StatusCode == HttpStatusCode.TooManyRequests)
                        {
                            foreach (var header in response.Headers)
                            {
                                if (header.Key.StartsWith("X-Rate", StringComparison.OrdinalIgnoreCase)
                                    || header.Key.StartsWith("Retry", StringComparison.OrdinalIgnoreCase))
                                {
                                    var value = string.Join(";", header.Value);
                                    logger.Info($"Header {header.Key}: {value}");
                                }
                            }
                            var delta = TimeSpan.Zero;
                            if (response.Headers.RetryAfter.Date.HasValue)
                                delta = response.Headers.RetryAfter.Date.Value - DateTimeOffset.Now;
                            if (delta <= TimeSpan.Zero && response.Headers.RetryAfter.Delta.HasValue)
                                delta = response.Headers.RetryAfter.Delta.Value;
                            if (delta <= TimeSpan.Zero)
                            {
                                var delaySeconds = baseDelaySeconds * triesSoFar;
                                delta = TimeSpan.FromSeconds(delaySeconds);
                            }

                            delta = delta.Add(TimeSpan.FromSeconds(30));
                            logger.Warn($"Got response 429 too many requests(1) - delaying for {delta.TotalSeconds} seconds");
                            await Task.Delay(delta);
                            continue;
                        }
                        else if (!response.IsSuccessStatusCode)
                        {
                            logger.Error($"Got response {response.StatusCode}");
                        }
                        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                        MaybeSavePageJson(result, tabName);
                        return result;
                    }
                    catch (HttpRequestException httpException)
                    {
                        logger.Error(httpException, $"Getting URL '{theUrl}'");
                        if (httpException.StatusCode == HttpStatusCode.TooManyRequests)
                        {
                            var delaySeconds = baseDelaySeconds * triesSoFar;
                            logger.Warn($"Got response 429 too many requests(2) - delaying for {delaySeconds} seconds");
                            await Task.Delay(delaySeconds * 1000);

                        }
                        else
                            break;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"Getting URL '{theUrl}'");
                        break;
                    }
                }
            }

            if (StashCanDoManualRead)
                return await ManualGetUrlJson(theUrl);
            return new JsonElement();
        }

        public static string TabFileName(string tabName)
        {
            return GetConfigFilePath($"json_{tabName}.json");
        }

        public static void MaybeSavePageJson(JsonElement json, string tabName)
        {

            if (StashReadMode == StashReading.Record)
            {
                var jsonString = JsonSerializer.Serialize(json, RawJsonConfiguration.SerializerOptions);
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
            if (text.StartsWith('{') && text.EndsWith('}'))
                return JsonSerializer.Deserialize<JsonElement>(text); ;
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }

        public static void SetProcessModule(string processModulePath, int poeProcessId)
        {
            var path = Path.GetDirectoryName(processModulePath) ?? string.Empty;
            var clientFile = Path.Combine(path, "logs\\Client.txt");
            if (File.Exists(clientFile))
                ClientFileName = clientFile;
            else
                ClientFileName = string.Empty;

            _poeProcessId = poeProcessId;
            GetCloseTcpPortsArgument();
        }

        public static void SetPoeDir(string poeDir)
        {
            ClientFileName = string.Empty;
            if (Directory.Exists(poeDir))
            {
                var clientFile = Path.Combine(poeDir, "logs\\Client.txt");
                if (File.Exists(clientFile))
                    ClientFileName = clientFile;
            }
        }

        public static void GetCloseTcpPortsArgument()
        {
            // pre-compute the arguments to speed up closing the ports when the hotkey is pressed
            //
            if (_poeProcessId <= 0 || string.IsNullOrEmpty(ClosePortsForPidPath))
                return;
            try
            {
                var oldArg = _closePortsArg;
                var arguments = $"{_poeProcessId} print args";
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ClosePortsForPidPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                     CreateNoWindow = true,

                };
                using var process = System.Diagnostics.Process.Start(processStartInfo);
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0 || output.StartsWith('*'))
                    logger.Error($"Running ClosePortsForPid.exe to get arguments did not succeed: {output}");
                else
                {
                    _closePortsArg = $"{output.Trim()} {_poeProcessId}";
                    if (!string.Equals(_closePortsArg, oldArg))
                        logger.Info($"ClosePortsForPid argument is '{_closePortsArg}'");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Running ClosePortsForPid.exe to get arguments");
            }
        }

        public static void CloseTcpPorts()
        {
            if (_poeProcessId <= 0 || string.IsNullOrEmpty(ClosePortsForPidPath) || string.IsNullOrEmpty(_closePortsArg))
            {
                logger.Warn("Could not run ClosePortsForPid");
                return;
            }
            var arguments = _closePortsArg ?? $"{_poeProcessId}";
            _closePortsArg = null;
            try
            {
                using var exeProcess = System.Diagnostics.Process.Start(ClosePortsForPidPath, arguments);
                exeProcess.WaitForExit();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Running ClosePortsForPid.exe");
            }
        }
    }

    static class Constants
    {
        public const string ClosePorts = "closePorts";
        public const string HighlightItems = "highlightItems";
        public const string ShowQualityItems = "showQualityItems";
        public const string ShowJunkItems = "showJunkItems";
        public const string ForceUpdate = "forceUpdate";
        public const string CharacterCheck = "characterCheck";
        public const string TestPattern = "testPattern";
    }
}
