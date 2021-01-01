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
        public Cat Category;
        public string CategoryStr;
        public string FilterClass;

        public ItemClass(string abbrev, bool skip, Cat category, string categoryStr, string filterClass)
        {
            Abbrev = abbrev;
            Skip = skip;
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
            new ItemClass("a",  false, Cat.BodyArmours, "BodyArmours", "Body Armours"),
            new ItemClass("h",  false, Cat.Helmets, "Helmets", "Helmets"),
            new ItemClass("g",  false, Cat.Gloves, "Gloves", "Gloves"),
            new ItemClass("b",  false, Cat.Boots, "Boots", "Boots"),
            new ItemClass("w1", false, Cat.OneHandWeapons, "OneHandWeapons", "Wands\" \"Daggers\" \"Sceptres\" \"One Hand Swords\" \"One Hand Maces"),
            new ItemClass("w2", true,  Cat.TwoHandWeapons, "TwoHandWeapons", "Bows"),
            new ItemClass("be", true,  Cat.Belts, "Belts", "Belts"),
            new ItemClass("am", true,  Cat.Amulets, "Amulets", "Amulets"),
            new ItemClass("ri", true,  Cat.Rings, "Rings", "Rings"),
            new ItemClass("j",  true,  Cat.Junk, "Junk", null),
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