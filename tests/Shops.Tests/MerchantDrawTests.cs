using System;
using System.Collections.Generic;
using System.Linq;
using POGLib.Shops.Model;
using Xunit;

namespace POGLib.Shops.Tests
{
    /// <summary>
    /// Who keeps a dungeon merchant's stall. The expected numbers are worked out by hand from the rule (the ceiling, then the
    /// class, then the merchant), never by the code under test.
    /// </summary>
    public class MerchantDrawTests
    {
        const string Stall = "Curiosa";

        static MerchantClass Class(string mod, double chance, params (string id, double weight)[] merchants) => new MerchantClass
        {
            ModId = mod,
            Chance = chance,
            Merchants = merchants.Select(m => new MerchantDef { Id = m.id, Weight = m.weight, Stalls = new List<string> { Stall } }).ToList(),
        };

        /// <summary>Each outcome's frequency over many draws; "" is the game's own merchant.</summary>
        static Dictionary<string, double> Frequencies(IReadOnlyList<MerchantClass> classes, double ceiling = 0.75, string stall = Stall,
                                                      Func<MerchantClass, double> chance = null, Func<MerchantClass, MerchantDef, bool> ready = null)
        {
            const int N = 100_000;
            var rng = new Random(20261125);
            var count = new Dictionary<string, int>();
            for (int i = 0; i < N; i++)
            {
                var o = MerchantDraw.Draw(classes, stall, ceiling, chance ?? (c => c.Chance), ready ?? ((c, m) => true), rng.NextDouble);
                string who = o.Merchant == null ? "" : o.Class.ModId + ":" + o.Merchant.Id;
                count[who] = count.TryGetValue(who, out var n) ? n + 1 : 1;
            }
            return count.ToDictionary(kv => kv.Key, kv => kv.Value / (double)N);
        }

        static void Near(double expected, Dictionary<string, double> f, string who)
        {
            double got = f.TryGetValue(who, out var x) ? x : 0;
            Assert.True(Math.Abs(got - expected) < 0.008, $"'{who}': expected {expected:0.####}, got {got:0.####}");   // over 5 sigma at 100 000 draws (largest sigma 0.0016)
        }

        // ---- the ceiling ----

        [Theory]
        [InlineData(new[] { 0.25 }, 0.25, new[] { 0.25 })]
        [InlineData(new[] { 0.25, 0.25 }, 0.25, new[] { 0.125, 0.125 })]
        [InlineData(new[] { 0.05, 0.10 }, 0.25, new[] { 0.05, 0.10 })]                  // under the ceiling: as asked
        [InlineData(new[] { 0.25, 0.05 }, 0.15, new[] { 0.125, 0.025 })]                // in proportion
        [InlineData(new[] { 0.25, 0.25, 0.25, 0.25, 0.25 }, 0.25, new[] { 0.05, 0.05, 0.05, 0.05, 0.05 })]
        [InlineData(new[] { 0.25, 0.25, 0.25 }, 0.75, new[] { 0.25, 0.25, 0.25 })]      // just at the ceiling: as asked
        [InlineData(new[] { 0.25, 0.25, 0.25, 0.25, 0.25 }, 0.75, new[] { 0.15, 0.15, 0.15, 0.15, 0.15 })]
        [InlineData(new[] { 1.0 }, 1.0, new[] { 1.0 })]
        [InlineData(new[] { 1.0, 1.0 }, 1.0, new[] { 0.5, 0.5 })]
        [InlineData(new[] { 0.25 }, 0.0, new[] { 0.0 })]
        public void SharesStayUnderTheCeiling(double[] chances, double ceiling, double[] expected)
        {
            var s = MerchantDraw.Shares(chances, ceiling);
            Assert.Equal(expected.Length, s.Length);
            for (int i = 0; i < s.Length; i++) Assert.Equal(expected[i], s[i], 12);
        }

