using System;
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

        readonly Dictionary<string, TagValue> tagValues = new Dictionary<string, TagValue>();

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
                logger.Warn($"*** unknown mod '{s}'");
        }

        public Cat Cat { get; set; }
        public string ItemClass { get; set; }
        public string BaseType { get; set; }

        public ItemStats(Cat cat, string baseType)
        {
            Cat = cat;
            ItemClass = Cat.ToString();
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
            var frameType = json.GetIntOrDefault("frameType", 0);
            var identified = json.GetProperty("identified").GetBoolean();

            AddToTag("ilvl", item.iLvl);

            if (Cat == Cat.Junk)
            {
                var iconPath = json.GetStringOrDefault("icon");
                if (iconPath.Contains("Shields"))
                    ItemClass = "Shields";
            }

            if (frameType != 2 /*rare*/ || !identified) return;

            foreach (var modCat in modCats)
            {
                var fractured = modCat.StartsWith("fract");
                var theArray = json.GetArray(modCat);
                while (theArray.MoveNext())
                    CheckMod(theArray.Current.GetString(), fractured);

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
                    ItemClass = line.Substring(11).Trim();
                else if (line.StartsWith("Item Level:", StringComparison.OrdinalIgnoreCase))
                    AddToTag("ilvl", int.Parse(line.Substring(11).Trim()));
                else if (line.StartsWith("-"))
                {
                    seenDash = true;
                    continue;
                }
                else if (!seenDash || line.Contains(":"))
                    continue;
                else
                    CheckMod(line, fractured);
            }
        }

        public void DumpValues()
        {
            Console.WriteLine();
            if (!string.IsNullOrEmpty(ItemClass))
                Console.WriteLine($"class = {ItemClass}");
            if (!string.IsNullOrEmpty(BaseType))
                Console.WriteLine($"base = {BaseType}");
            foreach (var tagValue in tagValues.Values)
            {
                Console.WriteLine($"\t{tagValue.Tag}: {tagValue.Value}");
            }
            var msg = GetValueMessage();
            if (string.IsNullOrWhiteSpace(msg))
                Console.WriteLine("not interesting");
            else
                Console.WriteLine($"{GetValueMessage()}");
            Console.WriteLine();
        }

        static readonly Dictionary<int, string> propDict = new Dictionary<int, string>
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
                if (propType == -1 && first)
                {
                    var itemClass = prop.GetStringOrDefault("name");
                    if (!string.IsNullOrEmpty(itemClass))
                        ItemClass = itemClass;
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
                            if (double.TryParse(valueString.Substring(0, dashIndex), out var value1)
                                && double.TryParse(valueString.Substring(dashIndex+1), out var value2))
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
