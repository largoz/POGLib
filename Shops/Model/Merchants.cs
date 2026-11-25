using System;
using System.Collections.Generic;
using System.Linq;

namespace POGLib.Shops.Model
{
    /// <summary>
    /// A mod's dungeon merchants, as one class. The host draws a class first, by its chance, and a merchant inside it
    /// after, so a mod with ten merchants comes up as often as a mod with one. One mod = one class.
    /// Register it with <see cref="ShopsApi.AddMerchants"/> from OnInitializeMelon.
    /// </summary>
    public sealed class MerchantClass
    {
        /// <summary>Your mod's id, the same as for your items; it is the class.</summary>
        public string ModId { get; set; }

        /// <summary>The name the host sees in the settings; your mod's id when not set.</summary>
        public string Title { get; set; }

        /// <summary>
        /// How often one of these merchants keeps a stall instead of the game's own, 0 to 0.25 (a quarter of the stalls
        /// they may take). All mods together stay under the host's ceiling; the host may change both.
        /// </summary>
        public double Chance { get; set; } = MerchantDraw.MaxAsk;

        public List<MerchantDef> Merchants { get; set; } = new List<MerchantDef>();

        internal string Name => string.IsNullOrWhiteSpace(Title) ? ModId : Title.Trim();

        /// <summary>What was checked stays what is used: the library keeps a copy, so a change after registering does nothing.</summary>
        internal MerchantClass Copy() => new MerchantClass
        {
            ModId = ModId, Title = Title, Chance = Chance,
            Merchants = Merchants?.Select(m => m?.Copy()).ToList(),
        };
    }

    /// <summary>One merchant of a <see cref="MerchantClass"/>.</summary>
    public sealed class MerchantDef
    {
        /// <summary>Local id, unique in the class: "BerryMod:trader" is "trader" of BerryMod.</summary>
        public string Id { get; set; }

        /// <summary>How often this merchant comes up, against the others of the same class only.</summary>
        public double Weight { get; set; } = 1;

        /// <summary>Whose stalls this merchant may take: Antique, Curiosa, Foodlover, MysticTent, Smith, Tinkerer.</summary>
        public List<string> Stalls { get; set; } = new List<string>();

        /// <summary>
        /// What is sold, drawn by weight, no item twice on a stall; a place nothing is left for stays empty. An empty list:
        /// the stall's own goods.
        /// </summary>
        public List<PoolEntry> Goods { get; set; } = new List<PoolEntry>();

        /// <summary>
        /// The merchant's look as the game's own outfit text (the JSON of a goblin's parts), asked for on the host when a stall is being decided.
        /// Null or empty from it: the merchant cannot come now and takes no part in the draw. Not set: the seller of the
        /// stall keeps their own look.
        /// </summary>
        public Func<string> Look { get; set; }

        /// <summary>From a host's shop file ("sellers"): stocked slot by slot through the catalog, not from Goods.</summary>
        internal bool FromFile { get; set; }

        internal MerchantDef Copy() => new MerchantDef
        {
            Id = Id, Weight = Weight, Look = Look, FromFile = FromFile,
            Stalls = Stalls?.ToList(),
            Goods = Goods?.Select(g => g == null ? null : new PoolEntry { Item = g.Item, Weight = g.Weight }).ToList(),
        };
    }

    /// <summary>Who keeps a stall: a mod's merchant, or the game's own (<see cref="Merchant"/> is null).</summary>
    internal sealed class MerchantOutcome
    {
        public MerchantClass Class { get; internal set; }
        public MerchantDef Merchant { get; internal set; }
        /// <summary>The share of this stall each class had (those that could come), for the log.</summary>
        public List<(MerchantClass c, double share)> Shares { get; } = new List<(MerchantClass, double)>();
        public double GameShare => Math.Max(0, 1 - Shares.Sum(s => s.share));
    }

    /// <summary>
    /// Who keeps a dungeon merchant's stall (the user's design, 2026-11-25): (1) the classes' chances add up to a ceiling,
    /// over it all shrink in proportion and the game's own merchant keeps the rest; (2) a class is drawn by its share;
    /// (3) a merchant of it by weights that mean nothing outside the class. Only classes with a merchant that can keep this
    /// stall now take part, so one that cannot leaves its share to nobody and shrinks nobody.
    /// </summary>
    internal static class MerchantDraw
    {
        /// <summary>The most of the stalls all mods together take, unless the host says otherwise: the game's own merchants keep a quarter at least (the user, 2026-11-25).</summary>
        public const double StandardCeiling = 0.75;
        /// <summary>The most a class may ask for (the host may give it more).</summary>
        public const double MaxAsk = 0.25;

        public static readonly string[] Stalls = { "Antique", "Curiosa", "Foodlover", "MysticTent", "Smith", "Tinkerer" };

        public static bool IsShare(double x) => x >= 0 && x <= 1;   // false for NaN

        /// <summary>What each class gets of a stall for the chances they have: their own, or less in proportion when together they pass the ceiling.</summary>
        public static double[] Shares(IReadOnlyList<double> chances, double ceiling)
        {
            var shares = new double[chances.Count];
            double total = 0;
            for (int i = 0; i < shares.Length; i++)
            {
                shares[i] = IsShare(chances[i]) ? chances[i] : 0;
                total += shares[i];
            }
            if (!IsShare(ceiling)) ceiling = 0;
            if (total > ceiling)
                for (int i = 0; i < shares.Length; i++) shares[i] = shares[i] * ceiling / total;
            return shares;
        }

