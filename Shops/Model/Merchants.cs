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
        /// What is sold, drawn by weight, no item twice on a stall; a place nothing is left for stays empty. An entry whose
        /// <see cref="PoolEntry.With"/> finds no place any more is passed over. An empty list: the stall's own goods.
        /// </summary>
        public List<PoolEntry> Goods { get; set; } = new List<PoolEntry>();

        /// <summary>
        /// The name over their head, for the host and every player who has POGShops (others see the game's own). Not set: the
        /// seller's own name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Their weapon, as the shiv of the game's sellers: hidden until somebody hurts them, then in the left hand; it is gone when they
        /// die. Only how it looks: the game's sellers stab equally hard with anything. Only with a <see cref="Look"/>, which takes the
        /// seller's own hand items off.
        /// </summary>
        public string LeftHand { get; set; }

        /// <summary>An item they hold in their right hand all the time. Only with a <see cref="Look"/>.</summary>
        public string RightHand { get; set; }

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
            Id = Id, Weight = Weight, Look = Look, FromFile = FromFile, Name = Name, LeftHand = LeftHand, RightHand = RightHand,
            Stalls = Stalls?.ToList(),
            Goods = Goods?.Select(g => g == null ? null : new PoolEntry
            {
                Item = g.Item, Weight = g.Weight, Endless = g.Endless, With = g.With?.ToList() ?? new List<string>(),
            }).ToList(),
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
        /// <summary>The longest name of a merchant (it stands over their head).</summary>
        public const int MaxName = 40;

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

        /// <summary>
        /// The goods along a stall with this many places: drawn by weight, none twice, each with what comes with it beside it. One
        /// whose companions find no place any more is passed over, so it never stands without them.
        /// </summary>
        public static List<PoolEntry> GoodsOrder(IReadOnlyList<PoolEntry> goods, int places, Func<double> next)
        {
            var left = goods.Where(g => g != null && WeightedPick.Usable(g.Weight)).ToList();
            var order = new List<PoolEntry>();
            while (left.Count > 0)
            {
                int i = WeightedPick.Index(left, g => g.Weight, next());
                if (i < 0) break;
                var group = Together(left[i], goods, order);
                left.RemoveAt(i);
                if (order.Count + group.Count > places) continue;
                int beside = order.FindIndex(g => group[0].With.Contains(g.Item));   // a companion that was drawn by itself before
                order.InsertRange(beside < 0 ? order.Count : beside, group);
                left.RemoveAll(group.Contains);
            }
            return order;
        }

        /// <summary>An entry and everything that comes with it, and with that in turn; what is out already or not in <paramref name="goods"/> is left out.</summary>
        public static List<PoolEntry> Together(PoolEntry entry, IReadOnlyList<PoolEntry> goods, ICollection<PoolEntry> outAlready)
        {
            var group = new List<PoolEntry>();
            void Add(PoolEntry g)
            {
                if (g == null || group.Contains(g) || outAlready.Contains(g)) return;
                group.Add(g);
                foreach (var id in g.With) Add(goods.FirstOrDefault(x => x != null && x.Item == id));
            }
            Add(entry);
            return group;
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
                if (m.Name != null && (m.Name.Trim().Length == 0 || m.Name.Length > MaxName || m.Name.Any(char.IsControl)))
                    e.Add($"{at}: Name must be 1 to {MaxName} characters in one line");
                m.LeftHand = Held(m.LeftHand, "LeftHand");
                m.RightHand = Held(m.RightHand, "RightHand");
                string Held(string item, string hand)
                {
                    if (string.IsNullOrEmpty(item)) return null;
                    item = ShopIds.Qualify(item, c.ModId);
                    if (item == ShopIds.Nothing || !ShopIds.TrySplit(item, out _, out _)) e.Add($"{at}: {hand} '{item}' is not an item id");
                    if (m.Look == null) e.Add($"{at}: {hand} is only for a merchant with a Look (the seller's own hand items come off with it)");
                    return item;
                }

                m.Goods ??= new List<PoolEntry>();
                var items = new HashSet<string>(StringComparer.Ordinal);
                foreach (var g in m.Goods)
                {
                    if (g == null || string.IsNullOrEmpty(g.Item)) { e.Add($"{at}: an entry of Goods has no Item"); continue; }
                    g.Item = ShopIds.Qualify(g.Item, c.ModId);
                    g.With = (g.With ?? new List<string>()).Select(w => string.IsNullOrEmpty(w) ? "" : ShopIds.Qualify(w, c.ModId)).ToList();
                    if (g.Item == ShopIds.Nothing || !ShopIds.TrySplit(g.Item, out _, out _)) e.Add($"{at}: '{g.Item}' is not an item id");
                    if (!items.Add(g.Item)) e.Add($"{at}: {g.Item} is in Goods twice (no item is sold twice on a stall)");
                }
                var companions = new HashSet<string>(StringComparer.Ordinal);
                foreach (var g in m.Goods.Where(g => g?.With != null))
                    foreach (var w in g.With)
                    {
                        companions.Add(w);
                        if (w == g.Item) e.Add($"{at}: {g.Item} comes With itself");
                        else if (!items.Contains(w)) e.Add($"{at}: {g.Item} comes With '{w}', which is not in Goods (put it there; Weight 0 if it only comes with another)");
                    }
                foreach (var g in m.Goods.Where(g => g != null && !string.IsNullOrEmpty(g.Item) && !WeightedPick.Usable(g.Weight)))
                    if (g.Weight != 0 || !companions.Contains(g.Item)) e.Add($"{at}: the Weight of {g.Item} must be a number > 0 (0 only for an item another one comes With)");
                if (m.Goods.Count > 0 && m.Goods.All(g => g != null && g.Weight == 0)) e.Add($"{at}: every Weight in Goods is 0, so nothing can come up");
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
