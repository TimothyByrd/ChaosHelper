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

    public class ConsoleLog : ILogger
    {
        public void WriteLine(string line) => Console.WriteLine(line);
    }

    public struct ItemPosition
    {
        public int X;
        public int Y;
        public int H;
        public int W;
        public int iLvl;
        public bool Identified;
        public int TabIndex;

        public ItemPosition(int x, int y, int h, int w, int ilvl, bool identified, int tabIndex)
        {
            X = x;
            Y = y;
            H = h;
            W = w;
            iLvl = ilvl;
            Identified = identified;
            TabIndex = tabIndex;
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

    public struct ItemClass
    {
        public string Abbrev;
        public bool Skip;
        public int FontSize;
        public Cat Category;
        public string CategoryStr;
        public string FilterClass;

        public ItemClass(string abbrev, bool skip, int fontSize, Cat category, string categoryStr, string filterClass)
        {
            Abbrev = abbrev;
            Skip = skip;
            FontSize = fontSize;
            Category = category;
            CategoryStr = categoryStr;
            FilterClass = filterClass;
        }

        public static IEnumerable<ItemClass> Iterator()
        {
            foreach (var item in itemClasses)
            {
                yield return item;
            }
        }

        static readonly List<ItemClass> itemClasses = new List<ItemClass>
        {
            new ItemClass("a",  false, 38, Cat.BodyArmours, "BodyArmours", "Body Armours"),
            new ItemClass("h",  false, 38, Cat.Helmets, "Helmets", "Helmets"),
            new ItemClass("g",  false, 38, Cat.Gloves, "Gloves", "Gloves"),
            new ItemClass("b",  false, 38, Cat.Boots, "Boots", "Boots"),
            new ItemClass("w1", false, 38, Cat.OneHandWeapons, "OneHandWeapons", "Wands\" \"Daggers\" \"Sceptres\" \"One Hand Swords\" \"One Hand Maces"),
            new ItemClass("w2", true,  38, Cat.TwoHandWeapons, "TwoHandWeapons", "Bows"),
            new ItemClass("be", false, 45, Cat.Belts, "Belts", "Belts"),
            new ItemClass("am", false, 45, Cat.Amulets, "Amulets", "Amulets"),
            new ItemClass("ri", false, 45, Cat.Rings, "Rings", "Rings"),
            new ItemClass("j",  true,  38, Cat.Junk, "Junk", null),
        };
    }

    public class Currency
    {
        public string Name { get; private set; }
        public int Desired { get; private set; }
        public int FontSize { get; private set; }
        public string TextColor { get; private set; }
        public string BorderColor { get; private set; }
        public string BackGroundColor { get; private set; }
        public double ValueRatio { get; private set; }
        public double Value { get { return ValueRatio * CurrentCount; } }
        public bool CanFilterOn { get; private set; }
        public bool ShowBlock { get { return CanFilterOn && CurrentCount < Desired; } } 
        public int CurrentCount { get; set; }

        static public List<Currency> CurrencyList = new List<Currency>();

        public static void ResetCounts()
        {
            foreach (var x in CurrencyList)
                x.CurrentCount = 0;
        }

        static public Dictionary<string, Currency> GetWebDictionary()
        {
            var result = new Dictionary<string, Currency>();
            foreach (var x in CurrencyList)
                result[x.Name] = x;
            return result;
        }

        public static double GetTotalValue()
        {
            return CurrencyList.Sum(x => x.Value);
        }

        static double GetValueRatio(System.Text.Json.JsonElement element)
        {
            if (!element.TryGetProperty("value", out var valueProp))
                return 0.0;

            if (valueProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                return Math.Max(valueProp.GetDouble(), 0.0);

            var s = valueProp.GetString();
            var slash = s.IndexOf('/');
            if (slash > 0 && double.TryParse(s.Substring(0, slash), out var num)
                && double.TryParse(s.Substring(slash + 1), out var denom) && denom > 0)
                return Math.Max(num / denom, 0.0);
            if (double.TryParse(s, out var d))
                return Math.Max(d, 0.0); ;
            return 0.0;
        }

        static public void AddArray(System.Text.Json.JsonElement.ArrayEnumerator array)
        {
            while (array.MoveNext())
                Add(array.Current);
        }

        static public void Add(System.Text.Json.JsonElement element)
        {
            var canFilterOn = true;

            var desired = element.GetIntOrDefault("desired", 0);
            if (desired <= 0) canFilterOn = false;

            var currencyName = element.GetStringOrDefault("c");
            if (string.IsNullOrWhiteSpace(currencyName)) canFilterOn = false;
            
            var fontSize = element.GetIntOrDefault("fontSize", 0).Clamp(0, 50);
            if (fontSize <= 10) canFilterOn = false;

            var textColor = element.GetStringOrDefault("text").CheckColorString();
            if (string.IsNullOrWhiteSpace(textColor)) canFilterOn = false;
 
            var borderColor = element.GetStringOrDefault("border").CheckColorString();
            if (string.IsNullOrWhiteSpace(borderColor)) canFilterOn = false;

            var backgroundColor = element.GetStringOrDefault("back").CheckColorString();
            if (string.IsNullOrWhiteSpace(backgroundColor)) canFilterOn = false;

            var valueRatio = GetValueRatio(element);
            
            Log.Info($"Adding currency entry for {currencyName}: des={desired}, value={valueRatio}");

            CurrencyList.Add(new Currency {
                Name = currencyName,
                Desired = desired,
                FontSize = fontSize,
                TextColor = textColor,
                BorderColor = borderColor,
                BackGroundColor = backgroundColor,
                CanFilterOn = canFilterOn,
                ValueRatio = valueRatio,
                CurrentCount = int.MaxValue,
            });
        }
    }

// some extension methods
//
public static class Helpers
    {
        /// <summary>
        /// Returns a value indicating whether a specified substring occurs within this string.
        /// </summary>
        /// <param name="s">The string to seek in.</param>
        /// <param name="value">The string to seek.</param>
        /// <param name="comparisonType">One of the enumeration values that specifies the rules for the search.</param>
        /// <returns>true if the value parameter occurs within this string; otherwise, false.</returns>
        public static bool Contains(this string thisString, string value, StringComparison comparisonType)
        {
            return thisString.IndexOf(value, comparisonType) >= 0;
        }

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

        static readonly Regex nonColorCharRegex = new Regex("[^0-9xXa-fA-F ]+");
        static readonly Regex filterColorRegex  = new Regex("^[0-9]{1,3}( [0-9]{1,3}){2,3}$");

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
    }
}