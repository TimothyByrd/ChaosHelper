using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Overlay.NET.Common;

namespace ChaosHelper
{
    //
    // This file contains some small data structures that don't merit their own files.
    //

    public class ItemPosition
    {
        public int X;
        public int Y;
        public int H;
        public int W;
        public int iLvl;
        public bool Identified;
        public int TabIndex;
        public int Quality;
        public string Name;
        public object JsonElement;
        public string BaseType;

        public ItemPosition(int x, int y, int h, int w, int ilvl, bool identified, string name, string baseType, int tabIndex, int quality, object jsonElement)
        {
            X = x;
            Y = y;
            H = h;
            W = w;
            iLvl = ilvl;
            Identified = identified;
            Name = name;
            BaseType = baseType;
            TabIndex = tabIndex;
            Quality = quality;
            JsonElement = jsonElement;
        }

        public static int Compare(ItemPosition ip1, ItemPosition ip2)
        {
            if (ip1.X < ip2.X)
                return 1;
            if (ip1.X > ip2.X)
                return -1;
            if (ip1.Y < ip2.Y)
                return 1;
            if (ip1.Y > ip2.Y)
                return -1;
            return 0;
        }
    }

    public enum Cat
    {
        BodyArmours,
        Helmets,
        Gloves,
        Boots,
        OneHandWeapons,
        TwoHandWeapons,
        Belts,
        Amulets,
        Rings,
        Junk,
    }

    public enum BaseClass
    {
        Any,
        Amulets,
        Belts,
        BodyArmours,
        Boots,
        Gloves,
        Helmets,
        Jewels,
        OneHandWeapons,
        Quivers,
        Rings,
        Shields,
        TwoHandWeapons,
        Flask,
        Gems,
        Currency,
        Divination,
        Map,
    }

    public struct ItemClassForFilter
    {
        public string Abbrev;
        public bool Skip;
        public int FontSize;
        public Cat Category;
        public string CategoryStr;
        public string FilterClass;

        public ItemClassForFilter(string abbrev, bool skip, int fontSize, Cat category, string categoryStr, string filterClass)
        {
            Abbrev = abbrev;
            Skip = skip;
            FontSize = fontSize;
            Category = category;
            CategoryStr = categoryStr;
            FilterClass = filterClass;
        }

        public static IEnumerable<ItemClassForFilter> Iterator()
        {
            foreach (var item in itemClasses)
            {
                yield return item;
            }
        }

