﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChaosHelper
{
    class Config
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private static RawJsonConfiguration rawConfig;

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
        public static int TabIndex { get; private set; }
        public static bool IsQuadTab { get; private set; }
        public static bool AllowIDedSets { get; private set; }
        public static int ChaosParanoiaLevel { get; private set; }
        public static string IgnoreMaxSets { get; private set; }
        public static string IgnoreMaxIlvl { get; private set; }
        public static bool IncludeInventoryOnForce { get; private set; }
        public static List<string> TownZones { get; private set; }
        public static List<int> HighlightColors { get; private set; }
        public static string FilterColor { get; private set; }
        public static string FilterMarker { get; private set; }
        public static string AreaEnteredPattern { get; private set; }
        public static int CurrencyTabIndex { get; set; } = -1;
        public static HotKeyBinding HighlightItemsHotkey { get; private set; }
        public static HotKeyBinding ShowJunkItemsHotkey { get; private set; }
        public static HotKeyBinding ForceUpdateHotkey { get; private set; }
        public static HotKeyBinding CharacterCheckHotkey { get; private set; }
        public static HotKeyBinding TestModeHotkey { get; private set; }
        public static bool ShouldHookMouseEvents { get; private set; }
        public static string RequiredProcessName { get; private set; }
        public static System.Drawing.Rectangle StashPageXYWH { get; private set; }
        public static int StashPageVerticalOffset { get; private set; }

        public static int Ilvl60 { get; private set; } = 60;

        private static void SetLeague(string s)
        {
            League = s.Replace(' ', '+');
        }

        public static bool IsTown(string newArea)
        {
            bool isTown = TownZones == null || TownZones.Count == 0
                || TownZones.Any(x => string.Equals(x, newArea, StringComparison.OrdinalIgnoreCase));
            return isTown;
        }

        public static bool HaveAHotKey()
        {
            var haveAHotKey = HighlightItemsHotkey != null
                || ShowJunkItemsHotkey != null
                || ForceUpdateHotkey != null
                || CharacterCheckHotkey != null
                || TestModeHotkey != null;
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

        public static bool IsTestModeHotkey(ConsoleHotKey.HotKeyEventArgs e)
        {
            return HotKeyMatches(TestModeHotkey, e);
        }

        public static bool LimitIlvl(ItemClass c)
        {
            var limitIlvl = MaxIlvl > 60 && MaxIlvl < 100 && IgnoreMaxIlvl.IndexOf(c.CategoryStr, StringComparison.OrdinalIgnoreCase) < 0;
            return limitIlvl;
        }
        
        public static async Task<bool> ReadConfigFile()
        {
            string exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var configFile = Path.Combine(exePath, "./settings.jsonc");
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

            FilterUpdateSound = Path.Combine(exePath, "./FilterUpdateSound.wav");
            var filterUpdateVolumeInt = rawConfig.GetInt("soundFileVolume", 50).Clamp(0, 100);
            FilterUpdateVolume = filterUpdateVolumeInt / 100.0f;

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

            if (!await CheckAccount())
                return false;

            MaxSets = rawConfig.GetInt("maxSets", 12);
            MaxIlvl = rawConfig.GetInt("maxIlvl", -1);
            AllowIDedSets = rawConfig.GetBoolean("allowIDedSets", false);
            ChaosParanoiaLevel = rawConfig.GetInt("chaosParanoiaLevel", 0);
            IgnoreMaxSets = rawConfig["ignoreMaxSets"];
            IgnoreMaxIlvl = rawConfig["ignoreMaxIlvl"];
            IncludeInventoryOnForce = rawConfig.GetBoolean("includeInventoryOnForce", false);
            TownZones = rawConfig.GetStringList("townZones");

            AreaEnteredPattern = rawConfig["areaEnteredPattern "];
            if (string.IsNullOrWhiteSpace(AreaEnteredPattern))
                AreaEnteredPattern = "] : You have entered ";

            Currency.SetArray(rawConfig.GetArray("currency"));

            HighlightItemsHotkey = rawConfig.GetHotKey("highlightItemsHotkey");
            ShowJunkItemsHotkey = rawConfig.GetHotKey("showJunkItemsHotkey");
            ForceUpdateHotkey = rawConfig.GetHotKey("forceUpdateHotkey");
            CharacterCheckHotkey = rawConfig.GetHotKey("characterCheckHotkey");
            TestModeHotkey = rawConfig.GetHotKey("testModeHotkey");
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
                FilterMarker = "section displays 20% quality rares";
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

            var clientTxtBase = rawConfig["clientTxt"];
            ClientFileName = Environment.ExpandEnvironmentVariables(clientTxtBase);
            if (!File.Exists(ClientFileName))
                ClientFileName = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\Games\\Grinding Gear Games\\Path of Exile\\logs\\Client.txt");
            if (!File.Exists(ClientFileName))
                ClientFileName = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\Grinding Gear Games\\Path of Exile\\logs\\Client.txt");
            if (!File.Exists(ClientFileName))
                ClientFileName = Environment.ExpandEnvironmentVariables("%ProgramFiles%\\Grinding Gear Games\\Path of Exile\\logs\\Client.txt");
            if (!File.Exists(ClientFileName))
                ClientFileName = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\Steam\\steamapps\\common\\Path of Exile\\logs\\Client.txt");
            if (!File.Exists(ClientFileName))
            {
                logger.Error($"ERROR: client.txt file '{clientTxtBase}' not found");
                return false;
            }

            if (!await DetermineTabIndex())
                return false;

            return true;
        }

        public static async Task<bool> CheckAccount(bool forceWebCheck = false)
        {
            try
            {
                SetLeague(rawConfig["league"]);
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
                        SetLeague(leagueElement.GetString());

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

        public static async Task<bool> DetermineTabIndex(bool forceWebCheck = false)
        {
            // see if we have configured a tab index
            //
            if (!forceWebCheck)
            {
                TabIndex = rawConfig.GetInt("tabIndex", -1);
                if (TabIndex >= 0)
                {
                    logger.Info($"using configured tab index = {TabIndex}");
                    IsQuadTab = rawConfig.GetBoolean("isQuadTab", true);
                    return true;
                }
            }

            TabIndex = -1;
            CurrencyTabIndex = -1;

            var tabNameFromConfig = rawConfig["tabName"];
            var checkTabNames = !string.IsNullOrWhiteSpace(tabNameFromConfig);

            try
            {
                var stashTabListUrl = System.Uri.EscapeUriString("https://www.pathofexile.com/character-window/get-stash-items"
                    + $"?league={League}&tabs=1&accountName={Account}");
                var json = await HttpClient.GetFromJsonAsync<JsonElement>(stashTabListUrl);

                var lookForCurrencyTab = Currency.CurrencyList.Any();

                const string removeOnlyTabPattern = "remove-only";

                foreach (var tab in json.GetProperty("tabs").EnumerateArray())
                {
                    var name = tab.GetProperty("n").GetString();
                    var isRemoveOnly = name.Contains(removeOnlyTabPattern, StringComparison.OrdinalIgnoreCase);
                    var tabType = tab.GetProperty("type").GetString();
                    var i = tab.GetIntOrDefault("i", -1);
                    bool found = false;

                    if (TabIndex == -1)
                    {
                        if (checkTabNames)
                        {
                            found = string.Equals(name, tabNameFromConfig, StringComparison.OrdinalIgnoreCase);
                        }
                        else if (!isRemoveOnly)
                        {
                            found = string.Equals(tabType, "QuadStash", StringComparison.OrdinalIgnoreCase);
                        }
                        if (found)
                        {
                            TabIndex = i;
                            logger.Info($"found tab '{name}', index = {TabIndex}, type = {tabType}");
                            IsQuadTab = string.Equals(tabType, "QuadStash", StringComparison.OrdinalIgnoreCase);
                            if (!lookForCurrencyTab || CurrencyTabIndex >= 0)
                                break;
                        }
                    }

                    if (lookForCurrencyTab && CurrencyTabIndex == -1 && !isRemoveOnly)
                    {
                        found = string.Equals(tabType, "CurrencyStash", StringComparison.OrdinalIgnoreCase);
                        if (found)
                        {
                            CurrencyTabIndex = i;
                            logger.Info($"found currency tab '{name}', index = {CurrencyTabIndex}");
                            if (TabIndex >= 0)
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"HTTP error getting tab list: {ex.Message}");
            }
            if (TabIndex < 0)
            {
                logger.Error("ERROR: stash tab not found");
                return false;
            }
            return true;
        }
    }
}