        [Fact]
        public void ABadChanceOrCeilingIsNoChance()
        {
            Assert.Equal(new[] { 0, 0.1, 0, 0 }, MerchantDraw.Shares(new[] { double.NaN, 0.1, -1, 2 }, 0.25));
            Assert.Equal(new[] { 0.0 }, MerchantDraw.Shares(new[] { 0.25 }, double.NaN));
        }

        [Fact]
        public void TheStandardCeilingLeavesTheGameAQuarter() => Assert.Equal(0.75, MerchantDraw.StandardCeiling);   // the user, 2026-11-25

        [Theory]
        [InlineData(1, 0.25, 0.75)]     // one mod: as it asks, the rest is the game's
        [InlineData(2, 0.25, 0.50)]
        [InlineData(3, 0.25, 0.25)]     // the ceiling is reached
        [InlineData(5, 0.15, 0.25)]     // past it the mods share three quarters, the game's merchant keeps its quarter
        [InlineData(10, 0.075, 0.25)]
        public void TheGamesOwnMerchantKeepsAQuarterHoweverManyModsThereAre(int mods, double each, double games)
        {
            var f = Frequencies(Enumerable.Range(0, mods).Select(i => Class("M" + i, 0.25, ("a", 1))).ToList(), MerchantDraw.StandardCeiling);
            Near(games, f, "");
            Near(each, f, "M0:a");
            Near(each, f, $"M{mods - 1}:a");   // the last as often as the first
        }

        // ---- the class ----

