using System;
using System.Collections.Generic;
using System.Linq;
using POGLib.Shops.Model;
using Xunit;

namespace POGLib.Shops.Tests
{
    /// <summary>
    /// The one weighted draw the game uses for slots and merchant variants (ShopRuntime, MerchantDraw).
    /// Expected values come from the weights themselves (w / sum), not from another draw.
    /// </summary>
    public class WeightedPickTests
    {
        [Fact]
        public void FrequenciesMatchTheWeights()
        {
            // Index 2 (zero), 5 (negative) and 6 (NaN) must never come up and must not count in the total.
            var weights = new[] { 10.0, 5, 0, 5, 2.5, -3, double.NaN };
            const double total = 22.5;
            const int n = 400_000;
            var rng = new Random(20261114);
            var counts = new int[weights.Length];
            for (int i = 0; i < n; i++) counts[WeightedPick.Index(weights, rng.NextDouble())]++;

            // Largest standard error here is sqrt(.444 * .556 / 400000) = 0.0008; 0.005 is over 6 of them.
            for (int i = 0; i < weights.Length; i++)
            {
                double expected = weights[i] > 0 ? weights[i] / total : 0;
                Assert.True(Math.Abs((double)counts[i] / n - expected) < 0.005,
                    $"index {i}: drawn {(double)counts[i] / n:0.0000}, weight says {expected:0.0000}");
            }
            Assert.Equal(0, counts[2] + counts[5] + counts[6]);
        }

        [Theory]
        [InlineData(0.0, 0)]
        [InlineData(0.4999, 0)]
        [InlineData(0.5, 1)]            // half-open: [0, 1) is entry 0, [1, 2) is entry 1
        [InlineData(0.9999999999, 1)]
        [InlineData(1.0, 1)]            // out of range at the top: the last usable entry
        [InlineData(-0.5, 0)]           // out of range at the bottom: the first usable entry
        [InlineData(double.NaN, 0)]
        public void BoundariesAreHalfOpen(double r, int expected) =>
            Assert.Equal(expected, WeightedPick.Index(new[] { 1.0, 1.0 }, r));

        [Fact]
        public void NothingUsableIsMinusOne()
        {
            Assert.Equal(-1, WeightedPick.Index(new double[0], 0.5));
            Assert.Equal(-1, WeightedPick.Index((IReadOnlyList<double>)null, 0.5));
            Assert.Equal(-1, WeightedPick.Index(new[] { 0.0, -1, double.NaN, double.PositiveInfinity }, 0.5));
        }

        [Fact]
        public void UnusableEntriesAroundTheOnlyUsableOneNeverWin()
        {
            var w = new[] { 0.0, 3, 0 };
            foreach (var r in new[] { 0.0, 0.3, 0.7, 0.99999, 1.0 }) Assert.Equal(1, WeightedPick.Index(w, r));
        }

        [Fact]
        public void PickSkipsEntriesTheCallerRejects()
        {
            var f = new ShopFile { Format = 1, Mod = "A", SourcePath = "A.json" };
            f.Slots.Add(new SlotDef
            {
                Id = "s", Scene = "S", Seller = "vanilla:N", Pos = new float[3],
                Pool = { new PoolEntry { Item = "Missing", Weight = 10 }, new PoolEntry { Item = "Here", Weight = 10 }, new PoolEntry { Item = "nothing", Weight = 5 } },
            });
            var r = ShopCatalog.Build(new[] { f }).Resolve("A:s", null);
            Func<SlotEntry, bool> loaded = e => e.Item != "A:Missing";

            Assert.Equal("A:Here", r.Pick(0.0, loaded).Item);           // without the filter r = 0 is "Missing"
            Assert.Equal("A:Missing", r.Pick(0.0).Item);
            Assert.Equal("nothing", r.Pick(0.7, loaded).Item);          // 0.7 × 15 = 10.5 ≥ 10
            Assert.Equal(10.0 / 15, r.ChanceAmong(r.Entries.First(e => e.Item == "A:Here"), loaded), 9);
            Assert.Equal(0, r.ChanceAmong(r.Entries.First(e => e.Item == "A:Missing"), loaded));
            Assert.Null(r.Pick(0.5, e => false));
        }
    }

    public class NearestIdsTests
    {
        [Theory]
        [InlineData("kitten", "sitting", 3)]   // the textbook pair
        [InlineData("", "abc", 3)]
        [InlineData("same", "same", 0)]
        [InlineData("Bread", "bread", 1)]     // ids are case-sensitive
        public void DistanceIsLevenshtein(string a, string b, int d) => Assert.Equal(d, NearestIds.Distance(a, b));

        [Fact]
        public void ClosestFirstTiesByOrdinalAtMostMax()
        {
            // HealingBeryy and HealingBerry are 1 edit away (listed in reverse ordinal order on purpose),
            // HealingBerrz 2, HealingCherry 3.
            var near = NearestIds.Of("BerryMod:HealingBery",
                new[] { "vanilla:Item_Bread", "BerryMod:HealingBerrz", "BerryMod:HealingBeryy", "BerryMod:HealingBerry", "BerryMod:HealingBerry", "BerryMod:HealingCherry" }, 3);
            Assert.Equal(new[] { "BerryMod:HealingBerry", "BerryMod:HealingBeryy", "BerryMod:HealingBerrz" }, near);   // duplicates once
            Assert.Empty(NearestIds.Of("x", new string[0]));
            Assert.Empty(NearestIds.Of("x", null));
        }
    }

