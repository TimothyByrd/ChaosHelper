using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ChaosHelper
{
    public class ItemSet
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static bool DetailedLogging { get; set; } = false;

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
            public bool ShouldShowInFilter { get; set; }
            public int MaxItemLevelToShow { get; set; }

            public void ResetCounts()
            {
                NumIded60 = 0;
                NumUnIded60 = 0;
                NumIded75 = 0;
                NumUnIded75 = 0;
            }
        }

        readonly Dictionary<Cat, List<ItemPosition>> itemsDict = [];
        readonly Dictionary<Cat, ItemCounts> countsDict = [];
        private string _ShowCatsSignature;

        public ItemSet()
        {
            foreach (var c in ItemClassForFilter.Iterator())
            {
                itemsDict[c.Category] = [];
                countsDict[c.Category] = new ItemCounts();
            }
        }

        public void RefreshCounts()
        {
            foreach (var c in ItemClassForFilter.Iterator())
            {
                var counts = countsDict[c.Category];
                counts.ResetCounts();
                foreach (var i in itemsDict[c.Category])
                {
                    if (i.Is75)
                    {
                        if (i.Identified)
                            ++counts.NumIded75;
                        else
                            ++counts.NumUnIded75;
                    }
                    else
                    {
                        if (i.Identified)
                            ++counts.NumIded60;
                        else
                            ++counts.NumUnIded60;
                    }
                }
            }
        }

        public void Sort()
        {
            foreach (var c in ItemClassForFilter.Iterator())
            {
                itemsDict[c.Category].Sort(ItemPosition.Compare);
            }
        }

        public bool HasAnyItems()
        {
            return itemsDict.Any(x => x.Value.Count != 0);
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

        public void CalculateClassesToShow()
        {
            var maxSets = Config.MaxSets;
            var maxIlvl = Config.MaxIlvl;
            if (maxIlvl >= 100 || maxIlvl < 60)
                maxIlvl = 0;
            var ignoreMaxSets = Config.IgnoreMaxSets;
            var ignoreMaxIlvl = Config.IgnoreMaxIlvl;
            var ignoreMaxSetsUnder75 = Config.IgnoreMaxSetsUnder75;
            _ShowCatsSignature = string.Empty;

            foreach (var c in ItemClassForFilter.Iterator())
            {
                if (c.Skip)
                    continue;

                var suffix = string.Empty;

                var counts = countsDict[c.Category];
                counts.MaxItemLevelToShow = ignoreMaxIlvl.Contains(c.CategoryStr, StringComparison.OrdinalIgnoreCase) ? 0 : maxIlvl;

                if (ignoreMaxSets.Contains(c.CategoryStr, StringComparison.OrdinalIgnoreCase))
                {
                    counts.ShouldShowInFilter = true;
                    continue;
                }
                else
                {
                    var wanted = maxSets;
                    var haveSoFar = counts.NumUnIded;
                    if (c.Category == Cat.Rings)
                        wanted *= 2;
                    else if (c.Category == Cat.OneHandWeapons)
                    {
                        wanted *= 2;
                        var num2hd = countsDict[Cat.TwoHandWeapons].NumUnIded;
                        if (num2hd > 0)
                        {
                            // can only use one 2x4 2hd-weapon per recipe inventory
                            var unIded2x4 = itemsDict[Cat.TwoHandWeapons].Count(x => !x.Identified && x.Is2x4);
                            haveSoFar += (num2hd - unIded2x4) * 2;
                            unIded2x4 = Math.Min(haveSoFar / 2, unIded2x4);
                            haveSoFar += unIded2x4 * 2;
                        }
                    }
                    counts.ShouldShowInFilter = haveSoFar < wanted;

                    if (!counts.ShouldShowInFilter && ignoreMaxSetsUnder75.Contains(c.CategoryStr, StringComparison.OrdinalIgnoreCase))
                    {
                        counts.ShouldShowInFilter = true;
                        counts.MaxItemLevelToShow = 74;
                        suffix = "74";
                    }
                }
                if (counts.ShouldShowInFilter)
                    _ShowCatsSignature = _ShowCatsSignature + c.CategoryStr + suffix;
            }
        }

        public bool SameClassesToShow(ItemSet other)
        {
            return string.Equals(_ShowCatsSignature, other._ShowCatsSignature);
        }

        public ItemCounts GetCounts(Cat category)
        {
            return countsDict[category];
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
                e.GetIntOrDefault("frameType", 0),
                tabIndex,
                quality,
                e,
                category));
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
            var logCounts = new List<string>();
            foreach (var c in ItemClassForFilter.Iterator())
            {
                if (c.Category == Cat.Junk || c.Category == Cat.TwoHandWeapons || c.Category == Cat.OffHand)
                    continue;

                var count = itemsDict[c.Category].Count(x => !x.Identified || ided);
                if (c.Category == Cat.Rings)
                {
                    count = itemsDict[c.Category].Count(x => x.Identified == ided) / 2;
                }
                else if (c.Category == Cat.OneHandWeapons)
                {
                    count /= 2;
                    var c2Short = itemsDict[Cat.TwoHandWeapons].Count(x => (!x.Identified || ided) && !x.Is2x4);
                    var c2Tall = itemsDict[Cat.TwoHandWeapons].Count(x => (!x.Identified || ided) && x.Is2x4);
                    count += c2Short;
                    count += Math.Min(Math.Max(1, count), c2Tall);
                }
                if (DetailedLogging)
                    logCounts.Add($"{c.Category}: {count}");
                possible = Math.Min(possible, count);
                if (possible == 0)
                    break;
            }

            if (DetailedLogging)
            {
                var countsStr = string.Join(", ", logCounts);
                logger.Info($"CountPossible({IdedStr(ided)}): {countsStr}");
            }
            return possible;
        }

        public static string IdedStr(bool ided)
        {
            return ided ? "ided" : "un-ided";
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
            bool CanMakeSingleSets()
            {
                return (chaosParanoiaLevel & 1) != 0;
            }

            bool IlvlDoesNotMatter()
            {
                return ided && (chaosParanoiaLevel & 4) != 0;
            }

            bool HoardIded60to74()
            {
                return (chaosParanoiaLevel & 2) != 0;
            }

            var result = new ItemSet();

            numSets = Math.Min(2, numSets);
            if (DetailedLogging)
                logger.Info($"trying for {numSets} sets - CanMakeSingleSets() = {CanMakeSingleSets()}, IlvlDoesNotMatter() = {IlvlDoesNotMatter()}, HoardIded60to74() = {HoardIded60to74()}");

            var optimizerList = new List<ChaosSlotOptimizer>
            {
                new ChaosSlotOptimizerWeapons(this, 1),
                new(this, Cat.BodyArmours, 2),
                new(this, Cat.Gloves, 3),
                new(this, Cat.Boots, 4),
                new(this, Cat.Helmets, 5),
                new(this, Cat.Belts, 6),
                new(this, Cat.Amulets, 7),
                new ChaosSlotOptimizerRings(this, 8),
            };

            foreach (var o in optimizerList)
                o.Calculate();

            // if any can't make 1 75, then
            // GetItems(mustbe60: false) for all
            var mustBe60 = Cat.Junk;
            var know60Cat = IlvlDoesNotMatter() || optimizerList.Any(x => x.Num75(ided) == 0);
            if (IlvlDoesNotMatter())
                logger.Info($"mustBe60 is {mustBe60} because IlvlDoesNotMatter()");
            else if (know60Cat)
            {
                var cats = string.Join(", ", optimizerList.Where(x => x.Num75(ided) == 0).Select(x => x.Category.ToString()).ToList());
                logger.Info($"mustBe60 is {mustBe60} because these cats have no {IdedStr(ided)} items over 75: {cats}");
            }

            if (!know60Cat)
            {
                //else if any can make n 60, then
                //     select the highest priority slot (biggest item) that can make n 60
                //     GetItems(mustbe60: true) for it, GetItems(mustbe60: false) for rest
                var bestFor60 = optimizerList.FirstOrDefault(x => x.Num60(ided) >= numSets);
                if (bestFor60 != null)
                {
                    mustBe60 = bestFor60.Category;
                    know60Cat = true;
                    logger.Info($"mustBe60 is {mustBe60} because it has {bestFor60.Num60(ided)} {IdedStr(ided)} 60s");
                }
            }

            if (!know60Cat && numSets > 1 && CanMakeSingleSets())
            {
                //else if (optimize and any can make 1 60)
                //     select the highest priority slot (biggest item) that can make 1 60
                //     GetItems(mustbe60: true) for it, GetItems(mustbe60: false) for rest
                var bestFor60 = optimizerList.FirstOrDefault(x => x.Num60(ided) >= 1);
                if (bestFor60 != null)
                {
                    mustBe60 = bestFor60.Category;
                    know60Cat = true;
                    numSets = 1;
                    logger.Info($"mustBe60 is {mustBe60} (and numSets = 1) because it has {bestFor60.Num60(ided)} {IdedStr(ided)} 60s");
                }
            }

            if (!know60Cat)
                logger.Info($"mustBe60 is {mustBe60} (and it's a regal recipe) because can't find any 60s");

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
                var xDivs40 = x.Quality == 10 || x.Quality == 8 || x.Quality == 5;
                var yDivs40 = y.Quality == 10 || y.Quality == 8 || y.Quality == 5;
                if (xDivs40 && !yDivs40)
                    return 1;
                if (yDivs40 && !xDivs40)
                    return -1;
                if (x.Quality > y.Quality)
                    return -1;
                if (y.Quality > x.Quality)
                    return 1;
                // Choose items that use more stash space first.
                if (x.H * x.W > y.H * y.W)
                    return -1;
                if (y.H * y.W > x.H * x.W)
                    return 1;
                if (x.X > y.X)
                    return 1;
                if (y.X > x.X)
                    return -1;
                if (x.Y > y.Y)
                    return 1;
                if (y.Y > x.Y)
                    return -1;
                return 0;
           }
        }

        public ItemSet MakeQualitySet()
        {
            var result = new ItemSet();

            var gems = itemsDict[Cat.Junk].Where(x => x.W == 1 && x.H == 1 && IsGem(x))
                .Where(x => x.Quality > 0).OrderBy(x => x, new QualityComparer());
            //ShowSet("gem", gems);
            var gemSet = MakeAQualitySet(gems, Config.QualityGemMapRecipeSlop);
            AddToResult(gemSet);

            var maps = itemsDict[Cat.Junk].Where(x => x.W == 1 && x.H == 1 && !IsGem(x))
                .Where(x => x.Quality > 0).OrderBy(x => x, new QualityComparer());
            var mapSet = MakeAQualitySet(maps, Config.QualityGemMapRecipeSlop);
            AddToResult(mapSet);

            var flasks = itemsDict[Cat.Junk].Where(x => x.W == 1 && x.H == 2)
                .Where(x => x.Quality > 0).OrderBy(x => x, new QualityComparer());
            //ShowSet("flask", flasks);
            var flaskSet = MakeAQualitySet(flasks, Config.QualityFlaskRecipeSlop);
            AddToResult(flaskSet);

            var weapons = itemsDict[Cat.OneHandWeapons].Concat(itemsDict[Cat.TwoHandWeapons])
                .Where(x => x.Quality > 0).OrderBy(x => x, new QualityComparer());
            var weaponSet = MakeAQualitySet(weapons, Config.QualityScrapRecipeSlop);
            AddToResult(weaponSet);

            var armours = itemsDict[Cat.BodyArmours].Concat(itemsDict[Cat.Helmets])
                .Concat(itemsDict[Cat.Gloves]).Concat(itemsDict[Cat.Boots])
                .Concat(itemsDict[Cat.OffHand])
                .Where(x => x.Quality > 0).OrderBy(x => x, new QualityComparer());
            var armourSet = MakeAQualitySet(armours, Config.QualityScrapRecipeSlop);
            AddToResult(armourSet);

            RemoveItems(result);

            foreach (var c in ItemClassForFilter.Iterator())
            {
                if (c.Category == Cat.Junk) continue;
                if (result.itemsDict[c.Category].Count == 0) continue;
                result.itemsDict[Cat.Junk].AddRange(result.itemsDict[c.Category]);
                result.itemsDict[c.Category].Clear();
            }

            return result;

            static bool IsGem(ItemPosition itemPos)
            {
                return itemPos.iLvl == 0;
            }

            void AddToResult(IEnumerable<ItemPosition> set)
            {
                if (set == null)
                    return;
                foreach (var item in set)
                    result.itemsDict[item.Category].Add(item);
            }
        }

        //private void ShowSet(string itemType, IEnumerable<ItemPosition> items)
        //{
        //    var qualities = string.Join(",", items.Select(x => x.Quality));
        //    logger.Debug($"{itemType} qualities: {qualities}");
        //}

        private static IEnumerable<ItemPosition> MakeAQualitySet(IEnumerable<ItemPosition> items, int allowedSlop)
        {
            for (int i = 0; i <= allowedSlop; ++i)
            {
                var result = MakeAQualitySetRecurse(items, 40, i);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static IEnumerable<ItemPosition> MakeAQualitySetRecurse(IEnumerable<ItemPosition> items, int minNeeded, int allowedSlop)
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

        struct ItemPair
        {
            public ItemPosition Item1 { get; set; }
            public ItemPosition Item2 { get; set; }

            public static int Compare(ItemPair ip1, ItemPair ip2)
            {
                if (ip1.Item1.TabIndex < ip2.Item1.TabIndex)
                    return -1;
                if (ip2.Item1.TabIndex < ip1.Item1.TabIndex)
                    return 1;
                if (ip1.Item2.TabIndex < ip2.Item2.TabIndex)
                    return -1;
                if (ip2.Item2.TabIndex < ip1.Item2.TabIndex)
                    return 1;
                return string.Compare(ip1.Item1.Name, ip2.Item1.Name);
            }
        }

        public List<string> LogMatchingNames(Dictionary<int, string> dumpTabDict)
        {
            string DumpTabName(int tabIndex)
            {
                return dumpTabDict.TryGetValue(tabIndex, out string value) ? value : "<unknown>";
            }

            var nameDict = new Dictionary<string, ItemPosition>();
            var pairs = new List<ItemPair>();
            foreach (var c in ItemClassForFilter.Iterator())
            {
                foreach (var item in itemsDict[c.Category])
                {
                    if (item.FrameType != 2) continue;
                    if (string.IsNullOrWhiteSpace(item.Name)) continue;
                    if (!nameDict.TryGetValue(item.Name, out ItemPosition value))
                        nameDict[item.Name] = item;
                    else
                    {
                        var otherItem = value;
                        var item1 = item.TabIndex < otherItem.TabIndex ? item : otherItem;
                        var item2 = item.TabIndex < otherItem.TabIndex ? otherItem : item;
                        pairs.Add(new ItemPair { Item1 = item1, Item2 = item2 });
                        nameDict.Remove(item.Name);
                    }
                }
            }

            pairs.Sort(ItemPair.Compare);
            List<string> result =
            [
                $"Total matches {pairs.Count}"
            ];
            foreach (var pair in pairs)
            {
                var tab1 = DumpTabName(pair.Item1.TabIndex);
                var tab2 = DumpTabName(pair.Item2.TabIndex);
                var s = $"tabs {tab1}, {tab2}: '{pair.Item1.Name}' ({pair.Item1.X},{pair.Item1.Y}), ({pair.Item2.X},{pair.Item2.Y})";
                result.Add(s);
                s = $"name match: {s}";
                logger.Info(s);
            }
            return result;
        }

        public List<string> CheckMods(Dictionary<int, string> dumpTabDict)
        {
            string DumpTabName(int tabIndex)
            {
                return dumpTabDict.TryGetValue(tabIndex, out string value) ? value : "<unknown>";
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

            static string RemoveTabIndex(string s)
            {
                return s.Substring(4);
            }

            foreach (var kv in interestingItems)
            {
                logger.Info(RemoveTabIndex(kv.Key));
                kv.Value.DumpValues();
            }
            return interestingItems.Select(x => RemoveTabIndex(x.Key)).ToList();
        }

        private static readonly Dictionary<BaseClass, string> equippedSlotDict = new()
        {
            { BaseClass.BodyArmour, "BodyArmour" },
            { BaseClass.Helmet, "Helm" },
            { BaseClass.Gloves, "Gloves" },
            { BaseClass.Boots, "Boots" },
            { BaseClass.OneHandWeapon, "Weapon" },
            { BaseClass.TwoHandWeapon, "Weapon" },
            { BaseClass.Belt, "Belt" },
            { BaseClass.Amulet, "Amulet" },
            { BaseClass.Ring, "Ring" }, // or Ring2
            { BaseClass.Shield, "Offhand" },
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
            if (rule.BaseClass == BaseClass.Ring && rule.Name.Contains('2'))
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

    public class ChaosSlotOptimizer(ItemSet source, Cat category, int priority)
    {
        protected static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public Cat Category { get; protected set; } = category;
        public int Priority { get; protected set; } = priority;
        public int CanMake { get; protected set; }
        public int CanMake60 { get; protected set; }
        public int CanMake75 { get; protected set; }
        public int CanMakeIded { get; protected set; }
        public int CanMake60Ided { get; protected set; }
        public int CanMake75Ided { get; protected set; }

        protected readonly ItemSet source = source;

        protected void CalculateInternal(Cat category)
        {
            foreach (var item in source.GetCategory(category))
            {
                ++CanMakeIded;
                if (item.Is75)
                    ++CanMake75Ided;
                else
                    ++CanMake60Ided;
                if (!item.Identified)
                {
                    ++CanMake;
                    if (item.Is75)
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
                list.AddRange(sourceList.Where(x => x.Identified == ided && x.Is75).Take(numSets));
            }

            // This block favors using up Ided items over using up ilvl 75+ items
            // skip to really optimize chaos vs regal at the expense of hoarding Ided ilvl 60 items.
            //
            var took60Ided = false;
            if (!HoardIded60to74(chaosParanoiaLevel) && list.Count < numSets && ided)
            {
                var want = numSets - list.Count;
                list.AddRange(sourceList.Where(x => x.Identified && !x.Is75).Take(want));
                took60Ided = true;
            }

            // un-Ided ilvl 75+ in a IDed recipe
            //
            if (!mustBe60 && list.Count < numSets && ided && CanMake75 > 0)
            {
                var want = numSets - list.Count;
                list.AddRange(sourceList.Where(x => !x.Identified && x.Is75).Take(want));
            }

            // now for ilvl 60 items
            //
            if (list.Count < numSets && (!took60Ided || !ided))
            {
                var want = numSets - list.Count;
                list.AddRange(sourceList.Where(x => x.Identified == ided && !x.Is75).Take(want));
            }
            if (list.Count < numSets && ided && CanMake60 > 0)
            {
                var want = numSets - list.Count;
                list.AddRange(sourceList.Where(x => !x.Identified && !x.Is75).Take(want));
            }

            if (list.Count < numSets)
                logger.Error($"Wanted {numSets} for {Category}, but only got {list.Count}, mustBe60:{mustBe60}, ided:{ided}");
            else if (list.Count == 2 && list[0].X == list[1].X && list[0].Y == list[1].Y)
                logger.Error($"Took same item twice for category {Category}, mustBe60:{mustBe60}, ided:{ided}");

            destination.GetCategory(Category).AddRange(list);
        }

        public int Num60(bool ided)
        {
            return ided ? CanMake60Ided : CanMake60;
        }

        public int Num75(bool ided)
        {
            return ided ? CanMake75Ided : CanMake75;
        }

        public static bool HoardIded60to74(int chaosParanoiaLevel)
        {
            return (chaosParanoiaLevel & 2) != 0;
        }
    }

    public class ChaosSlotOptimizerRings(ItemSet source, int priority) : ChaosSlotOptimizer(source, Cat.Rings, priority)
    {
        public override void Calculate()
        {
            CalculateInternal(Category);

            // for rings, IDed cannot mix in un-IDed.
            CanMakeIded -= CanMake;
            CanMake60Ided -= CanMake60;
            CanMake75Ided -= CanMake75;

            // because we need two per set.
            CanMake /= 2;
            if (CanMake60 != 1 || CanMake75 <= 0)
                CanMake60 /= 2;
            if (CanMake60 > 1 && CanMake75 > 1)
                CanMake60 = 1;
            CanMake75 /= 2;
            CanMakeIded /= 2;
            if (CanMake60Ided != 1 || CanMake75Ided <= 0)
                CanMake60Ided /= 2;
            if (CanMake60Ided > 1 && CanMake75Ided > 1)
                CanMake60Ided = 1;
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
            list.AddRange(sourceList.Where(x => x.Identified == ided && x.Is75).Take(want));

            want = wanted - list.Count;
            if (want > 0)
                list.AddRange(sourceList.Where(x => x.Identified == ided && !x.Is75).Take(want));

            if (list.Count < wanted )
                logger.Error($"Wanted {numSets} sets for {Category}, but only got {list.Count / 2}, mustBe60:{mustBe60}, ided:{ided}");

            destination.GetCategory(Category).AddRange(list);
        }
    }

    public class ChaosSlotOptimizerWeapons(ItemSet source, int priority) : ChaosSlotOptimizer(source, Cat.OneHandWeapons, priority)
    {
        private int w2H3_60;
        private int w2H3_75;
        private int w2H3_60Id;
        private int w2H3_75Id;
        private int w2H4_60;
        private int w2H4_75;
        private int w2H4_60Id;
        private int w2H4_75Id;

        public override void Calculate()
        {
            CalculateInternal(Cat.OneHandWeapons);

            // because we need two per set.
            CanMake /= 2;
            if (CanMake60 != 1 || CanMake75 <= 0) 
                CanMake60 /= 2;
            if (CanMake60 > 1 && CanMake75 > 1)
                CanMake60 = 1;
            CanMake75 /= 2;
            CanMakeIded /= 2;
            if (CanMake60Ided != 1 || CanMake75Ided <= 0)
                CanMake60Ided /= 2;
            if (CanMake60Ided > 1 && CanMake75Ided > 1)
                CanMake60Ided = 1;
            CanMake75Ided /= 2;

            foreach (var item in source.GetCategory(Cat.TwoHandWeapons))
            {
                if (!item.Is2x4 && !item.Is75 && !item.Identified) ++w2H3_60;
                if (!item.Is2x4 && item.Is75 && !item.Identified) ++w2H3_75;
                if (!item.Is2x4 && !item.Is75 && item.Identified) ++w2H3_60Id;
                if (!item.Is2x4 && item.Is75 && item.Identified) ++w2H3_75Id;
                if (item.Is2x4 && !item.Is75 && !item.Identified) ++w2H4_60;
                if (item.Is2x4 && item.Is75 && !item.Identified) ++w2H4_75;
                if (item.Is2x4 && !item.Is75 && item.Identified) ++w2H4_60Id;
                if (item.Is2x4 && item.Is75 && item.Identified) ++w2H4_75Id;
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
            if (numSets > 0)
                GetOneSet(destination, mustBe60, ided, chaosParanoiaLevel);
            if (numSets > 1)
                GetOneSet(destination, mustBe60, ided, chaosParanoiaLevel);
        }

        private void GetOneSet(ItemSet destination, bool mustBe60, bool ided, int chaosParanoiaLevel)
        {
            var w2Source = source.GetCategory(Cat.TwoHandWeapons);
            var w2Dest = destination.GetCategory(Cat.TwoHandWeapons);

            var w1Source = source.GetCategory(Cat.OneHandWeapons);
            var w1Dest = destination.GetCategory(Cat.OneHandWeapons);

            IEnumerable<ItemPosition> W2NotPicked() { return w2Source.Where(x => !w2Dest.Contains(x)); }
            IEnumerable<ItemPosition> W1NotPicked() { return w1Source.Where(x => !w1Dest.Contains(x)); }

            ItemPosition found2hd = null;

            // 2x4 weapons trump everything because they take up so much stash space
            //
            if (!w2Dest.Any(x => x.Is2x4))
            {
                if (!mustBe60)
                    found2hd = w2Source.FirstOrDefault(x => x.Identified == ided && x.Is75 && x.Is2x4);
                if (found2hd == null && !mustBe60 && ided && w2H4_75 > 0)
                    found2hd = w2Source.FirstOrDefault(x => !x.Identified && x.Is75 && x.Is2x4);
                found2hd ??= w2Source.FirstOrDefault(x => x.Identified == ided && !x.Is75 && x.Is2x4);
                if (found2hd == null && ided && w2H4_60 > 0)
                    found2hd = w2Source.FirstOrDefault(x => !x.Identified && !x.Is75 && x.Is2x4);
            }

            // next take ilvl 75+ 2x3 or 1x4 items
            //
            if (found2hd == null && !mustBe60)
                found2hd = W2NotPicked().FirstOrDefault(x => x.Identified == ided && x.Is75 && x.Is2x3);
            if (found2hd == null && !mustBe60)
                found2hd = W2NotPicked().FirstOrDefault(x => x.Identified == ided && x.Is75 && x.Is1x4);

            if (found2hd != null)
            {
                w2Dest.Add(found2hd);
                return;
            }

            IEnumerable<ItemPosition> w1Set = null;
            var max1hd75 = !mustBe60 ? 2 : w1Dest.Any(x => x.Is75) ? 0 : 1;

            // check for ided 1x3 items.
            //
            if (ided)
            {
                if (!HoardIded60to74(chaosParanoiaLevel))
                    w1Set = W1NotPicked().Where(x => x.Identified && x.Is75).Take(max1hd75)
                        .Concat(W1NotPicked().Where(x => x.Identified && !x.Is75))
                        .Concat(W1NotPicked().Where(x => !x.Identified && x.Is75).Take(max1hd75))
                        .Concat(W1NotPicked().Where(x => !x.Identified && !x.Is75))
                        ;
                else
                    w1Set = W1NotPicked().Where(x => x.Identified && x.Is75)
                        .Concat(W1NotPicked().Where(x => !x.Identified && x.Is75)).Take(max1hd75)
                        .Concat(W1NotPicked().Where(x => x.Identified && !x.Is75))
                        .Concat(W1NotPicked().Where(x => !x.Identified && !x.Is75))
                        ;
                if (w1Set.Count() >= 2
                    && w1Set.Take(2).Count(x => x.Is75) <= max1hd75
                    && w1Set.First().Identified)
                {
                    w1Dest.AddRange(w1Set.Take(2));
                    return;
                }
            }

            // next take ilvl 75+ 2x3 items - un-ided in ided recipe
            //
            if (!mustBe60 && ided)
                found2hd = W2NotPicked().FirstOrDefault(x => !x.Identified && x.Is75 && x.Is2x3);
            if (found2hd == null && !mustBe60 && ided)
                found2hd = W2NotPicked().FirstOrDefault(x => !x.Identified && x.Is75 && x.Is1x4);

            if (found2hd != null)
            {
                w2Dest.Add(found2hd);
                return;
            }

            if (ided)
                w1Set = W1NotPicked().Where(x => x.Identified && x.Is75)
                    .Concat(W1NotPicked().Where(x => !x.Identified && x.Is75))
                    .Take(max1hd75)
                    .Concat(W1NotPicked().Where(x => x.Identified && !x.Is75))
                    .Concat(W1NotPicked().Where(x => !x.Identified && !x.Is75));
            else
                w1Set = W1NotPicked().Where(x => !x.Identified && x.Is75).Take(max1hd75)
                    .Concat(W1NotPicked().Where(x => !x.Identified && !x.Is75));

            if (w1Set.Count() >= 2)
            {
                w1Dest.AddRange(w1Set.Take(2));
                return;
            }

            // check for 60 2x3s
            //
            found2hd = W2NotPicked().FirstOrDefault(x => x.Identified == ided && !x.Is75 && x.Is2x3);
            found2hd ??= W2NotPicked().FirstOrDefault(x => x.Identified == ided && !x.Is75 && x.Is1x4);
            if (found2hd == null && ided)
                found2hd = W2NotPicked().FirstOrDefault(x => !x.Identified && !x.Is75 && x.Is2x3);
            if (found2hd == null && ided)
                found2hd = W2NotPicked().FirstOrDefault(x => !x.Identified && !x.Is75 && x.Is1x4);
            if (found2hd != null)
            {
                w2Dest.Add(found2hd);
                return;
            }

            logger.Error($"Wanted 2 1hd weapons but failed - mustBe60:{mustBe60}, ided:{ided}");
        }
    }
}