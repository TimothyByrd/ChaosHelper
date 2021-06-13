using System.Collections.Generic;
using System.Linq;

namespace ChaosHelper
{
    public class Currency
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

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
        public static List<Currency> CurrencyList { get; private set; } = new List<Currency>();

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

            var fontSize = element.GetIntOrDefault("fontSize", 0).Clamp(0, 50);
            if (fontSize <= 10) canFilterOn = false;

            var textColor = element.GetStringOrDefault("text").CheckColorString();
            if (string.IsNullOrWhiteSpace(textColor)) canFilterOn = false;

            var borderColor = element.GetStringOrDefault("border").CheckColorString();
            if (string.IsNullOrWhiteSpace(borderColor)) canFilterOn = false;

            var backgroundColor = element.GetStringOrDefault("back").CheckColorString();
            if (string.IsNullOrWhiteSpace(backgroundColor)) canFilterOn = false;

            //logger.Info($"Adding currency entry for {currencyName} ({canFilterOn}): des={desired}");

            CurrencyList.Add(new Currency
            {
                Name = currencyName,
                Desired = desired,
                FontSize = fontSize,
                TextColor = textColor,
                BorderColor = borderColor,
                BackGroundColor = backgroundColor,
                CanFilterOn = canFilterOn,
                CurrentCount = int.MaxValue,
            });
        }
    }
}
