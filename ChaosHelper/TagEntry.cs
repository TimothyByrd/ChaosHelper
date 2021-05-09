using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChaosHelper
{
    public class TagEntry
    {
        public string Tag;
        public double Multiplier;

        private static readonly char[] tagValSplit = ":* ".ToCharArray();

        public static TagEntry FromString(string s)
        {
            var splits = s.Split(tagValSplit, StringSplitOptions.RemoveEmptyEntries);
            if (splits.Length == 1)
                return new TagEntry { Tag = splits[0], Multiplier = 1.0, };
            if (splits.Length == 2 && double.TryParse(splits[1], out var m))
                return new TagEntry { Tag = splits[0], Multiplier = m, };
            return null;
        }
    }
}
