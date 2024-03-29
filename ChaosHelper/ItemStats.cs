﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChaosHelper
{

    /*
    From Theros:

    0. Does it have the pseudo-mandatory requirements for the slot? This is stuff
    like "can it be 6-linked as a chest", "does it have some form of movespeed as
    boots" - the things you need for the item to be worth considering in the first
    place. Stuff without this can sometimes sell, but it's rare.

    1. Does it have enough survivability? This varies for the base - bodies get
    more than helms get more than gloves get more than boots get more than belts
    get more than rings/jewelery. You want either high ES, high life, or in niche
    scenarios, both; honorable mention for high mana when paired with life or
    niche survivability mods like +1 endurance charge. Items with a LOT of this
    can sell on their own.

    2. Does it have a big draw? Big draws tend to vary - it can be a lot of res,
    it can be a lot of damage, it can be a nice mix of the two, or it can be
    something special with a lot of utility. If it doesn't have a metric ton
    of survivability, it needs a big draw.

    3. Does it have mod clogs? This is stuff like life/mana leech or flat life
    regen where it's usually not valuable, but can be risky to try to remove to
    make space for better mods. Mod clogs make the item worth less - removing
    them with Harvest makes the item better, but that only really works if the
    draw of the item is big enough.
    */
    public class ItemStats
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public class TagValue
        {
            public string Tag;
            public double Value;
            public bool Fractured;
        }

        readonly SortedDictionary<string, TagValue> tagValues = new();

        public (double, bool) GetTag(string tag)
        {
            var lower = tag.ToLower();
            if (tagValues.TryGetValue(lower, out var value))
                return (value.Value, value.Fractured);
            return (0, false);
        }

        // Define the indexer to allow client code to use [] notation.
        public double this[string s]
        {
            get { return V(s); }
        }

        double V(string tag)
        {
            var lower = tag.ToLower();
            return tagValues.ContainsKey(lower) ? tagValues[lower].Value : 0;
        }

        void AddToTag(string tag, double value, bool fractured = false)
        {
            var lower = tag.ToLower();
            if (tagValues.ContainsKey(lower))
            {
                tagValues[lower].Value = tagValues[lower].Value + value;
                if (fractured)
                    tagValues[lower].Fractured = true;
            }
            else
                tagValues[lower] = new TagValue { Tag = tag, Value = value, Fractured = fractured };
        }

        public void CheckMod(string s, bool fractured = false)
        {
            var found = false;
            foreach (var itemMod in ItemMod.PossibleMods)
            {
                double value;
                (found, value) = itemMod.TryMatch(s);
                if (found)
                {
                    foreach (var tag in itemMod.Tags)
                    {
                        var v = tag.Multiplier > 0 ? value * tag.Multiplier : 1.0;
                        AddToTag(tag.Tag, v, fractured);
                    }
                    break;
                }
            }

            if (!found)
            {
                var x = Regex.Replace(s, @"\d+\.?\d*", "1").Replace("\r", "\\r").Replace("\n", "\\n");
                logger.Warn($"*** mod not in ItemMods.csv: {x}");
            }
        }

        public Cat Cat { get; set; }
        public BaseClass BaseClass { get; set; }
        public string BaseType { get; set; }

        public ItemStats(Cat cat, string baseType)
        {
            Cat = cat;
            BaseClass = Cat.ToBaseClass();
            BaseType = baseType;
        }

        public string GetValueMessage()
        {
            var msg = string.Join(", ", ItemRule.Rules.Where(x => x.Matches(this)).Select(x => x.Name));
            return string.IsNullOrEmpty(msg) ? null : $"rules matched: {msg}";

            //var (val, frac) = GetTag("MoveSpeed");

            //if (Cat == Cat.Boots && val < 25) return null;

            //if (val >= 25 && frac)
            //    return $"fractured move speed: {val}";
        }

        readonly string[] modCats = new string[]
        {
            "implicitMods",
            "explicitMods",
            "craftedMods",
            "enchantMods",
            "fracturedMods",
        };

        public void CheckMods(ItemPosition item)
        {
            var json = (JsonElement)item.JsonElement;
            //var frameType = json.GetIntOrDefault("frameType", 0);
            //var identified = json.GetProperty("identified").GetBoolean();

            AddToTag("ilvl", item.iLvl);

            if (BaseClass == BaseClass.Any)
            {
                BaseClass = json.JsonToBaseClass();
            }

            //if ((frameType != 2 /*rare*/ && frameType != 1 /*magic*/) || !identified) return;

            foreach (var modCat in modCats)
            {
                var fractured = modCat.StartsWith("fract");
                var theArray = json.GetArray(modCat);
                while (theArray.MoveNext())
                    CheckMod(theArray.Current.GetString(), fractured);
            }

            var veiledArray = json.GetArray("veiledMods");
            while (veiledArray.MoveNext())
            {
                AddToTag("Veiled", 1);
                // Technically Prefix01 vs, Suffix03 should tell us something.
                AddToTag(veiledArray.Current.GetString(), 1);
            }

            CheckProperties(json);
        }

        private static readonly char[] lineSplits = "\r\n".ToCharArray();


        public async Task CheckFromClipboard()
        {
            var text = await WindowsClipboard.GetTextAsync(default);
            for (int i = 0; i < 10 && text == null; ++i)
            {
                await Task.Delay(1000);
                text = await WindowsClipboard.GetTextAsync(default);
            }

            var splits = text.Split(lineSplits, StringSplitOptions.RemoveEmptyEntries);
            var seenDash = false;
            foreach (var split in splits)
            {
                var fractured = split.Contains("(fractured)");
                var line = split.Replace("(implicit)", "").Replace("(crafted)", "").Replace("(fractured)", "").Trim();
                if (line.StartsWith("Item Class:", StringComparison.OrdinalIgnoreCase))
                {
                    var classStr = line.Substring(11).Trim();
                    if (string.Equals(classStr, "Jewel", StringComparison.OrdinalIgnoreCase))
                        BaseClass = BaseClass.Jewel;
                    else
                        BaseClass = classStr.ToBaseClass();
                }
                else if (line.StartsWith("Item Level:", StringComparison.OrdinalIgnoreCase))
                    AddToTag("ilvl", int.Parse(line.Substring(11).Trim()));
                else if (line.StartsWith("-"))
                {
                    seenDash = true;
                    continue;
                }
                else if (!seenDash || line.Contains(':'))
                    continue;
                else
                    CheckMod(line, fractured);
            }
        }

        public void DumpValues()
        {
            Console.WriteLine();
            Console.WriteLine($"class = {BaseClass}");
            if (!string.IsNullOrEmpty(BaseType))
                Console.WriteLine($"base = {BaseType}");
            var defenseTypes = GetDefenseTypes();
            if (defenseTypes.Any())
                Console.WriteLine($"def = {string.Join(", ", defenseTypes)}");
            foreach (var tagValue in tagValues.Values)
            {
                Console.WriteLine($"\t{tagValue.Tag}: {tagValue.Value}");
            }
            var msg = GetValueMessage();
            if (string.IsNullOrWhiteSpace(msg))
                Console.WriteLine("not interesting");
            else
                Console.WriteLine(msg);
            Console.WriteLine();
        }


        const double _esMult = 5.0;

        public List<string> GetDefenseTypes()
        {
            double fudge = Config.DefenseVariance;

            var propArm = V("PropArm");
            var propEva = V("PropEva");
            var propES = V("PropES") * _esMult;
            var propWard = V("PropWard");
            var defenseTypes = new List<string>();
            if (propArm > 0 && propArm > propEva * fudge && propArm > propES * fudge)
                defenseTypes.Add("PropArm");
            if (propEva > 0 && propEva > propArm * fudge && propEva > propES * fudge)
                defenseTypes.Add("PropEva");
            if (propES > 0 && propES > propArm * fudge && propES > propEva * fudge)
                defenseTypes.Add("PropES");
            if (propWard > 0)
                defenseTypes.Add("PropWard");
            return defenseTypes;
        }
        
        public double GetDefense(List<string> defenseTypes)
        {
            var result = 0.0;
            if (defenseTypes != null)
            {
                foreach (var dt in defenseTypes)
                {
                    var mult = string.Equals(dt, "PropES", StringComparison.OrdinalIgnoreCase) ? _esMult : 1.0;
                    result += V(dt) * mult;
                }
            }
            return result;
        }

        static readonly Dictionary<int, string> propDict = new()
        {
            { 6,  "PropQuality" },
            { 9,  "PropPDam" },
            { 10, "PropEDam" },
            { 12, "PropCrit" },
            { 13, "PropAps" },
            { 14, "PropRange" },
            { 15, "PropBlock" },
            { 16, "PropArm" },
            { 17, "PropEva" },
            { 18, "PropES" },
            { 54, "PropWard" },
        };

        //
        // Check for certain item properties
        // Things like quality and final values for Armor, Evasion and ES are in the "properties" array.
        //
        // "properties": [
        // {
        //   "name": "Armour",
        //   "values": [[ "220", 1 ]],
        //   "displayMode": 0,
        //   "type": 16
        // },
        // {
        //   "name": "Energy Shield",
        //   "values": [[ "55", 1 ]],
        //   "displayMode": 0,
        //   "type": 18
        // }
        // ],
        //

        void CheckProperties(JsonElement item)
        {
            if (!item.TryGetProperty("properties", out var props))
                return;
            var first = true;
            foreach (var prop in props.EnumerateArray())
            {
                var propType = prop.GetIntOrDefault("type", -1);
                if (BaseClass == BaseClass.Any)
                {
                    if (propType == -1 && first)
                    {
                        var itemClass = prop.GetStringOrDefault("name");
                        if (!string.IsNullOrEmpty(itemClass))
                            BaseClass = itemClass.ToBaseClass();
                    }
                }
                first = false;

                if (!propDict.ContainsKey(propType))
                    continue;

                var values = prop.GetArray("values");
                while (values.MoveNext() && values.Current.ValueKind == JsonValueKind.Array)
                {
                    var innerArray = values.Current.EnumerateArray();
                    if (innerArray.MoveNext())
                    {
                        var valueString = innerArray.Current.GetString().Replace("+", "").Replace("%", "");
                        var dashIndex = valueString.IndexOf("-");
                        if (dashIndex > 0)
                        {
                            if (double.TryParse(valueString.AsSpan(0, dashIndex), out var value1)
                                && double.TryParse(valueString.AsSpan(dashIndex+1), out var value2))
                                AddToTag(propDict[propType], (value1 + value2)/2.0);
                        }
                        else if (double.TryParse(valueString, out var value))
                        {
                            AddToTag(propDict[propType], value);
                        }
                    }
                }
            }
        }
    }
}