        static readonly List<ItemClassForFilter> itemClasses = new()
        {
            new ItemClassForFilter("a",  false, 38, Cat.BodyArmours, "BodyArmours", "Body Armours"),
            new ItemClassForFilter("h",  false, 38, Cat.Helmets, "Helmets", "Helmets"),
            new ItemClassForFilter("g",  false, 38, Cat.Gloves, "Gloves", "Gloves"),
            new ItemClassForFilter("b",  false, 38, Cat.Boots, "Boots", "Boots"),
            new ItemClassForFilter("w1", false, 38, Cat.OneHandWeapons, "OneHandWeapons", "Wands\" \"Daggers\" \"Sceptres\" \"One Hand Swords\" \"One Hand Maces"),
            new ItemClassForFilter("w2", true,  38, Cat.TwoHandWeapons, "TwoHandWeapons", "Bows"),
            new ItemClassForFilter("be", false, 45, Cat.Belts, "Belts", "Belts"),
            new ItemClassForFilter("am", false, 45, Cat.Amulets, "Amulets", "Amulets"),
            new ItemClassForFilter("ri", false, 45, Cat.Rings, "Rings", "Rings"),
            new ItemClassForFilter("j",  true,  38, Cat.Junk, "Junk", null),
        };
    }

// some extension methods
//
public static class Helpers
    {
        /// <summary>
        /// Gets the specified JSON property value as an integer, or returns a default value.
        /// </summary>
        /// <param name="element">The element holding the property.</param>
        /// <param name="valueName">The name of the property to find.</param>
        /// <param name="defaultValue">A default value to return is the property does not exist or is not a number.</param>
        public static int GetIntOrDefault(this System.Text.Json.JsonElement element, string valueName, int defaultValue)
        {
            if (element.TryGetProperty(valueName, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                return value.GetInt32();
            }
            return defaultValue;
        }

        /// <summary>
        /// Gets the specified JSON property value as an array
        /// </summary>
        /// <param name="element">The element holding the property.</param>
        /// <param name="valueName">The name of the property to find.</param>
        /// <returns>An array enumerator</returns>
        public static System.Text.Json.JsonElement.ArrayEnumerator GetArray(this System.Text.Json.JsonElement element, string valueName)
        {
            if (element.TryGetProperty(valueName, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Array)
                return value.EnumerateArray();
            return default;
        }

        /// <summary>
        /// Gets the specified JSON property value as an string, or returns a default value.
        /// </summary>
        /// <param name="element">The element holding the property.</param>
        /// <param name="valueName">The name of the property to find.</param>
        /// <param name="defaultValue">A default value to return is the property does not exist or is not a number.</param>
        public static string GetStringOrDefault(this System.Text.Json.JsonElement element, string valueName, string defaultValue = null)
        {
            if (element.TryGetProperty(valueName, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return value.GetString();
            }
            return defaultValue;
        }

        public static int Clamp(this int i, int min, int max)
        {
            return Math.Max(min, Math.Min(max, i));
        }

        static readonly Regex nonColorCharRegex = new("[^0-9xXa-fA-F ]+");
        static readonly Regex filterColorRegex  = new("^[0-9]{1,3}( [0-9]{1,3}){2,3}$");

        public static string CheckColorString(this string s, string defaultValue = null)
        {
            if (string.IsNullOrWhiteSpace(s))
                return defaultValue;

            s = nonColorCharRegex.Replace(s, " ").Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(s.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var parsedInt))
                {
                    var color = System.Drawing.Color.FromArgb(parsedInt);
                    return $"{color.R} {color.G} {color.B}";
                }
            }
            else if (filterColorRegex.IsMatch(s))
            {
                return s;
            }
            return defaultValue;
        }

        static readonly char[] spaceArray = new char[] { ' ' };

        public static int ColorStringToRGB(this string s)
        {
            if (!string.IsNullOrEmpty(s))
            {
                var nums = s.Split(spaceArray, StringSplitOptions.RemoveEmptyEntries);
                if (nums.Length >= 3)
                    return System.Drawing.Color.FromArgb(int.Parse(nums[0]), int.Parse(nums[1]), int.Parse(nums[2])).ToArgb();
            }
            return -1;
        }

        private static readonly Dictionary<Cat, BaseClass> catClassDict = new()
        {
            { Cat.BodyArmours, BaseClass.BodyArmours },
            { Cat.Helmets, BaseClass.Helmets },
            { Cat.Gloves, BaseClass.Gloves },
            { Cat.Boots, BaseClass.Boots },
            { Cat.OneHandWeapons, BaseClass.OneHandWeapons },
            { Cat.TwoHandWeapons, BaseClass.TwoHandWeapons },
            { Cat.Belts, BaseClass.Belts },
            { Cat.Amulets, BaseClass.Amulets },
            { Cat.Rings, BaseClass.Rings },
            { Cat.Junk, BaseClass.Any },
        };

        public static BaseClass ToBaseClass(this Cat c)
        {
            return catClassDict[c];
        }

        public static Cat ToCat(this BaseClass b)
        {
            var kvp = catClassDict.FirstOrDefault(x => x.Value == b);
            return kvp.Value == b ? kvp.Key : Cat.Junk;
        }

        public static BaseClass ToBaseClass(this string s)
        {
            if (Enum.TryParse<BaseClass>(s, true, out var baseClass)
                && Enum.IsDefined(typeof(BaseClass), baseClass))
                return baseClass;
            return BaseClass.Any;
        }

        public static BaseClass JsonToBaseClass(this System.Text.Json.JsonElement json)
        {
            var iconPath = json.GetStringOrDefault("icon");
            foreach (BaseClass b in Enum.GetValues(typeof(BaseClass)))
            {
                if (iconPath.Contains($"/{b}/"))
                {
                    return b;
                }
            }
            var baseType = json.GetStringOrDefault("baseType");
            if (baseType.Contains(" Flask", StringComparison.OrdinalIgnoreCase))
                return BaseClass.Flask;
            if (baseType.Contains(" Map", StringComparison.OrdinalIgnoreCase))
                return BaseClass.Map;


            return BaseClass.Any;
        }
    }
}