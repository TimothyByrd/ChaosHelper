using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ChaosHelper
{
    public class ItemSet
    {
        public class CountItem
        {
            public int NumIded60 { get; set; }
            public int NumUnIded60 { get; set; }
            public int NumIded75 { get; set; }
            public int NumUnIded75 { get; set; }
            public int Num60 { get { return NumIded60 + NumUnIded60; } }
            public int Num75 { get { return NumIded75 + NumUnIded75; } }
            public int NumIded { get { return NumIded60 + NumIded75; } }
            public int NumUnIded { get { return NumUnIded60 + NumUnIded75; } }
            public int Total { get { return NumIded + NumUnIded; } }
        }

        readonly Dictionary<string, List<ItemPosition>> itemsDict = new Dictionary<string, List<ItemPosition>>();
        readonly Dictionary<string, CountItem> countsDict = new Dictionary<string, CountItem>();

        public ItemSet()
        {
            foreach (var c in ItemClass.Iterator())
            {
                itemsDict[c.Category] = new List<ItemPosition>();
                countsDict[c.Category] = new CountItem();
            }
        }

        public void RefreshCounts()
        {
            foreach (var c in ItemClass.Iterator())
            {
                countsDict[c.Category] = Counts(c.Category);
            }
        }

        public string GetCountsMsg()
        {
            var msg = "";
            var sep = string.Empty;
            foreach (var c in ItemClass.Iterator())
            {
                var counts = countsDict[c.Category];
                if (counts.Total > 0)
                {
                    msg += (counts.NumIded > 0)
                        ? $"{sep}{c.Abbrev}:{counts.NumUnIded}({counts.NumIded})"
                        : $"{sep}{c.Abbrev}:{counts.NumUnIded}";
                    sep = ", ";
                }
            }
            return msg;
        }

        public CountItem Counts(string category)
        {
            var result = new CountItem();
            foreach (var i in itemsDict[category])
            {
                if (i.iLvl < 75)
                {
                    if (i.Identified)
                        ++result.NumIded60;
                    else
                        ++result.NumUnIded60;
                }
                else
                {
                    if (i.Identified)
                        ++result.NumIded75;
                    else
                        ++result.NumUnIded75;
                }
            }

            return result;
        }

        public int UnidedCount(string category)
        {
            return countsDict[category].NumUnIded;
        }

        public List<ItemPosition> GetCategory(string s)
        {
            return itemsDict[s];
        }

        public void Add(string category, JsonElement e, int tabIndex)
        {
            itemsDict[category].Add(new ItemPosition(
                e.GetProperty("x").GetInt32(),
                e.GetProperty("y").GetInt32(),
                e.GetProperty("h").GetInt32(),
                e.GetProperty("w").GetInt32(),
                e.GetProperty("ilvl").GetInt32(),
                e.GetProperty("identified").GetBoolean(),
                tabIndex
                ));
        }

        public ItemSet GetSetToSell(bool allowIdentified)
        {
            CleanOutInventoryItems();

            // Determine how many unided sets we can make.
            var unidedPossible = CountPossible(false);
            if (unidedPossible > 0)
                return MakeSet(Math.Min(2, unidedPossible), false);

            if (allowIdentified)
            {
                var idedPossible = CountPossible(true);
                if (idedPossible > 0)
                    return MakeSet(Math.Min(2, idedPossible), true);
            }
            return null;
        }

        /// <summary>
        /// If we are including any items from the character's inventory, remove them.
        /// </summary>
        private void CleanOutInventoryItems()
        {
            if (itemsDict.Any(x => x.Value.Any(y => y.TabIndex < 0)))
            {
                foreach (var c in ItemClass.Iterator())
                {
                    itemsDict[c.Category] = itemsDict[c.Category].Where(z => z.TabIndex >= 0).ToList();
                }
                RefreshCounts();
            }
        }

        /// <summary>
        /// Determine how many complete sets we can make.
        /// </summary>
        /// <param name="ided">Are we checking for IDed sets?</param>
        /// <returns>The number of sets that could be turned in</returns>
        public int CountPossible(bool ided)
        {
            // Determine how many complete sets we can make.
            var possible = int.MaxValue;
            foreach (var c in ItemClass.Iterator())
            {
                if (c.Category == "Junk" || c.Category == "TwoHandWeapons")
                    continue;

                var count = itemsDict[c.Category].Count(x => !x.Identified || ided);
                if (c.Category == "Rings")
                {
                    count = itemsDict[c.Category].Count(x => x.Identified == ided) / 2;
                }
                else if (c.Category == "OneHandWeapons")
                {
                    count /= 2;
                    var c2Short = itemsDict["TwoHandWeapons"].Count(x => !x.Identified || ided && x.H == 3);
                    var c2Tall = itemsDict["TwoHandWeapons"].Count(x => !x.Identified || ided && x.H == 4);
                    if (c2Tall > 0) // can only fit in 1 2x4 weapon
                        ++c2Short;
                    count += c2Short;
                }
                possible = Math.Min(possible, count);
                if (possible == 0)
                    break;
            }

            return possible;
        }

        /// <summary>
        /// Make a set of items to sell (may be two sets of items, since that will fit into a character's inventory)
        /// </summary>
        /// <param name="numSets">the number of sets to make (1 or 2) - trust the caller to give a doable number</param>
        /// <param name="ided">It true, do IDed sets - both rings IDed and prefer IDed for everythng else.</param>
        /// <returns></returns>
        private ItemSet MakeSet(int numSets, bool ided)
        {
            numSets = Math.Min(2, numSets); // only two sets will fit in the character's inventory.

            var result = new ItemSet();
            foreach (var c in ItemClass.Iterator())
            {
                if (c.Category == "Junk" || c.Category == "TwoHandWeapons")
                    continue;

                if (c.Category == "Rings")
                {
                    result.itemsDict[c.Category] = itemsDict[c.Category]
                        .Where(x => x.Identified == ided).Take(numSets * 2).ToList();
                }
                else if (c.Category == "OneHandWeapons")
                {
                    var need1hd = numSets * 2;
                    // first use up the 2hd weapons
                    var w2List = new List<ItemPosition>();

                    // can have one tall one
                    w2List.AddRange(itemsDict["TwoHandWeapons"]
                        .Where(x => x.Identified == ided && x.H == 4).Take(1));
                    if (w2List.Count == 0 && ided)
                    {
                        w2List.AddRange(itemsDict["TwoHandWeapons"]
                            .Where(x => !x.Identified && x.H == 4).Take(1));
                    }
                    if (w2List.Count < numSets)
                    {
                        var want = numSets - w2List.Count;
                        w2List.AddRange(itemsDict["TwoHandWeapons"]
                            .Where(x => x.Identified == ided && x.H == 3).Take(want));
                    }
                    if (w2List.Count < numSets)
                    {
                        var want = numSets - w2List.Count;
                        w2List.AddRange(itemsDict["TwoHandWeapons"]
                            .Where(x => !x.Identified && x.H == 3).Take(want));
                    }
                    result.itemsDict["TwoHandWeapons"] = w2List;
                    itemsDict["TwoHandWeapons"] = itemsDict["TwoHandWeapons"].Except(result.itemsDict["TwoHandWeapons"]).ToList();

                    // add in 1hd weapons
                    if (w2List.Count < numSets)
                    {
                        var want = (numSets - w2List.Count) * 2;
                        var w1List = new List<ItemPosition>();
                        w1List.AddRange(itemsDict[c.Category]
                            .Where(x => x.Identified == ided).Take(want));
                        if (w1List.Count < want && ided)
                        {
                            want -= w1List.Count;
                            w1List.AddRange(itemsDict[c.Category]
                                .Where(x => !x.Identified).Take(want));
                        }
                        result.itemsDict[c.Category] = w1List;
                    }
                }
                else
                {
                    var list = new List<ItemPosition>();
                    list.AddRange(itemsDict[c.Category].Where(x => x.Identified == ided).Take(numSets));
                    if (list.Count < numSets && ided)
                    {
                        var want = numSets - list.Count;
                        list.AddRange(itemsDict[c.Category]
                            .Where(x => !x.Identified).Take(want));

                    }
                    result.itemsDict[c.Category] = list;
                }

                // remove the selected items
                itemsDict[c.Category] = itemsDict[c.Category].Except(result.itemsDict[c.Category]).ToList();
            }

            RefreshCounts();
            return result;
        }

        ///// Make a set of items to sell (may be two sets of items, since that will fit into a character's inventory)
        ///// Optimizes for when ilvl 75+ items start coming in.
        ///// </summary>
        ///// <param name="numSets">the number of sets to make (1 or 2) - trust the caller to give a doable number</param>
        ///// <param name="ided">It true, do IDed sets - both rings IDed and prefer IDed for everythng else.</param>
        ///// <param name="canDoRegalRecipe">Is an all 75+ recipe acceptable.</param>
        ///// <returns></returns>
        //private ItemSet MakeSet74(int numSets, bool ided, bool canDoRegalRecipe)
        //{
        //    return null;
        //}
    }
}