        /// <param name="chance">the class's chance: the host's setting, else what the mod asks.</param>
        /// <param name="ready">whether this merchant can keep the stall now (look and goods); not asked of a class that is off.</param>
        /// <param name="next">uniform numbers in [0, 1); two are taken.</param>
        public static MerchantOutcome Draw(IReadOnlyList<MerchantClass> classes, string stall, double ceiling,
                                           Func<MerchantClass, double> chance, Func<MerchantClass, MerchantDef, bool> ready, Func<double> next)
        {
            var outcome = new MerchantOutcome();
            double rollClass = next(), rollMerchant = next();
            if (!(ceiling > 0)) return outcome;   // the host has mods' merchants off: nobody is asked anything
            var at = new List<(MerchantClass c, List<MerchantDef> merchants)>();
            var chances = new List<double>();
            foreach (var c in classes)
            {
                double p = chance(c);
                if (!(p > 0)) continue;
                var can = c.Merchants.Where(m => WeightedPick.Usable(m.Weight) && m.Stalls.Contains(stall) && ready(c, m)).ToList();
                if (can.Count == 0) continue;
                at.Add((c, can));
                chances.Add(p);
            }
            var shares = Shares(chances, ceiling);
            for (int i = 0; i < shares.Length; i++) outcome.Shares.Add((at[i].c, shares[i]));

            // The last entry is the game's own merchant.
            var weights = shares.Concat(new[] { outcome.GameShare }).ToArray();
            int k = WeightedPick.Index(weights, rollClass);
            if (k < 0 || k >= shares.Length) return outcome;
            int m2 = WeightedPick.Index(at[k].merchants, m => m.Weight, rollMerchant);
            if (m2 < 0) return outcome;
            outcome.Class = at[k].c;
            outcome.Merchant = at[k].merchants[m2];
            return outcome;
        }

        /// <summary>The goods in the order they are put out: drawn by weight, none twice.</summary>
        public static List<string> GoodsOrder(IEnumerable<PoolEntry> goods, Func<double> next)
        {
            var left = goods.Where(g => g != null && WeightedPick.Usable(g.Weight)).ToList();
            var order = new List<string>();
            while (left.Count > 0)
            {
                int i = WeightedPick.Index(left, g => g.Weight, next());
                if (i < 0) break;
                order.Add(left[i].Item);
                left.RemoveAt(i);
            }
            return order;
        }

        /// <summary>
        /// Mistakes in a class; it is used only when there are none. Item ids become full ones ("Item" means "ModId:Item").
        /// </summary>
        public static List<string> Validate(MerchantClass c)
        {
            var e = new List<string>();
            if (c == null) { e.Add("the class is null"); return e; }
            if (!ShopIds.IsValidModId(c.ModId)) e.Add($"ModId '{c.ModId}' is not a mod id (letters, digits, '_', '-', '.')");
            if (!(c.Chance >= 0 && c.Chance <= MaxAsk)) e.Add($"Chance {c.Chance} must be from 0 to {MaxAsk} (the host may give more)");
            if (c.Merchants == null || c.Merchants.Count == 0) { e.Add("there are no Merchants"); return e; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < c.Merchants.Count; i++)
            {
                var m = c.Merchants[i];
                if (m == null || string.IsNullOrEmpty(m.Id) || m.Id.IndexOf(':') >= 0 || m.Id.IndexOf('#') >= 0) { e.Add($"merchant {i + 1}: Id must be a local id without ':' and '#'"); continue; }
                string at = $"merchant '{m.Id}'";
                if (!ids.Add(m.Id)) e.Add($"{at} is there twice");
                if (!(m.Weight >= 0) || double.IsInfinity(m.Weight)) e.Add($"{at}: Weight must be a number >= 0");
                if (m.Stalls == null || m.Stalls.Count == 0) e.Add($"{at}: Stalls is empty; name the stalls: {string.Join(", ", Stalls)}");
                else
                    foreach (var s in m.Stalls)
                        if (!Stalls.Contains(s))
                            e.Add($"{at}: no stall '{s}'; there are {string.Join(", ", Stalls)}");
                m.Goods ??= new List<PoolEntry>();
                var items = new HashSet<string>(StringComparer.Ordinal);
                foreach (var g in m.Goods)
                {
                    if (g == null || string.IsNullOrEmpty(g.Item)) { e.Add($"{at}: an entry of Goods has no Item"); continue; }
                    g.Item = ShopIds.Qualify(g.Item, c.ModId);
                    if (g.Item == ShopIds.Nothing || !ShopIds.TrySplit(g.Item, out _, out _)) e.Add($"{at}: '{g.Item}' is not an item id");
                    if (!WeightedPick.Usable(g.Weight)) e.Add($"{at}: the Weight of {g.Item} must be a number > 0");
                    if (!items.Add(g.Item)) e.Add($"{at}: {g.Item} is in Goods twice (no item is sold twice on a stall)");
                }
            }
            return e;
        }

        /// <summary>
        /// The dungeon merchants of the hosts' own shop files ("sellers") as classes: one per mod id, the file's weight is the
        /// weight inside the class.
        /// </summary>
        public static List<MerchantClass> FromFiles(ShopCatalog catalog, string presetPrefix)
        {
            var classes = new List<MerchantClass>();
            if (catalog == null) return classes;
            foreach (var s in catalog.Sellers)
            {
                var c = classes.FirstOrDefault(x => x.ModId == s.Mod);
                if (c == null) classes.Add(c = new MerchantClass { ModId = s.Mod });
                string preset = s.Def.Preset ?? "";
                c.Merchants.Add(new MerchantDef
                {
                    Id = s.Def.Id,
                    Weight = s.Def.Weight,
                    Stalls = new List<string> { preset.StartsWith(presetPrefix, StringComparison.Ordinal) ? preset.Substring(presetPrefix.Length) : preset },
                    FromFile = true,
                });
            }
            return classes;
        }
    }
}
