using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ChaosHelper
{
    public class ItemSet
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public class ItemCounts
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

        readonly Dictionary<Cat, List<ItemPosition>> itemsDict = new();
        readonly Dictionary<Cat, ItemCounts> countsDict = new();
        readonly Dictionary<Cat, bool> showDict = new();

        public ItemSet()
        {
            foreach (var c in ItemClassForFilter.Iterator())
            {
                itemsDict[c.Category] = new List<ItemPosition>();
                countsDict[c.Category] = new ItemCounts();
            }
        }

        public void RefreshCounts()
        {
            foreach (var c in ItemClassForFilter.Iterator())
            {
                countsDict[c.Category] = Counts(c.Category);
            }
        }

        public void Sort()
        {
            foreach (var c in ItemClassForFilter.Iterator())
            {
                itemsDict[c.Category].Sort(ItemPosition.Compare);
            }
        }

        public string GetCountsMsg()
        {
            var msg = "";
            var sep = string.Empty;
            foreach (var c in ItemClassForFilter.Iterator())
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

        public ItemCounts Counts(Cat category)
        {
            var result = new ItemCounts();
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

        public void CalculateClassesToShow(int maxSets, string ignoreMaxSets)
        {
            foreach (var c in ItemClassForFilter.Iterator())
            {
                if (c.Skip)
                    continue;

                if (ignoreMaxSets.Contains(c.CategoryStr, StringComparison.OrdinalIgnoreCase))
                {
                    showDict[c.Category] = true;
                    continue;
                }

                var wanted = maxSets;
                var haveSoFar = countsDict[c.Category].NumUnIded;
                if (c.Category == Cat.OneHandWeapons)
                {
                    wanted *= 2;
                    var num2hd = countsDict[Cat.TwoHandWeapons].NumUnIded;
                    if (num2hd > 0)
                    {
                        // can only use one 2x4 2hd-weapon per recipe inventory
                        var unIded2x4 = itemsDict[Cat.TwoHandWeapons].Count(x => !x.Identified && x.H == 4);
                        haveSoFar += (num2hd - unIded2x4) * 2;
                        unIded2x4 = Math.Min(haveSoFar / 2, unIded2x4);
                        haveSoFar += unIded2x4 * 2;
                    }
                }
                else if (c.Category == Cat.Rings)
                {
                    wanted *= 2;
                }
                showDict[c.Category] = haveSoFar < wanted;
            }
        }

        public bool SameClassesToShow(ItemSet other)
        {
            var keys = showDict.Keys;
            var keysOther = other.showDict.Keys;

            if (keys.Count != keysOther.Count || keys.Any(x => !keysOther.Contains(x)))
                return false;

            foreach (var key in keys)
            {
                if (showDict[key] != other.showDict[key])
                    return false;
            }

            return true;
        }

        public bool ShouldShow(Cat category)
        {
            return showDict[category];
        }

        public List<ItemPosition> GetCategory(Cat category)
        {
            return itemsDict[category];
        }

        public void Add(Cat category, JsonElement e, int tabIndex, int quality = 0)
        {
            itemsDict[category].Add(new ItemPosition(
                e.GetProperty("x").GetInt32(),
                e.GetProperty("y").GetInt32(),
                e.GetProperty("h").GetInt32(),
                e.GetProperty("w").GetInt32(),
                e.GetProperty("ilvl").GetInt32(),
                e.GetProperty("identified").GetBoolean(),
                e.GetProperty("name").GetString(),
                e.GetProperty("baseType").GetString(),
                tabIndex,
                quality,
                e));
        }

        public ItemSet GetSetToSell(bool allowIdentified, int chaosParanoiaLevel)
        {
            CleanOutInventoryItems();

            // Determine how many unided sets we can make.
            var unidedPossible = CountPossible(false);
            if (unidedPossible > 0)
                return MakeSetOptimize(Math.Min(2, unidedPossible), false, chaosParanoiaLevel);

            if (allowIdentified)
            {
                var idedPossible = CountPossible(true);
                if (idedPossible > 0)
                    return MakeSetOptimize(Math.Min(2, idedPossible), true, chaosParanoiaLevel);
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
                foreach (var c in ItemClassForFilter.Iterator())
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
            foreach (var c in ItemClassForFilter.Iterator())
            {
                if (c.Category == Cat.Junk || c.Category == Cat.TwoHandWeapons)
                    continue;

                var count = itemsDict[c.Category].Count(x => !x.Identified || ided);
                if (c.Category == Cat.Rings)
                {
                    count = itemsDict[c.Category].Count(x => x.Identified == ided) / 2;
                }
                else if (c.Category == Cat.OneHandWeapons)
                {
                    count /= 2;
                    var c2Short = itemsDict[Cat.TwoHandWeapons].Count(x => (!x.Identified || ided) && x.H == 3);
                    var c2Tall = itemsDict[Cat.TwoHandWeapons].Count(x => (!x.Identified || ided) && x.H == 4);
                    count += c2Short;
                    count += Math.Min(Math.Max(1, count), c2Tall);
                }
                possible = Math.Min(possible, count);
                if (possible == 0)
                    break;
            }

            return possible;
        }

        private void RemoveItems(ItemSet itemsToRemove)
        {
            foreach (var c in ItemClassForFilter.Iterator())
            {
                itemsDict[c.Category] = itemsDict[c.Category].Except(itemsToRemove.itemsDict[c.Category]).ToList();
            }
        }

        /// Make a set of items to sell (may be two sets of items, since that will fit into a character's inventory)
        /// Optimizes for when ilvl 75+ items start coming in.
        /// </summary>
        /// <param name="numSets">the number of sets to make (1 or 2) - trust the caller to give a doable number</param>
        /// <param name="ided">It true, do IDed sets - both rings IDed and prefer IDed for everythng else.</param>
        /// <returns></returns>
        private ItemSet MakeSetOptimize(int numSets, bool ided, int chaosParanoiaLevel)
        {
            var result = new ItemSet();

            var optimizerList = new List<ChaosSlotOptimizer>
            {
                new ChaosSlotOptimizerWeapons(this, 1),
                new ChaosSlotOptimizer(this, Cat.BodyArmours, 2),
                new ChaosSlotOptimizer(this, Cat.Gloves, 3),
                new ChaosSlotOptimizer(this, Cat.Boots, 4),
                new ChaosSlotOptimizer(this, Cat.Helmets, 5),
                new ChaosSlotOptimizer(this, Cat.Belts, 6),
                new ChaosSlotOptimizer(this, Cat.Amulets, 7),
                new ChaosSlotOptimizerRings(this, 8),
            };

            foreach (var o in optimizerList)
                o.Calculate();

            var know60Cat = false;
            var mustBe60 = Cat.Junk;

            if (optimizerList.Any(x => x.CanMake75 == 0))
            {
                // if any can't make 1 75, then
                // GetItems(mustbe60: false) for all
                know60Cat = true;
            }

            if (!know60Cat)
            {
                //else if any can make n 60, then
                //     select the highest priority slot (biggest item) that can make n 60
                //     GetItems(mustbe60: true) for it, GetItems(mustbe60: false) for rest
                var bestFor60 = optimizerList.FirstOrDefault(x => x.CanMake60 >= numSets);
                if (bestFor60 != null)
                {
                    mustBe60 = bestFor60.Category;
                    know60Cat = true;
                }
            }

            if (!know60Cat && ided)
            {
                //else if any can make n 60, then
                //     select the highest priority slot (biggest item) that can make n 60
                //     GetItems(mustbe60: true) for it, GetItems(mustbe60: false) for rest
                var bestFor60Ided = optimizerList.FirstOrDefault(x => x.CanMake60Ided >= numSets);
                if (bestFor60Ided != null)
                {
                    mustBe60 = bestFor60Ided.Category;
                    know60Cat = true;
                }
            }

            if (!know60Cat && chaosParanoiaLevel > 0 && numSets > 1)
            {
                //else if (optimize and any can make 1 60)
                //     select the highest priority slot (biggest item) that can make 1 60
                //     GetItems(mustbe60: true) for it, GetItems(mustbe60: false) for rest
                var bestFor60 = optimizerList.FirstOrDefault(x => x.CanMake60 >= 1);
                if (bestFor60 != null)
                {
                    mustBe60 = bestFor60.Category;
                    know60Cat = true;
                    numSets = 1;
                }
            }

            if (!know60Cat && chaosParanoiaLevel > 0 && numSets > 1 && ided)
            {
                //else if (optimize and any can make 1 60)
                //     select the highest priority slot (biggest item) that can make 1 60
                //     GetItems(mustbe60: true) for it, GetItems(mustbe60: false) for rest
                var bestFor60 = optimizerList.FirstOrDefault(x => x.CanMake60Ided >= 1);
                if (bestFor60 != null)
                {
                    mustBe60 = bestFor60.Category;
                    know60Cat = true;
                    numSets = 1;
                }
            }

            //else
            //     GetItems(mustbe60: false) for all

            foreach (var o in optimizerList)
                o.GetItems(result, numSets, o.Category == mustBe60, ided, chaosParanoiaLevel);

            // remove the selected items
            RemoveItems(result);
            RefreshCounts();
            return result;
        }

        public class QualityComparer : IComparer<ItemPosition>
        {
            // sort largest first, but put 10, 8 and 5 at the end
            // since they divide into 40 evenly want to use them last.
            public int Compare(ItemPosition x, ItemPosition y)
            {
                var xDivs = x.Quality == 10 || x.Quality == 8 || x.Quality == 5;
                var yDivs = y.Quality == 10 || y.Quality == 8 || y.Quality == 5;
                if (xDivs == yDivs)
                    return x.Quality > y.Quality ? -1 : x.Quality == y.Quality ? 0 : 1;
                else if (xDivs)
                    return 1;
                else
                    return -1;
            }
        }

        public ItemSet MakeQualitySet()
        {
            static bool IsGem(ItemPosition itemPos)
            {
                return itemPos.iLvl == 0;
            }

            var result = new ItemSet();

            var gems = itemsDict[Cat.Junk].Where(x => x.H == 1 && IsGem(x)).OrderBy(x => x, new QualityComparer());
            //ShowSet("gem", gems);
            var gemSet = MakeAQualitySet(gems, Config.QualityGemRecipeSlop);
            if (gemSet != null)
                result.itemsDict[Cat.Junk].AddRange(gemSet);

            var flasks = itemsDict[Cat.Junk].Where(x => x.H == 2).OrderBy(x => x, new QualityComparer());
            //ShowSet("flask", flasks);
            var flaskSet = MakeAQualitySet(flasks, Config.QualityFlaskRecipeSlop);
            if (flaskSet != null)
                result.itemsDict[Cat.Junk].AddRange(flaskSet);

            var maps = itemsDict[Cat.Junk].Where(x => x.H == 1 && !IsGem(x)).OrderBy(x => x, new QualityComparer());
            var mapSet = MakeAQualitySet(maps, Config.QualityGemRecipeSlop);
            if (mapSet != null)
                result.itemsDict[Cat.Junk].AddRange(mapSet);

            RemoveItems(result);
            return result;
        }

        //private void ShowSet(string itemType, IEnumerable<ItemPosition> items)
        //{
        //    var qualities = string.Join(",", items.Select(x => x.Quality));
        //    logger.Debug($"{itemType} qualities: {qualities}");
        //}

        private IEnumerable<ItemPosition> MakeAQualitySet(IEnumerable<ItemPosition> items, int allowedSlop)
        {
            for (int i = 0; i <= allowedSlop; ++i)
            {
                var result = MakeAQualitySetRecurse(items, 40, i);
                if (result != null)
                    return result;
            }
            return null;
        }

        private IEnumerable<ItemPosition> MakeAQualitySetRecurse(IEnumerable<ItemPosition> items, int minNeeded, int allowedSlop)
        {
            if (items == null || !items.Any())
                return null;

            // Is this item a solution?
            //
            var item = items.First();
            if (item.Quality >= minNeeded && item.Quality <= minNeeded + allowedSlop)
                return new List<ItemPosition> { item };

            if (item.Quality < minNeeded)
            {
                // Try to solve using this item.
                //
                var subResult1 = MakeAQualitySetRecurse(items.Skip(1), minNeeded - item.Quality, allowedSlop);
                if (subResult1 != null)
                    return subResult1.Append(item);
            }

            // Try to solve skipping this item
            //
            return MakeAQualitySetRecurse(items.Skip(1), minNeeded, allowedSlop);
        }

        public void LogMatchingNames(Dictionary<int, string> dumpTabDict)
        {
            string DumpTabName(int tabIndex)
            {
                return dumpTabDict.ContainsKey(tabIndex) ? dumpTabDict[tabIndex] : "<unknown>";
            }

            var nameDict = new Dictionary<string, ItemPosition>();
            foreach (var c in ItemClassForFilter.Iterator())
            {
                foreach (var item in itemsDict[c.Category])
                {
                    if (string.IsNullOrWhiteSpace(item.Name)) continue;
                    if (!nameDict.ContainsKey(item.Name))
                        nameDict[item.Name] = item;
                    else
                    {
                        var otherItem = nameDict[item.Name];
                        var item1 = item.TabIndex < otherItem.TabIndex ? item : otherItem;
                        var item2 = item.TabIndex < otherItem.TabIndex ? otherItem : item;
                        var tab1 = DumpTabName(item1.TabIndex);
                        var tab2 = DumpTabName(item2.TabIndex);
                        logger.Info($"name match: tab '{tab1}' at {item1.X},{item1.Y}, '{tab2}' at {item2.X},{item2.Y} - {item.Name}");
                        nameDict.Remove(item.Name);
                    }
                }
            }
        }

        public void CheckMods(Dictionary<int, string> dumpTabDict)
        {
            string DumpTabName(int tabIndex)
            {
                return dumpTabDict.ContainsKey(tabIndex) ? dumpTabDict[tabIndex] : "<unknown>";
            }

            var interestingItems = new SortedDictionary<string, ItemStats>();
            foreach (var c in ItemClassForFilter.Iterator())
                foreach (var item in itemsDict[c.Category])
                {
                    var itemStats = new ItemStats(c.Category, item.BaseType);
                    itemStats.CheckMods(item);
                    var message = itemStats.GetValueMessage();
                    if (message == null) continue;
                    var tab = DumpTabName(item.TabIndex);
                    interestingItems.Add($"{item.TabIndex:D4}interesting item: tab '{tab}' at {item.X},{item.Y} - {item.Name} - {message}", itemStats);
                }

            foreach (var kv in interestingItems)
            {
                logger.Info(kv.Key.Substring(4));
                kv.Value.DumpValues();
            }
        }

        private static readonly Dictionary<BaseClass, string> equippedSlotDict = new()
        {
            { BaseClass.BodyArmours, "BodyArmour" },
            { BaseClass.Helmets, "Helm" },
            { BaseClass.Gloves, "Gloves" },
            { BaseClass.Boots, "Boots" },
            { BaseClass.OneHandWeapons, "Weapon" },
            { BaseClass.TwoHandWeapons, "Weapon" },
            { BaseClass.Belts, "Belt" },
            { BaseClass.Amulets, "Amulet" },
            { BaseClass.Rings, "Ring" }, // or Ring2
            { BaseClass.Shields, "Offhand" },
        };

        public void UpdateDynamicRules()
        {
            foreach (var rule in ItemRule.Rules.Where(x => x.IsDynamic))
            {
                if (!equippedSlotDict.ContainsKey(rule.BaseClass))
                {
                    logger.Warn($"dynamic rule '{rule.Name}' wants '{rule.BaseClass}'");
                    continue;
                }

                // find the correct equipped item.
                //
                var itemStats = FindItemForRule(rule);
                if (itemStats == null)
                {
                    logger.Warn($"could not find equipped item '{rule.BaseClass}' for dynamic rule '{rule.Name}'");
                    continue;
                }

                // use it to update the rule
                //
                rule.UpdateDynamic(itemStats);
            }
        }

        private ItemStats FindItemForRule(ItemRule rule)
        {
            var wantedInventoryId = equippedSlotDict[rule.BaseClass];
            if (rule.BaseClass == BaseClass.Rings && rule.Name.Contains("2"))
                wantedInventoryId = "Ring2";

            ItemStats TryFind(Cat cat)
            {
                foreach (var item in itemsDict[cat])
                {
                    var jsonElement = (JsonElement)item.JsonElement;
                    var inventoryId = jsonElement.GetProperty("inventoryId").GetString();
                    if (inventoryId == wantedInventoryId)
                    {
                        var itemStats = new ItemStats(cat, item.BaseType);
                        itemStats.CheckMods(item);
                        return itemStats;
                    }
                }
                return null;
            }

            var initialCat = rule.BaseClass.ToCat();
            var result = TryFind(initialCat);
            if (result != null)
                return result;

            foreach (var c in ItemClassForFilter.Iterator())
            {
                result = TryFind(c.Category);
                if (result != null)
                    return result;
            }
            return null;
        }
    }

    public class ChaosSlotOptimizer
    {
        protected static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public Cat Category { get; protected set; }
        public int Priority { get; protected set; }
        public int CanMake { get; protected set; }
        public int CanMake60 { get; protected set; }
        public int CanMake75 { get; protected set; }
        public int CanMakeIded { get; protected set; }
        public int CanMake60Ided { get; protected set; }
        public int CanMake75Ided { get; protected set; }

        protected readonly ItemSet source;

        public ChaosSlotOptimizer(ItemSet source, Cat category, int priority)
        {
            this.source = source;
            Category = category;
            Priority = priority;
        }

        protected void CalculateInternal(Cat category)
        {
            foreach (var item in source.GetCategory(category))
            {
                ++CanMakeIded;
                if (item.iLvl >= 75)
                    ++CanMake75Ided;
                else
                    ++CanMake60Ided;
                if (!item.Identified)
                {
                    ++CanMake;
                    if (item.iLvl >= 75)
                        ++CanMake75;
                    else
                        ++CanMake60;
                }
            }
        }

        public virtual void Calculate()
        {
            CalculateInternal(Category);
        }

        public virtual void GetItems(ItemSet destination, int numSets, bool mustBe60, bool ided, int chaosParanoiaLevel)
        {
            var sourceList = source.GetCategory(Category);
            var list = new List<ItemPosition>();

            // take ilvl 75+ items first, if possible.
            //
            if (!mustBe60)
            {
                list.AddRange(sourceList.Where(x => x.Identified == ided && x.iLvl >= 75).Take(numSets));
            }

            // This block favors using up Ided items over using up ilvl 75+ items
            // skip to really optimize chaos vs regal at the expense of hoarding Ided ilvl 60 items.
            //
            var took60Ided = false;
            if (chaosParanoiaLevel < 2 && list.Count < numSets && ided)
            {
                var want = numSets - list.Count;
                list.AddRange(sourceList.Where(x => x.Identified && x.iLvl < 75).Take(want));
                took60Ided = true;
            }

            // un-Ided ilvl 75+ in a IDed recipe
            //
            if (!mustBe60 && list.Count < numSets && ided && CanMake75 > 0)
            {
                var want = numSets - list.Count;
                list.AddRange(sourceList.Where(x => !x.Identified && x.iLvl >= 75).Take(want));
            }

            // now for ilvl 60 items
            //
            if (list.Count < numSets && (!took60Ided || !ided))
            {
                var want = numSets - list.Count;
                list.AddRange(sourceList.Where(x => x.Identified == ided && x.iLvl < 75).Take(want));
            }
            if (list.Count < numSets && ided && CanMake60 > 0)
            {
                var want = numSets - list.Count;
                list.AddRange(sourceList.Where(x => !x.Identified && x.iLvl < 75).Take(want));
            }

            if (list.Count < numSets)
                logger.Error($"Wanted {numSets} for {Category}, but only got {list.Count}, mustBe60:{mustBe60}, ided:{ided}");
            else if (list.Count == 2 && list[0].X == list[1].X && list[0].Y == list[1].Y)
                logger.Error($"Took same item twice for category {Category}, mustBe60:{mustBe60}, ided:{ided}");

            destination.GetCategory(Category).AddRange(list);
        }
    }

    public class ChaosSlotOptimizerRings : ChaosSlotOptimizer
    {
        public ChaosSlotOptimizerRings(ItemSet source, int priority)
            : base(source, Cat.Rings, priority)
        {
        }

        public override void Calculate()
        {
            CalculateInternal(Category);

            // for rings, IDed cannot mix in un-IDed.
            CanMakeIded -= CanMake;
            CanMake60Ided -= CanMake60;
            CanMake75Ided -= CanMake75;

            // because we need two per set.
            CanMake /= 2;
            CanMake60 /= 2;
            CanMake75 /= 2;
            CanMakeIded /= 2;
            CanMake60Ided /= 2;
            CanMake75Ided /= 2;
        }

        public override void GetItems(ItemSet destination, int numSets, bool mustBe60, bool ided, int chaosParanoiaLevel)
        {
            // because we need two per set.
            var wanted = numSets * 2;

            var sourceList = source.GetCategory(Category);
            var list = new List<ItemPosition>();

            // take ilvl 75+ items first, if possible.
            // rings can't mix IDed and un-IDed.
            var want = mustBe60 ? 1 : wanted;
            list.AddRange(sourceList.Where(x => x.Identified == ided && x.iLvl >= 75).Take(want));

            want = wanted - list.Count;
            if (want > 0)
                list.AddRange(sourceList.Where(x => x.Identified == ided && x.iLvl < 75).Take(want));

            if (list.Count < wanted )
                logger.Error($"Wanted {numSets} sets for {Category}, but only got {list.Count / 2}, mustBe60:{mustBe60}, ided:{ided}");

            destination.GetCategory(Category).AddRange(list);
        }
    }

    public class ChaosSlotOptimizerWeapons : ChaosSlotOptimizer
    {
        private int w2H3_60;
        private int w2H3_75;
        private int w2H3_60Id;
        private int w2H3_75Id;
        private int w2H4_60;
        private int w2H4_75;
        private int w2H4_60Id;
        private int w2H4_75Id;
        private int w1_60;
        private int w1_75;
        private int w1_60Id;
        private int w1_75Id;

        public ChaosSlotOptimizerWeapons(ItemSet source, int priority)
            : base(source, Cat.OneHandWeapons, priority)
        {
        }

        public override void Calculate()
        {
            CalculateInternal(Cat.OneHandWeapons);
            w1_60 = CanMake60;
            w1_75 = CanMake75;
            w1_60Id = CanMake60Ided - CanMake60;
            w1_75Id = CanMake75Ided - CanMake75;

            // because we need two per set.
            CanMake /= 2;
            CanMake60 /= 2;
            CanMake75 /= 2;
            CanMakeIded /= 2;
            CanMake60Ided /= 2;
            CanMake75Ided /= 2;

            foreach (var item in source.GetCategory(Cat.TwoHandWeapons))
            {
                if (item.H == 3 && item.iLvl < 75 && !item.Identified) ++w2H3_60;
                if (item.H == 3 && item.iLvl >= 75 && !item.Identified) ++w2H3_75;
                if (item.H == 3 && item.iLvl < 75 && item.Identified) ++w2H3_60Id;
                if (item.H == 3 && item.iLvl >= 75 && item.Identified) ++w2H3_75Id;
                if (item.H == 4 && item.iLvl < 75 && !item.Identified) ++w2H4_60;
                if (item.H == 4 && item.iLvl >= 75 && !item.Identified) ++w2H4_75;
                if (item.H == 4 && item.iLvl < 75 && item.Identified) ++w2H4_60Id;
                if (item.H == 4 && item.iLvl >= 75 && item.Identified) ++w2H4_75Id;
            }

            CanMake += (w2H3_60 + w2H3_75);
            CanMake60 += w2H3_60;
            CanMake75 += w2H3_75;
            CanMakeIded += (w2H3_60 + w2H3_75 + w2H3_60Id + w2H3_75Id);
            CanMake60Ided += (w2H3_60 + w2H3_60Id);
            CanMake75Ided += (w2H3_75 + w2H3_75Id);

            // only one 2x4 can count
            if (w2H4_60 + w2H4_75 > 0) ++CanMake;
            if (w2H4_60 > 0) ++CanMake60;
            if (w2H4_75 > 0) ++CanMake75;
            if (w2H4_60 + w2H4_75 + w2H4_60Id + w2H4_75Id > 0) ++CanMakeIded;
            if (w2H4_60 + w2H4_60Id > 0) ++CanMake60Ided;
            if (w2H4_75 + w2H4_75Id > 0) ++CanMake75Ided;
        }

        public override void GetItems(ItemSet destination, int numSets, bool mustBe60, bool ided, int chaosParanoiaLevel)
        {
            var w2Source = source.GetCategory(Cat.TwoHandWeapons);
            var w2List = new List<ItemPosition>();

            var w1Source = source.GetCategory(Cat.OneHandWeapons);
            var w1List = new List<ItemPosition>();

            int StillNeed() { return (numSets * 2) - (w2List.Count * 2 + w1List.Count); }

            // 2x4 weapons trump everything because they take up so much stash space
            //
            if (!mustBe60)
                w2List.AddRange(w2Source.Where(x => x.Identified == ided && x.iLvl >= 75 && x.H == 4).Take(1));
            if (!mustBe60 && w2List.Count == 0 && ided && w2H4_75 > 0)
                w2List.AddRange(w2Source.Where(x => !x.Identified && x.iLvl >= 75 && x.H == 4).Take(1));
            if (w2List.Count == 0)
                w2List.AddRange(w2Source.Where(x => x.Identified == ided && x.iLvl < 75 && x.H == 4).Take(1));
            if (w2List.Count == 0 && ided && w2H4_60 > 0)
                w2List.AddRange(w2Source.Where(x => !x.Identified && x.iLvl < 75 && x.H == 4).Take(1));

            // next take ilvl 75+ 2x3 items
            if (!mustBe60 && w2List.Count < numSets)
            {
                w2List.AddRange(w2Source.Where(x => x.Identified == ided && x.iLvl >= 75 && x.H == 3).Take(numSets - w2List.Count));
                if (numSets > w2List.Count && ided)
                    w2List.AddRange(w2Source.Where(x => !x.Identified && x.iLvl >= 75 && x.H == 3).Take(numSets - w2List.Count));
            }

            // at this point we still need 0, 1 or 2 sets and it's time to look at 1x3 1hd weapons

            // look ahead to see how many 75 1x3s we want
            // can only have an odd number if there is a 60 that can match
            //
            var w1_60_possible = w1_60 + (ided ? w1_60Id : 0);
            var w1_75_possible = w1_75 + (ided ? w1_75Id : 0);
            if (w1_75_possible % 2 == 1 && w1_60_possible == 0)
                --w1_75_possible;

            if (!mustBe60 && w1_75_possible > 0 && StillNeed() > 0)
            {
                var prevCount = w1List.Count;
                w1List.AddRange(w1Source.Where(x => x.Identified == ided && x.iLvl >= 75).Take(Math.Min(w1_75_possible, StillNeed())));
                w1_75_possible -= (w1List.Count - prevCount);
            }
            if (!mustBe60 && w1_75_possible > 0 && ided && StillNeed() > 0)
                w1List.AddRange(w1Source.Where(x => !x.Identified && x.iLvl >= 75).Take(Math.Min(w1_75_possible, StillNeed())));

            // Special case to add in one 75 1x3 to a must be 60 recipe.
            //
            if (mustBe60 && w1_75_possible > 0 && w1_60_possible > 0 && StillNeed() > 1)
            {
                var prevCount = w1List.Count;
                w1List.AddRange(w1Source.Where(x => x.Identified == ided && x.iLvl >= 75).Take(1));
                if (prevCount == w1List.Count && ided)
                    w1List.AddRange(w1Source.Where(x => !x.Identified && x.iLvl >= 75).Take(1));
            }

            // check for 60 2x3s
            var wantW260 = StillNeed() / 2;
            if (wantW260 > 0)
            {
                w2List.AddRange(w2Source.Where(x => x.Identified == ided && x.iLvl < 75 && x.H == 3).Take(wantW260));
                wantW260 = StillNeed() / 2;
                if (wantW260 > 0 && ided)
                    w2List.AddRange(w2Source.Where(x => !x.Identified && x.iLvl < 75 && x.H == 3).Take(wantW260));
            }

            // last bit is 60 1x3s
            var wantW160 = StillNeed();
            if (wantW160 > 0)
                w1List.AddRange(w1Source.Where(x => x.Identified == ided && x.iLvl < 75).Take(wantW160));
            wantW160 = StillNeed();
            if (wantW160 > 0 && ided)
                w1List.AddRange(w1Source.Where(x => !x.Identified && x.iLvl < 75).Take(wantW160));

            if (StillNeed() > 0)
            {
                logger.Error($"Wanted {numSets} for weapons, but only got {w1List.Count} 1hd and {w2List.Count} 2hd, mustBe60:{mustBe60}, ided:{ided}");
            }

            destination.GetCategory(Cat.TwoHandWeapons).AddRange(w2List);
            destination.GetCategory(Cat.OneHandWeapons).AddRange(w1List);
        }
    }
}