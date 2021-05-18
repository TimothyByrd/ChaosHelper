using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChaosHelper
{
    public class ItemMod
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private static readonly char[] modTagSplits = "\t;".ToCharArray();

        public static readonly List<ItemMod> PossibleMods = new List<ItemMod>();

        public Regex Regex { get; private set; }
        public int NumVars { get; private set; }
        public List<TagEntry> Tags { get; private set; } = new List<TagEntry>();

        public (bool, double) TryMatch(string s)
        {
            var match = Regex.Match(s);
            if (!match.Success)
                return (false, 0);

            if (NumVars == 0)
                return (true, 1);

            var v1 = double.Parse(match.Groups["v1"].Value);
            if (NumVars == 1)
                return (true, v1);

            if (NumVars == 2
                || NumVars == 4
                && string.Equals(match.Groups["v1"].Value, match.Groups["v3"].Value)
                && string.Equals(match.Groups["v2"].Value, match.Groups["v4"].Value))
            {
                var v2 = double.Parse(match.Groups["v2"].Value);
                return (true, (v1 + v2) / 2);

            }
            throw new Exception($"don't know how to handle {NumVars} match groups");
        }

        public static ItemMod FromString(string modFileLine)
        {
            if (string.IsNullOrWhiteSpace(modFileLine)) return null;
            modFileLine = modFileLine.Trim();
            if (modFileLine.StartsWith("#")) return null;
            var splits = modFileLine.Split(modTagSplits, StringSplitOptions.RemoveEmptyEntries);
            if (splits.Length < 2)
            {
                logger.Warn($"Invalid mod line '{modFileLine}'");
                return null;
            }
            var (regex, numVars) = GetRegex(splits[0]);
            if (regex == null)
            {
                logger.Warn($"Invalid mod line '{modFileLine}'");
                return null;
            }
            var result = new ItemMod
            {
                Regex = regex,
                NumVars = numVars,
            };
            foreach (var tagStr in splits.Skip(1))
            {
                if (tagStr.Trim().StartsWith("#")) break;
                var tagItem = TagEntry.FromString(tagStr);
                if (tagItem != null)
                    result.Tags.Add(tagItem);
            }
            return result;
        }

        private static (Regex, int) GetRegex(string rawStr)
        {
            if (string.IsNullOrWhiteSpace(rawStr))
                return (null, 0);

           var escaped = Regex.Escape(rawStr.Replace("\\n", "\n").Trim());

            var counter = 0;
            string NextValueGroup(Match match)
            {
                ++counter;
                return $"(?<v{counter}>\\d+\\.?\\d*)";
            }

            // replace each sring of digits (with decimal point)
            // with a (?<v1>\d+) group
            MatchEvaluator evaluator = new MatchEvaluator(NextValueGroup);
            var regexStr = Regex.Replace(escaped, @"\d+\.?\d*", evaluator);

            return (new Regex($"^{regexStr}$", RegexOptions.Compiled | RegexOptions.IgnoreCase), counter);
        }

        public static void ReadItemModFile(string fileName)
        {
            PossibleMods.Clear();
            foreach (var line in System.IO.File.ReadLines(fileName))
            {
                var mod = FromString(line);
                if (mod != null)
                    PossibleMods.Add(mod);
            }

            // special regexes for clipboard
            //
            PossibleMods.Add(new ItemMod
            {
                NumVars = 1,
                Regex = new Regex("^Energy Shield: (?<v1>\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                Tags = new List<TagEntry> { new TagEntry { Multiplier = 1, Tag = "PropES" } },
            });

            PossibleMods.Add(new ItemMod
            {
                NumVars = 1,
                Regex = new Regex("^Armour: (?<v1>\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                Tags = new List<TagEntry> { new TagEntry { Multiplier = 1, Tag = "PropArm" } },
            });

            PossibleMods.Add(new ItemMod
            {
                NumVars = 1,
                Regex = new Regex("^Evasion Rating: (?<v1>\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                Tags = new List<TagEntry> { new TagEntry { Multiplier = 1, Tag = "PropEva" } },
            });
        }
    }
}