        [Fact]
        public void AModWithManyMerchantsComesUpAsOftenAsAModWithOne()
        {
            var a = Class("A", 0.25, ("one", 1));
            var b = Class("B", 0.25, Enumerable.Range(0, 20).Select(i => ("m" + i, 100.0)).ToArray());
            var f = Frequencies(new[] { a, b }, ceiling: 0.25);
            Near(0.125, f, "A:one");
            Near(0.125, f.Where(kv => kv.Key.StartsWith("B:")).GroupBy(_ => "B").ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value)), "B");
            Near(0.75, f, "");
        }

        [Fact]
        public void ARareClassStaysRare()
        {
            var f = Frequencies(new[] { Class("Rare", 0.02, ("a", 1000)) });
            Near(0.02, f, "Rare:a");
            Near(0.98, f, "");
        }

        [Fact]
        public void TheHostsSettingReplacesTheAsk()
        {
            var a = Class("A", 0.25, ("a", 1)); var b = Class("B", 0.25, ("b", 1));
            var f = Frequencies(new[] { a, b }, ceiling: 1, chance: c => c.ModId == "A" ? 0.6 : 0);
            Near(0.6, f, "A:a"); Near(0, f, "B:b"); Near(0.4, f, "");
        }

        [Fact]
        public void EveryStallIsAModsWhenTheHostSaysSo()
        {
            var f = Frequencies(new[] { Class("A", 0.25, ("a", 1)) }, ceiling: 1, chance: _ => 1);
            Assert.Equal(1.0, f["A:a"]);
        }

        // ---- inside the class ----

        [Fact]
        public void WeightsDivideTheClassesShareOnly()
        {
            var f = Frequencies(new[] { Class("N", 0.25, ("plain", 5), ("mask", 3), ("bandana", 2), ("off", 0)) });
            Near(0.125, f, "N:plain"); Near(0.075, f, "N:mask"); Near(0.05, f, "N:bandana"); Near(0, f, "N:off"); Near(0.75, f, "");
        }

        // ---- who takes part ----

        [Fact]
        public void AClassThatCannotKeepThisStallShrinksNobody()
        {
            var a = Class("A", 0.10, ("a", 1)); var b = Class("B", 0.10, ("b", 1)); var c = Class("C", 0.10, ("c", 1));
            c.Merchants[0].Stalls = new List<string> { "Smith" };
            var f = Frequencies(new[] { a, b, c }, ceiling: 0.25);        // 0.1 + 0.1 is under 0.25; with C it would be 0.0833 each
            Near(0.10, f, "A:a"); Near(0.10, f, "B:b"); Near(0, f, "C:c"); Near(0.80, f, "");
            Near(0.10, Frequencies(new[] { a, b, c }, ceiling: 0.25, stall: "Smith"), "C:c");
        }

        [Fact]
        public void AMerchantThatCannotComeLeavesTheClassToTheOthers()
        {
            var a = Class("A", 0.25, ("x", 9), ("y", 1));
            var f = Frequencies(new[] { a }, ready: (c, m) => m.Id != "x");
            Near(0.25, f, "A:y"); Near(0, f, "A:x"); Near(0.75, f, "");
        }

        [Fact]
        public void NobodyReadyIsTheGamesOwnMerchant()
        {
            var o = MerchantDraw.Draw(new[] { Class("A", 0.25, ("a", 1)) }, Stall, 1, _ => 1, (c, m) => false, () => 0);
            Assert.Null(o.Merchant); Assert.Null(o.Class); Assert.Empty(o.Shares); Assert.Equal(1, o.GameShare);
        }

        [Fact]
        public void NoClassesIsTheGamesOwnMerchant() =>
            Assert.Null(MerchantDraw.Draw(new MerchantClass[0], Stall, 1, _ => 1, (c, m) => true, () => 0).Merchant);

        [Fact]
        public void AClassThatIsOffIsNotAskedAnything()
        {
            int asked = 0;
            MerchantDraw.Draw(new[] { Class("A", 0.25, ("a", 1)) }, Stall, 0.25, _ => 0, (c, m) => { asked++; return true; }, () => 0);
            MerchantDraw.Draw(new[] { Class("A", 0.25, ("a", 1)) }, Stall, 0, _ => 1, (c, m) => { asked++; return true; }, () => 0);
            Assert.Equal(0, asked);
        }

        [Fact]
        public void TheLogsSharesAreWhatWasDrawn()
        {
            var o = MerchantDraw.Draw(new[] { Class("A", 0.25, ("a", 1)), Class("B", 0.05, ("b", 1)) }, Stall, 0.15, c => c.Chance, (c, m) => true, () => 0.99);
            Assert.Equal(new[] { 0.125, 0.025 }, o.Shares.Select(s => Math.Round(s.share, 12)));
            Assert.Equal(0.85, o.GameShare, 12);
            Assert.Null(o.Merchant);
        }

        [Theory]
        [InlineData(0.0, "A")]
        [InlineData(0.0999, "A")]
        [InlineData(0.1001, "B")]
        [InlineData(0.1999, "B")]
        [InlineData(0.2001, "")]
        [InlineData(0.9999, "")]
        public void TheRollFallsIntoTheSharesInOrder(double roll, string who)
        {
            var rolls = new Queue<double>(new[] { roll, 0.5 });
            var o = MerchantDraw.Draw(new[] { Class("A", 0.1, ("a", 1)), Class("B", 0.1, ("b", 1)) }, Stall, 0.25, c => c.Chance, (c, m) => true, rolls.Dequeue);
            Assert.Equal(who, o.Class?.ModId ?? "");
        }

        // ---- goods ----

        static PoolEntry E(string item, double weight, params string[] with) => new PoolEntry { Item = item, Weight = weight, With = with.ToList() };
        static string[] Names(IEnumerable<PoolEntry> order) => order.Select(g => g.Item).ToArray();

        [Fact]
        public void GoodsComeOnceEachAndTheFirstByWeight()
        {
            var goods = new[] { E("a", 3), E("b", 1), E("c", 0) };
            var rng = new Random(7);
            int aFirst = 0; const int N = 100_000;
            for (int i = 0; i < N; i++)
            {
                var o = Names(MerchantDraw.GoodsOrder(goods, 5, rng.NextDouble));
                Assert.Equal(new[] { "a", "b" }, o.OrderBy(x => x, StringComparer.Ordinal));
                if (o[0] == "a") aFirst++;
            }
            Assert.InRange(aFirst / (double)N, 0.743, 0.757);
        }

        [Fact]
        public void NoMoreGoodsThanPlaces()
        {
            var goods = new[] { E("a", 1), E("b", 1), E("c", 1) };
            Assert.Equal(new[] { "a", "b" }, Names(MerchantDraw.GoodsOrder(goods, 2, () => 0)));
            Assert.Empty(MerchantDraw.GoodsOrder(goods, 0, () => 0));
        }

        [Fact]
        public void WhatComesWithAThingIsPutOutRightAfterItAndOnce()
        {
            // The crossbow first brings them; "bolts" first by themselves, and the crossbow is put in beside them.
            var goods = new[] { E("bolts", 1), E("crossbow", 1, "bolts"), E("dagger", 1) };
            Assert.Equal(new[] { "crossbow", "bolts", "dagger" }, Names(MerchantDraw.GoodsOrder(goods, 5, () => 0)));
            var apart = new[] { E("dagger", 1), E("bolts", 1), E("ward", 1), E("crossbow", 1, "bolts") };
            Assert.Equal(new[] { "dagger", "crossbow", "bolts", "ward" }, Names(MerchantDraw.GoodsOrder(apart, 5, () => 0)));
            var rolls = new Queue<double>(new[] { 0.5, 0.0 });   // the middle one of three, then the only one left
            Assert.Equal(new[] { "crossbow", "bolts", "dagger" }, Names(MerchantDraw.GoodsOrder(goods, 5, rolls.Dequeue)));
            Assert.Empty(rolls);
        }

        [Fact]
        public void AThingOnlyEverStandsWithItsCompanions()
        {
            var mayStandAlone = new[] { E("crossbow", 3, "bolts"), E("bolts", 2), E("dagger", 3), E("ward", 2), E("tome1", 1) };
            var rng = new Random(3);
            int alone = 0;
            for (int i = 0; i < 20_000; i++)
            {
                var o = Names(MerchantDraw.GoodsOrder(mayStandAlone, 3, rng.NextDouble));
                if (o.Contains("crossbow")) Assert.Equal("bolts", o[Array.IndexOf(o, "crossbow") + 1]);
                else if (o.Contains("bolts")) alone++;
            }
            Assert.True(alone > 1000);

            var goods = new[] { E("crossbow", 3, "bolts"), E("bolts", 0), E("dagger", 3), E("ward", 2), E("tome1", 1), E("tome2", 1) };
            int crossbow = 0, bolts = 0;
            for (int i = 0; i < 20_000; i++)
                for (int places = 1; places <= 6; places++)
                {
                    var o = Names(MerchantDraw.GoodsOrder(goods, places, rng.NextDouble));
                    Assert.True(o.Length <= places && o.Distinct().Count() == o.Length, string.Join(",", o));
                    Assert.Equal(o.Contains("crossbow"), o.Contains("bolts"));   // weight 0: never by itself
                    if (o.Contains("crossbow")) { crossbow++; Assert.True(places >= 2); Assert.Equal("bolts", o[Array.IndexOf(o, "crossbow") + 1]); }
                    if (places == 6) Assert.Equal(6, o.Length);   // room for all: nothing is passed over
                    bolts += o.Contains("bolts") ? 1 : 0;
                }
            Assert.True(crossbow > 10_000 && crossbow == bolts);
        }

        [Fact]
        public void OneThatNoLongerFitsIsPassedOverAndTheNextIsTaken()
        {
            var goods = new[] { E("dagger", 1), E("crossbow", 1, "bolts"), E("bolts", 0), E("ward", 1) };
            Assert.Equal(new[] { "dagger", "ward" }, Names(MerchantDraw.GoodsOrder(goods, 2, () => 0)));
        }

        [Fact]
        public void CompanionsOfCompanionsComeTooAndARingEnds()
        {
            var goods = new[] { E("a", 1, "b"), E("b", 0, "c", "a"), E("c", 0, "a") };
            Assert.Equal(new[] { "a", "b", "c" }, Names(MerchantDraw.GoodsOrder(goods, 3, () => 0)));
            Assert.Empty(MerchantDraw.GoodsOrder(goods, 2, () => 0));
        }

        [Fact]
        public void ACompanionThatIsNotLoadedHoldsNothingBack()
        {
            // The stall passes the loaded goods only.
            Assert.Equal(new[] { "crossbow" }, Names(MerchantDraw.GoodsOrder(new[] { E("crossbow", 1, "bolts") }, 1, () => 0)));
        }

        // ---- mistakes ----

        static MerchantClass Good() => new MerchantClass
        {
            ModId = "Berry", Chance = 0.1,
            Merchants = { new MerchantDef { Id = "trader", Stalls = { "Curiosa" }, Goods = { new PoolEntry { Item = "GlowBerry", Weight = 2 }, new PoolEntry { Item = "vanilla:Item_Key", Weight = 1 } } } },
        };

        [Fact]
        public void AGoodClassHasNoMistakesAndItsItemsGetFullIds()
        {
            var c = Good();
            Assert.Empty(MerchantDraw.Validate(c));
            Assert.Equal(new[] { "Berry:GlowBerry", "vanilla:Item_Key" }, c.Merchants[0].Goods.Select(g => g.Item));
        }

        [Theory]
        [InlineData(null, "Berry")]
        [InlineData("  ", "Berry")]
        [InlineData(" Berry Traders ", "Berry Traders")]
        public void TheHostSeesTheTitleOrTheId(string title, string shown) => Assert.Equal(shown, new MerchantClass { ModId = "Berry", Title = title }.Name);

        [Fact]
        public void ACopyDoesNotFollowTheOriginal()
        {
            var a = Good(); a.Title = "Berries"; a.Merchants[0].Look = () => "{}"; a.Merchants[0].Weight = 7;
            a.Merchants[0].Name = "Nobody"; a.Merchants[0].LeftHand = "vanilla:Item_Key"; a.Merchants[0].RightHand = "Stick";
            a.Merchants[0].Goods[0].Endless = true; a.Merchants[0].Goods[0].With.Add("vanilla:Item_Key");
            var b = a.Copy();
            a.Merchants[0].Name = a.Merchants[0].LeftHand = a.Merchants[0].RightHand = null; a.Merchants[0].Goods[0].Endless = false; a.Merchants[0].Goods[0].With.Clear();
            a.Chance = 1; a.Merchants[0].Weight = 0; a.Merchants[0].Stalls.Clear(); a.Merchants[0].Goods[0].Weight = 99; a.Merchants[0].Goods.RemoveAt(1);
            a.Merchants.Add(new MerchantDef { Id = "late" });
            Assert.Equal(("Berry", "Berries", 0.1), (b.ModId, b.Title, b.Chance));
            var m = Assert.Single(b.Merchants);
            Assert.Equal(("trader", 7.0, "{}"), (m.Id, m.Weight, m.Look()));
            Assert.Equal(new[] { "Curiosa" }, m.Stalls);
            Assert.Equal(("Nobody", "vanilla:Item_Key", "Stick"), (m.Name, m.LeftHand, m.RightHand));
            Assert.Equal(new[] { "GlowBerry|2|True|vanilla:Item_Key", "vanilla:Item_Key|1|False|" }, m.Goods.Select(g => $"{g.Item}|{g.Weight}|{g.Endless}|{string.Join(",", g.With)}"));
            Assert.Null(new MerchantClass { Merchants = null }.Copy().Merchants);
        }

        public static IEnumerable<object[]> Mistakes() => new List<object[]>
        {
            new object[] { (Action<MerchantClass>)(c => c.ModId = null), "ModId" },
            new object[] { (Action<MerchantClass>)(c => c.ModId = "vanilla"), "ModId" },
            new object[] { (Action<MerchantClass>)(c => c.Chance = 0.26), "Chance" },
            new object[] { (Action<MerchantClass>)(c => c.Chance = -0.1), "Chance" },
            new object[] { (Action<MerchantClass>)(c => c.Chance = double.NaN), "Chance" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants.Clear()), "no Merchants" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Id = "a:b"), "local id" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Id = "a#1"), "local id" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants.Add(new MerchantDef { Id = "trader", Stalls = { "Smith" } })), "twice" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Weight = -1), "Weight" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Weight = double.PositiveInfinity), "Weight" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Stalls.Clear()), "Stalls is empty" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Stalls[0] = "curiosa"), "no stall 'curiosa'" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Goods[0].Item = ""), "no Item" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Goods[0].Item = "nothing"), "not an item id" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Goods[0].Weight = 0), "must be a number > 0" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Goods[1].Item = "Berry:GlowBerry"), "in Goods twice" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Goods[0].Weight = double.NaN), "must be a number > 0" },
            new object[] { (Action<MerchantClass>)(c => { c.Merchants[0].Goods[0].With.Add("vanilla:Item_Key"); c.Merchants[0].Goods[1].Weight = -1; }), "must be a number > 0" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Goods[0].With.Add("Stick")), "'Berry:Stick', which is not in Goods" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Goods[0].With.Add(null)), "'', which is not in Goods" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Goods[0].With.Add("GlowBerry")), "With itself" },
            new object[] { (Action<MerchantClass>)(c => { foreach (var g in c.Merchants[0].Goods) g.Weight = 0; c.Merchants[0].Goods[0].With.Add("vanilla:Item_Key"); c.Merchants[0].Goods[1].With.Add("GlowBerry"); }), "nothing can come up" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Name = " "), "Name must be" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Name = new string('x', 41)), "Name must be" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].Name = "a\nb"), "Name must be" },
            new object[] { (Action<MerchantClass>)(c => c.Merchants[0].LeftHand = "Stick"), "only for a merchant with a Look" },
            new object[] { (Action<MerchantClass>)(c => { c.Merchants[0].Look = () => "{}"; c.Merchants[0].RightHand = "nothing"; }), "RightHand 'nothing' is not an item id" },
        };

        [Fact]
        public void AMerchantMayHaveANameThingsInHandAndGoodsThatOnlyComeWithOthers()
        {
            var c = Good();
            var m = c.Merchants[0];
            m.Look = () => "{}"; m.Name = new string('x', 40); m.LeftHand = "Stick"; m.RightHand = "vanilla:Item_Key";
            m.Goods[0].Weight = 0; m.Goods[1].With.Add("GlowBerry"); m.Goods[1].Endless = true;
            Assert.Empty(MerchantDraw.Validate(c));
            Assert.Equal(("Berry:Stick", "vanilla:Item_Key"), (m.LeftHand, m.RightHand));
            Assert.Equal(new[] { "Berry:GlowBerry" }, m.Goods[1].With);
            m.Goods[0].With = null; m.Name = null; m.LeftHand = ""; m.RightHand = null;
            Assert.Empty(MerchantDraw.Validate(c));
            Assert.Null(m.LeftHand);
        }

        [Theory]
        [MemberData(nameof(Mistakes))]
        public void EachMistakeIsNamed(Action<MerchantClass> spoil, string said)
        {
            var c = Good();
            spoil(c);
            var e = MerchantDraw.Validate(c);
            Assert.Contains(e, x => x.Contains(said));
        }

        // ---- hosts' files ----

        [Fact]
        public void AHostFilesSellersAreOneClassWithTheirStalls()
        {
            var f = new ShopFile { Format = 1, Mod = "MyServer", SourcePath = "a.json" };
            f.Sellers.Add(new SellerDef { Id = "m", Preset = "EntranceCave_Shopkeeper_Curiosa", Weight = 3 });
            f.Sellers.Add(new SellerDef { Id = "n", Preset = "EntranceCave_Shopkeeper_Smith", Weight = 1 });
            var c = Assert.Single(MerchantDraw.FromFiles(ShopCatalog.Build(new[] { f }), "EntranceCave_Shopkeeper_"));
            Assert.Equal("MyServer", c.ModId);
            Assert.Equal(new[] { "m|Curiosa|3", "n|Smith|1" }, c.Merchants.Select(m => $"{m.Id}|{m.Stalls[0]}|{m.Weight}"));
            Assert.All(c.Merchants, m => Assert.True(m.FromFile));
            Assert.Empty(MerchantDraw.Validate(c));
        }
    }
}
