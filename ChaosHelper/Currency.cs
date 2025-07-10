using System.Collections.Generic;
using System.Linq;

namespace ChaosHelper
{
    public class Currency
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public enum CurrencyOrdering
        {
            /// <summary>Sort by name.</summary>
            Name,
            /// <summary>Sort by value.</summary>
            Value,
            /// <summary>Sort by count.</summary>
            Count,
        }

        public string Name { get; private set; }
        public int Desired { get; private set; }
        public ItemDisplay ItemDisplay { get; private set; }
        public double ValueRatio { get; private set; }
        public double Value { get { return ValueRatio * CurrentCount; } }
        public bool CanFilterOn { get; private set; }
        public bool ShowBlock { get { return CanFilterOn && CurrentCount < Desired; } }
        public int CurrentCount { get; set; }
        
        public string DisplayString
        { get
            {
                return ValueRatio > 0.0
                                    ? $"{Name}; {CurrentCount}; {ValueRatio}; {Value}"
                                    : $"{Name}; {CurrentCount}";
            }
        }

        public static List<Currency> CurrencyList { get; private set; } = [];

        public static void ResetCounts()
        {
            foreach (var x in CurrencyList)
                x.CurrentCount = 0;
        }

        public static void ResetValueRatios()
        {
            foreach (var x in CurrencyList)
                x.ValueRatio = 0;
        }

        public static Currency SetValueRatio(string currency, double newRatio)
        {
            var c = CurrencyList.FirstOrDefault(x => x.Name == currency);
            if (c == null)
            {
                c = new Currency
                {
                    Name = currency,
                };
                CurrencyList.Add(c);
            }
            c.ValueRatio = newRatio;
            return c;
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

        static public void SetArray(System.Text.Json.JsonElement.ArrayEnumerator array)
        {
            CurrencyList.Clear();
            while (array.MoveNext())
                Add(array.Current);
            logger.Info($"currency: {CurrencyList.Count} entries");
        }

        static public void Add(System.Text.Json.JsonElement element)
        {
            var canFilterOn = true;

            var desired = element.GetIntOrDefault("desired", 0);
            if (desired <= 0) canFilterOn = false;

            var currencyName = element.GetStringOrDefault("c");
            if (string.IsNullOrWhiteSpace(currencyName)) canFilterOn = false;

            var itemDisplay = ItemDisplay.Parse(element);
            if (itemDisplay == null) canFilterOn = false;

            //logger.Info($"Adding currency entry for {currencyName} ({canFilterOn}): des={desired}");

            CurrencyList.Add(new Currency
            {
                Name = currencyName,
                Desired = desired,
                ItemDisplay = itemDisplay,
                CanFilterOn = canFilterOn,
                CurrentCount = int.MaxValue,
            });
        }

        public static void Sort(CurrencyOrdering ordering)
        {
            switch (ordering)
            {
                case CurrencyOrdering.Name:
                    CurrencyList.Sort((x, y) => x.Name.CompareTo(y.Name));
                    break;
                case CurrencyOrdering.Value:
                    CurrencyList.Sort((x, y) => CompareByValue(x, y));
                    break;
                case CurrencyOrdering.Count:
                    CurrencyList.Sort((x, y) => CompareByCount(x, y));
                    break;
            }
        }

        private static int CompareByCount(Currency x, Currency y)
        {
            if (x.CurrentCount == y.CurrentCount) return x.Name.CompareTo(y.Name);
            return x.CurrentCount.CompareTo(y.CurrentCount);
        }

        private static int CompareByValue(Currency x, Currency y)
        {
            if (x.Value == y.Value) return x.Name.CompareTo(y.Name);
            return x.Value.CompareTo(y.Value);
        }
    }
}
