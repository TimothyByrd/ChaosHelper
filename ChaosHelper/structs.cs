using System;
using System.Collections.Generic;

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
    }

    public struct ItemClass
    {
        public string Category;
        public string Abbrev;
        public string FilterClass;
        public bool Skip;

        public ItemClass(string abbrev, bool skip, string category, string filterClass)
        {
            Category = category;
            Abbrev = abbrev;
            Skip = skip;
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
            new ItemClass("a",  false, "BodyArmours", "Body Armours"),
            new ItemClass("h",  false, "Helmets", "Helmets"),
            new ItemClass("g",  false, "Gloves", "Gloves"),
            new ItemClass("b",  false, "Boots", "Boots"),
            new ItemClass("w1", false, "OneHandWeapons", "Wands\" \"Daggers\" \"Sceptres\" \"One Hand Swords\" \"One Hand Maces"),
            new ItemClass("w2", true,  "TwoHandWeapons", "Bows"),
            new ItemClass("be", true,  "Belts", "Belts"),
            new ItemClass("am", true,  "Amulets", "Amulets"),
            new ItemClass("ri", true,  "Rings", "Rings"),
            new ItemClass("j",  true,  "Junk", null),
        };
    }

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
    }
}