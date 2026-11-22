using System;
using System.Collections.Generic;

namespace POGLib.Shops.Model
{
    /// <summary>
    /// The one weighted draw every shop choice goes through: a slot's item and a dungeon merchant's variant.
    /// Kept in one place so the distribution the unit tests measure is the one the game gets.
    /// </summary>
    internal static class WeightedPick
    {
        /// <summary>
        /// The index drawn for a uniform <paramref name="r"/> in [0, 1): entry i comes up with chance
        /// weights[i] / (sum of the usable weights). Zero, negative, NaN and infinite weights never come up
        /// (a shop file cannot contain them: the parser refuses them).
        /// Entry i owns the half-open interval [sum before i, sum before i + weights[i]) of r × total.
        /// Returns -1 when no weight is usable.
        /// </summary>
        public static int Index(IReadOnlyList<double> weights, double r)
        {
            if (weights == null) return -1;
            double total = 0;
            for (int i = 0; i < weights.Count; i++)
                if (Usable(weights[i])) total += weights[i];
            if (!(total > 0)) return -1;

            if (!(r > 0)) r = 0;   // negative or NaN
            double target = r * total, acc = 0;
            int last = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (!Usable(weights[i])) continue;
                acc += weights[i];
                last = i;
                if (target < acc) return i;
            }
            return last;   // r >= 1, or rounding at the top end
        }

        /// <summary>
        /// Draw with a weight function over any list. Returns the index, -1 when nothing can come up.
        /// </summary>
        public static int Index<T>(IReadOnlyList<T> items, Func<T, double> weight, double r)
        {
            if (items == null) return -1;
            var w = new double[items.Count];
            for (int i = 0; i < w.Length; i++) w[i] = weight(items[i]);
            return Index(w, r);
        }

        public static bool Usable(double w) => w > 0 && !double.IsInfinity(w);   // NaN > 0 is false
    }

    /// <summary>"Did you mean": the closest ids to a misspelt one, for log lines a modder acts on.</summary>
    internal static class NearestIds
    {
        /// <summary>Up to <paramref name="max"/> candidates by edit distance (ties: ordinal order).</summary>
        public static List<string> Of(string id, IEnumerable<string> candidates, int max = 3)
        {
            var scored = new List<(int d, string c)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (candidates != null && id != null)
                foreach (var c in candidates)
                    if (c != null && c != id && seen.Add(c)) scored.Add((Distance(id, c), c));
            scored.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : string.CompareOrdinal(a.c, b.c));
            var result = new List<string>();
            for (int i = 0; i < scored.Count && i < max; i++) result.Add(scored[i].c);
            return result;
        }

        /// <summary>Levenshtein distance (insert, delete, substitute; case-sensitive like the ids).</summary>
        public static int Distance(string a, string b)
        {
            a ??= ""; b ??= "";
            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int sub = prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                    cur[j] = Math.Min(sub, Math.Min(prev[j] + 1, cur[j - 1] + 1));
                }
                var t = prev; prev = cur; cur = t;
            }
            return prev[b.Length];
        }
    }
}
