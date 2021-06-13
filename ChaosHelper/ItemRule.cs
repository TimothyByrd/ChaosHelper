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

        public static List<ItemRule> Rules { get; private set; } = new List<ItemRule>();
        public static bool HaveDynamic { get; private set; }

        public string Name { get; private set; }
        public BaseClass BaseClass { get; private set; }
        public bool IsDynamic { get; private set; }
        
        private readonly List<RuleEntry> entries = new();

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
            HaveDynamic = Rules.Any(x => x.IsDynamic);
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
                BaseClass = splits[1].Trim().ToBaseClass(),
            };

            foreach (var s in splits.Skip(2))
            {
                var ruleEntry = RuleEntry.FromString(s);
                if (ruleEntry != null)
                    result.entries.Add(ruleEntry);
            }

            result.IsDynamic = result.entries.Any(x => x.isDynamic);
            
            return result;
        }

        public bool Matches(ItemStats stats)
        {
            if (BaseClass == stats.BaseClass || BaseClass == BaseClass.Any)
                return entries.All(x => x.Matches(stats));
            return false;
        }

        public void UpdateDynamic(ItemStats stats)
        {
            foreach (var entry in entries.Where(x => x.isDynamic))
            {
                entry.UpdateDynamic(stats);
            }
        }

        public class RuleEntry
        {
            private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

            private static readonly Regex entrySplitter = new(
                @"^
                (?<term> \w+ (?: [:*](?: \d+\.?\d*))? )
                (?: (?<plus> \+)
                    (?<term2> \w+(?: [:*](?: \d+\.?\d*))? )
                )*
                (?<op> >|>=|<|<=|=|==)
                (?: (?<value> \d+\.?\d*)|(?<dynamic> X (?: [:*](?: \d+\.?\d*))? ) )
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
            public bool isDynamic;
            public double dynamicFactor;

            private readonly List<TagEntry> sumItems = new();

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
                    compare = m.Groups["op"].Value,
                    isDynamic = m.Groups["dynamic"].Success,
                    dynamicFactor = 1.0,
                };

                if (!result.isDynamic)
                    result.target = double.Parse(m.Groups["value"].Value);
                else
                {
                    var dynValue = m.Groups["dynamic"].Value;
                    result.dynamicFactor = dynValue.Length > 2 ? double.Parse(dynValue.Substring(2)) : 1.0;
                }

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
                double sum = GetSum(stats);

                return compare switch
                {
                    ">" => sum > target,
                    "<" => sum < target,
                    "<=" => sum <= target,
                    "==" => sum == target,
                    "=" => sum == target,
                    _ => sum >= target,
                };
            }

            private double GetSum(ItemStats stats)
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

                return sum;
            }

            public void UpdateDynamic(ItemStats stats)
            {
                double sum = GetSum(stats);
                target = sum * dynamicFactor;
            }
        }
    }
}
