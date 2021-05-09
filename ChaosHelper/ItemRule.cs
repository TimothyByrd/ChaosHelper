using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChaosHelper
{
    public class ItemRule
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static readonly List<ItemRule> Rules = new List<ItemRule>();

        public string Name { get; private set; }
        public string ItemClass { get; private set; }
        private readonly List<RuleEntry> entries = new List<RuleEntry>();

        private static readonly char[] ruleLineSplits = "\t;,".ToCharArray();

        public static void ReadRuleFile(string fileName)
        {
            Rules.Clear();
            foreach (var line in System.IO.File.ReadLines(fileName))
            {
                var rule = FromString(line);
                if (rule != null)
                    Rules.Add(rule);
            }
        }

        public static ItemRule FromString(string ruleFileLine)
        {
            if (string.IsNullOrWhiteSpace(ruleFileLine)) return null;
            ruleFileLine = ruleFileLine.Trim();
            if (ruleFileLine.StartsWith("#")) return null;

            var splits = ruleFileLine.Split(ruleLineSplits, StringSplitOptions.RemoveEmptyEntries);
            if (splits.Length < 3)
            {
                logger.Warn($"Invalid rule line '{ruleFileLine}'");
                return null;
            }

            var result = new ItemRule
            {
                Name = splits[0].Trim(),
                ItemClass = splits[1].Trim(),
            };

            foreach (var s in splits.Skip(2))
            {
                var ruleEntry = RuleEntry.FromString(s);
                if (ruleEntry != null)
                    result.entries.Add(ruleEntry);
            }
            return result;
        }

        public bool Matches(ItemStats stats)
        {
            if (!string.Equals(ItemClass, stats.ItemClass, StringComparison.OrdinalIgnoreCase))
                return false;

            return entries.All(x => x.Matches(stats));

        }

        public class RuleEntry
        {
            private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

            private static readonly Regex entrySplitter = new Regex(
                @"^
                (?<term> \w+ (?: [:*](?: \d+\.?\d*))? )
                (?: (?<plus> \+)
                    (?<term2> \w+(?: [:*](?: \d+\.?\d*))? )
                )*
                (?<op> =|>=)
                (?<value> \d+\.?\d*)
                $",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

            public static void ShowMatch(string s)
            {
                var m = entrySplitter.Match(s.Replace(" ", ""));
                Console.WriteLine(s);
                if (!m.Success)
                {
                    Console.WriteLine("\tfailed");
                    return;
                }
                Console.WriteLine($"\t{m.Groups["term"].Value}");
                foreach (Capture capture in m.Groups["term2"].Captures)
                        Console.WriteLine($"\t{capture.Value}");
                Console.WriteLine($"\t{m.Groups["op"].Value}");
                Console.WriteLine($"\t{m.Groups["value"].Value}");
                Console.WriteLine();
            }

            public string compare;
            public double target;
            private readonly List<TagEntry> sumItems = new List<TagEntry>();

            public static RuleEntry FromString(string s)
            {
                var m = entrySplitter.Match(s.Replace(" ", ""));
                if (!m.Success)
                {
                    logger.Warn($"Invalid rule entry '{s}'");
                    return null;
                }

                var result = new RuleEntry
                {
                    target = double.Parse(m.Groups["value"].Value),
                    compare = m.Groups["op"].Value,
                };

                var entry = TagEntry.FromString(m.Groups["term"].Value);
                if (entry != null)
                    result.sumItems.Add(entry);

                foreach (Capture capture in m.Groups["term2"].Captures)
                {
                    entry = TagEntry.FromString(capture.Value);
                    if (entry != null)
                        result.sumItems.Add(entry);
                }
                return result;
            }

            public bool Matches(ItemStats stats)
            {
                var sum = 0.0;
                foreach (var entry in sumItems)
                {
                    var value = stats[entry.Tag];
                    if (value == 0 && entry.Tag.Contains("_frac", StringComparison.OrdinalIgnoreCase))
                    {
                        var baseTag = entry.Tag.Substring(0, entry.Tag.IndexOf("_frac", StringComparison.OrdinalIgnoreCase));
                        var (baseVal, fractured) = stats.GetTag(baseTag);
                        if (fractured)
                            value = baseVal;
                    }
                    sum += (value * entry.Multiplier);
                }

                return compare == ">" && sum > target || compare == ">=" && sum >= target;
            }
        }
    }
}