    public class CatalogChecksTests
    {
        private static ShopFile Mod(string id, params string[] requires) =>
            new ShopFile { Format = 1, Mod = id, Requires = requires.ToList(), SourcePath = id + ".json" };

        private static ShopFile WithSlot(ShopFile f, string id)
        {
            f.Slots.Add(new SlotDef { Id = id, Scene = "S", Seller = "vanilla:N", Pos = new float[3], Pool = { new PoolEntry { Item = "Thing", Weight = 1 } } });
            return f;
        }

        [Fact]
        public void AddToAMisspeltSlotOfALoadedModIsAWarningWithTheClosestId()
        {
            var a = Mod("A"); a.Add.Add(new AddOp { Slot = "B:stal", Item = "Berry", Weight = 5 });
            var c = ShopCatalog.Build(new[] { a, WithSlot(Mod("B"), "stall") });
            var d = Assert.Single(c.Diagnostics);
            Assert.Equal(Severity.Warning, d.Severity);
            Assert.Equal("A", d.Source);
            Assert.Contains("'B:stal'", d.Message);
            Assert.Contains("B:stall", d.Message);
        }

        [Fact]
        public void AMisspeltOwnSlotIsCaughtToo()
        {
            var b = WithSlot(Mod("B"), "stall");
            b.Add.Add(new AddOp { Slot = "stal", Item = "Berry", Weight = 5 });   // unqualified = B's own
            var d = Assert.Single(ShopCatalog.Build(new[] { b }).Diagnostics);
            Assert.Equal(Severity.Warning, d.Severity);
            Assert.Contains("B:stall", d.Message);
        }

        [Fact]
        public void AddToASlotOfAModThatIsNotInstalledIsOnlyInfo()
        {
            var a = Mod("A"); a.Add.Add(new AddOp { Slot = "C:stall", Item = "Berry", Weight = 5 });
            var d = Assert.Single(ShopCatalog.Build(new[] { a }).Diagnostics);
            Assert.Equal(Severity.Info, d.Severity);
            Assert.Contains("'C' has no shop file loaded", d.Message);
        }

        [Fact]
        public void ExistingSlotsAndMerchantSlotsRaiseNothing()
        {
            var b = WithSlot(Mod("B"), "stall");
            b.Sellers.Add(new SellerDef { Id = "trader", Preset = "P", Weight = 1 });
            var a = Mod("A");
            a.Add.Add(new AddOp { Slot = "B:stall", Item = "Berry", Weight = 5 });
            a.Add.Add(new AddOp { Slot = "B:trader#2", Item = "Berry", Weight = 5 });   // slot 2 of a known seller
            a.Add.Add(new AddOp { Slot = "vanilla:Scene/Path", Item = "Berry", Weight = 5 });   // checked in the scene, not here
            Assert.Empty(ShopCatalog.Build(new[] { a, b }).Diagnostics);
        }

        [Fact]
        public void ItemReferencesCoverPoolsAddsAndReplacementsButNotNothing()
        {
            var b = WithSlot(Mod("B"), "stall");
            b.Slots[0].Pool.Add(new PoolEntry { Item = "nothing", Weight = 1 });
            b.Add.Add(new AddOp { Slot = "vanilla:S/P", Item = "Cake", Weight = 1 });
            b.Patch.Add(new PatchOp { Slot = "vanilla:S/P", Item = "vanilla:Item_Bread", ReplaceWith = "Pie" });
            var refs = ShopCatalog.Build(new[] { b }).ItemReferences;
            Assert.Equal(new[] { "B:Cake add", "B:Pie replace", "B:Thing pool" },
                refs.Select(r => r.Item + " " + r.How).OrderBy(x => x, StringComparer.Ordinal));
        }

        [Fact]
        public void ModsTargetingListsEachModOnceInRuleOrder()
        {
            var a = Mod("A"); a.Add.Add(new AddOp { Slot = "vanilla:S/P", Item = "X", Weight = 1 });
            var z = Mod("Z"); z.Add.Add(new AddOp { Slot = "vanilla:S/P", Item = "Y", Weight = 1 });
            z.Add.Add(new AddOp { Slot = "vanilla:S/P", Item = "W", Weight = 1 });
            Assert.Equal(new[] { "A", "Z" }, ShopCatalog.Build(new[] { z, a }).ModsTargeting("vanilla:S/P"));
            Assert.Empty(ShopCatalog.Build(new[] { z, a }).ModsTargeting("vanilla:S/Q"));
        }

        [Fact]
        public void VanillaWeightZeroLeavesOnlyTheModsItems()
        {
            var a = Mod("A"); a.Add.Add(new AddOp { Slot = "vanilla:S/P", Item = "Berry", Weight = 1 });
            var r = ShopCatalog.Build(new[] { a }, new ShopCatalogOptions { DefaultVanillaWeight = 0 })
                .Resolve("vanilla:S/P", new[] { new VanillaEntry("vanilla:Item_Bread", 1) });
            foreach (var x in new[] { 0.0, 0.5, 0.99999 }) Assert.Equal("A:Berry", r.Pick(x).Item);
        }

        [Theory]
        [InlineData(0.0, true)]
        [InlineData(10.0, true)]
        [InlineData(-1.0, false)]
        [InlineData(double.NaN, false)]
        [InlineData(double.PositiveInfinity, false)]
        public void VanillaWeightSettingIsValidated(double w, bool ok) => Assert.Equal(ok, ShopCatalogOptions.IsValidVanillaWeight(w));
    }
}
