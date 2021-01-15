using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        static public void SetArray(System.Text.Json.JsonElement.ArrayEnumerator array)
        {
            CurrencyList.Clear();
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

            logger.Info($"Adding currency entry for {currencyName}: des={desired}, ratio={valueRatio}");

            CurrencyList.Add(new Currency
            {
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
}
