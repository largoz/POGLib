using System.Collections.Generic;
using System.Linq;
using POGLib.Shops.Model;
using Xunit;

namespace POGLib.Shops.Tests
{
    public class ShopFileTests
    {
        private static ShopFile Parse(string json, List<Diagnostic> d) => ShopFileIO.Parse(json, "test.json", d);

        private const string Valid = @"{
  // comments are allowed
  ""format"": 1,
  ""mod"": ""BerryMod"",
  ""slots"": [ { ""id"": ""berry_stand"", ""scene"": ""PitOfGoblin_InBetween"", ""seller"": ""vanilla:NPC_X"",
                 ""pos"": [1, 0, -0.5], ""rot"": [0, 90, 0], ""priceTag"": { ""pos"": [0, 0.3, 0.3] },
                 ""pool"": [ { ""item"": ""HealingBerry"", ""weight"": 10 }, { ""item"": ""nothing"", ""weight"": 2 } ] } ],
  ""add"": [ { ""slot"": ""vanilla:PitOfGoblin_Lobby/StarterShop/A"", ""item"": ""HealingBerry"", ""weight"": 3 } ],
}";

        [Fact]
        public void ValidFileParsesAndRoundTrips()
        {
            var d = new List<Diagnostic>();
            var f = Parse(Valid, d);
            Assert.NotNull(f);
            Assert.Empty(d);
            Assert.Equal("BerryMod", f.Mod);
            Assert.Equal(2, f.Slots[0].Pool.Count);
            Assert.Equal(new[] { 0f, 0.3f, 0.3f }, f.Slots[0].PriceTag.Pos);

            var again = Parse(ShopFileIO.Write(f), d = new List<Diagnostic>());
            Assert.NotNull(again);
            Assert.Empty(d);
            Assert.Equal(ShopFileIO.Write(f), ShopFileIO.Write(again));
        }

        [Fact]
        public void APlaceThatDoesNotRunOutIsSaidInAPoolOnly()
        {
            var d = new List<Diagnostic>();
            var f = Parse(Valid.Replace(@"""HealingBerry"", ""weight"": 10", @"""HealingBerry"", ""endless"": true, ""weight"": 10"), d);
            Assert.Empty(d);
            Assert.Equal(new[] { true, false }, f.Slots[0].Pool.Select(e => e.Endless));
            Assert.Equal(new[] { true, false }, ShopCatalog.Build(new[] { f }).Resolve("BerryMod:berry_stand", null).Entries.Select(e => e.Endless));
            var text = ShopFileIO.Write(f);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "endless"));   // a place that runs out says nothing, as before
            Assert.Equal(new[] { true, false }, Parse(text, d).Slots[0].Pool.Select(e => e.Endless));
            Assert.Empty(d);

            // "with" means something in a merchant's Goods (code) only, so a file that has it is told.
            Parse(Valid.Replace(@"""weight"": 10", @"""weight"": 10, ""with"": [""X""]"), d);
            Assert.Contains(d, x => x.Message.Contains("unknown key") && x.Message.Contains("with"));
        }

        [Fact]
        public void UnknownKeyIsAWarningNotAnError()
        {
            var d = new List<Diagnostic>();
            var f = Parse(Valid.Replace("\"priceTag\"", "\"pirceTag\""), d);
            Assert.NotNull(f);
            Assert.Contains(d, x => x.Severity == Severity.Warning && x.Message.Contains("slots[0].pirceTag"));
        }

        [Theory]
        [InlineData("\"format\": 1", "\"format\": 2", "newer")]
        [InlineData("\"format\": 1,", "", "missing \\\"format\\\"")]
        [InlineData("\"mod\": \"BerryMod\"", "\"mod\": \"Berry Mod\"", "mod id")]
        [InlineData("\"mod\": \"BerryMod\"", "\"mod\": \"vanilla\"", "mod id")]
        [InlineData("\"pos\": [1, 0, -0.5]", "\"pos\": [1, 0]", "pos must be")]
        [InlineData("\"weight\": 10", "\"weight\": -1", "weight must be")]
        public void BadFileIsRefusedWithTheReason(string from, string to, string reason)
        {
            var d = new List<Diagnostic>();
            Assert.Null(Parse(Valid.Replace(from, to), d));
            Assert.Contains(d, x => x.Severity == Severity.Error && x.Message.Contains(reason.Replace("\\\"", "\"")));
        }

        // "after" places a slot in the row of the seller's goods, instead of "pos" (ROADMAP 1.4, 2026-11-21).
        private const string AfterSlot = @"{ ""format"": 1, ""mod"": ""BerryMod"",
  ""slots"": [ { ""id"": ""berry_stand"", ""scene"": ""PitOfGoblin_Lobby"", ""seller"": ""vanilla:NPC_X"",
                 ""after"": ""vanilla:PitOfGoblin_Lobby/StarterShop/T"", ""pool"": [ { ""item"": ""HealingBerry"", ""weight"": 1 } ] } ] }";

        [Fact]
        public void AfterTakesThePlaceOfPos()
        {
            var d = new List<Diagnostic>();
            var f = Parse(AfterSlot, d);
            Assert.NotNull(f);
            Assert.Empty(d);
            Assert.Equal("vanilla:PitOfGoblin_Lobby/StarterShop/T", f.Slots[0].After);
            Assert.Null(f.Slots[0].Pos);
        }

        [Theory]
        [InlineData("left", false)]
        [InlineData("RIGHT", true)]
        [InlineData("vanilla:PitOfGoblin_Lobby/StarterShop/T", null)]
        public void LeftAndRightAreEndsOfTheRowAnythingElseIsASlotId(string after, bool? right) => Assert.Equal(right, SlotDef.SideOf(after));

        [Fact]
        public void AfterTakesAShiftAndATilt()
        {
            var d = new List<Diagnostic>();
            var f = Parse(AfterSlot.Replace("\"after\"", "\"pos\": [0.1, 0, -0.2], \"rot\": [10, 0, 0], \"after\""), d);
            Assert.NotNull(f);
            Assert.Empty(d);
            Assert.Equal(new[] { 0.1f, 0f, -0.2f }, f.Slots[0].Pos);
        }

        [Fact]
        public void LeftOrRightNeedsASeller()
        {
            var d = new List<Diagnostic>();
            Assert.Null(Parse(AfterSlot.Replace("\"seller\": \"vanilla:NPC_X\",", "").Replace("vanilla:PitOfGoblin_Lobby/StarterShop/T", "left"), d));
            Assert.Contains(d, x => x.Severity == Severity.Error && x.Message.Contains("needs a \"seller\""));
        }

        [Theory]
        [InlineData("\"after\"", "\"pos\": [0, 0], \"after\"", "with \"after\" it moves the slot")]
        [InlineData("\"after\"", "\"anchor\": \"vanilla:A\", \"after\"", "both \"after\" and \"anchor\"")]
        [InlineData("\"after\": \"vanilla:PitOfGoblin_Lobby/StarterShop/T\",", "", "pos must be")]
        public void AfterWithCoordinatesOrWithoutEitherIsRefused(string from, string to, string reason)
        {
            var d = new List<Diagnostic>();
            Assert.Null(Parse(AfterSlot.Replace(from, to), d));
            Assert.Contains(d, x => x.Severity == Severity.Error && x.Message.Contains(reason));
        }

        // 1.2 (2026-11-21): a mod's own file may only place its own slots and say how its items stand.
        [Fact]
        public void AModsFileWithOnlySlotsAndItemsIsInScope()
        {
            var d = new List<Diagnostic>();
            var f = Parse(AfterSlot, d);
            Assert.True(ShopFileIO.CheckModScope(f, d));
            Assert.Empty(d);
        }

        [Theory]
        [InlineData("\"add\": [ { \"slot\": \"vanilla:S/A\", \"item\": \"HealingBerry\", \"weight\": 1 } ]", "\"add\" puts items")]
        [InlineData("\"requires\": [\"OtherMod\"], \"patch\": [ { \"slot\": \"OtherMod:s\", \"item\": \"OtherMod:x\", \"remove\": true } ]", "\"patch\" changes")]
        [InlineData("\"sellers\": [ { \"id\": \"m\", \"preset\": \"EntranceCave_Shopkeeper_Curiosa\", \"weight\": 1 } ]", "\"sellers\" (dungeon merchants)")]
        public void AModsFileThatReachesOtherContentIsRefusedWithTheReason(string section, string reason)
        {
            var d = new List<Diagnostic>();
            var f = Parse(AfterSlot.Replace("\"slots\":", section + ", \"slots\":"), d);
            Assert.NotNull(f);                                   // a valid file for a host ...
            Assert.False(ShopFileIO.CheckModScope(f, d));        // ... but not for a mod
            Assert.Contains(d, x => x.Severity == Severity.Error && x.Message.Contains(reason) && x.Message.Contains("the file is not used"));
        }

        [Fact]
        public void DuplicateSlotIdIsRefused()
        {
            var d = new List<Diagnostic>();
            string twice = Valid.Replace("\"slots\": [ {", "\"slots\": [ { \"id\": \"dup\", \"scene\": \"S\", \"seller\": \"vanilla:N\", \"pos\": [0,0,0] }, {").Replace("berry_stand", "dup");
            Assert.Null(Parse(twice, d));
            Assert.Contains(d, x => x.Message.Contains("used twice"));
        }

        [Fact]
        public void PatchNeedsExactlyOneChange()
        {
            var d = new List<Diagnostic>();
            string json = Valid.Replace("\"add\":", "\"patch\": [ { \"slot\": \"vanilla:S/A\", \"item\": \"vanilla:Item_Key\", \"weight\": 1, \"remove\": true } ], \"add\":");
            Assert.Null(Parse(json, d));
            Assert.Contains(d, x => x.Message.Contains("exactly one"));
        }

        [Fact]
        public void BrokenJsonIsAnErrorNotAnException()
        {
            var d = new List<Diagnostic>();
            Assert.Null(Parse("{ \"format\": 1, ", d));
            Assert.Contains(d, x => x.Severity == Severity.Error && x.Message.Contains("not valid JSON"));
        }
    }

    public class ShopCatalogTests
    {
        private const string Slot = "vanilla:PitOfGoblin_InBetween/Pitaxas/3";
        private static readonly VanillaEntry[] Potion = { new VanillaEntry("vanilla:Item_Potion_01", 1) };

        private static ShopFile Mod(string id, params string[] requires) =>
            new ShopFile { Format = 1, Mod = id, Requires = requires.ToList(), SourcePath = id + ".json" };

        private static ShopFile WithAdd(ShopFile f, string slot, string item, double w)
        { f.Add.Add(new AddOp { Slot = slot, Item = item, Weight = w }); return f; }

        private static ShopFile WithPatch(ShopFile f, PatchOp p) { f.Patch.Add(p); return f; }

        [Fact]
        public void DesignDocExample_VanillaPlusTwoMods_Is50_25_25()
        {
            var c = ShopCatalog.Build(new[]
            {
                WithAdd(Mod("A"), Slot, "Berry", 5),
                WithAdd(Mod("B"), Slot, "Sword", 5),
            });
            var r = c.Resolve(Slot, Potion);
            Assert.False(r.Untouched);
            Assert.Equal(0.50, r.ChanceOfItem("vanilla:Item_Potion_01"), 9);
            Assert.Equal(0.25, r.ChanceOfItem("A:Berry"), 9);
            Assert.Equal(0.25, r.ChanceOfItem("B:Sword"), 9);
        }

        [Fact]
        public void AnyNumberOfModsStillSumsToOne()
        {
            var files = Enumerable.Range(0, 7).Select(i => WithAdd(Mod("M" + i), Slot, "X" + i, i + 1)).ToList();
            var r = ShopCatalog.Build(files).Resolve(Slot, Potion);
            Assert.Equal(1.0, r.Entries.Sum(e => r.ChanceOf(e)), 9);
            Assert.True(r.ChanceOfItem("vanilla:Item_Potion_01") > 0);
        }

        [Fact]
        public void UntouchedVanillaSlotIsReportedUntouched()
        {
            var r = ShopCatalog.Build(new[] { WithAdd(Mod("A"), "vanilla:Other/Slot", "Berry", 5) }).Resolve(Slot, Potion);
            Assert.True(r.Untouched);
            Assert.Single(r.Entries);
        }

        [Fact]
        public void VanillaCountsMultiplyTheDefaultWeight()
        {
            var opts = new ShopCatalogOptions { DefaultVanillaWeight = 10 };
            var r = ShopCatalog.Build(new[] { WithAdd(Mod("A"), Slot, "Berry", 10) }, opts)
                .Resolve(Slot, new[] { new VanillaEntry("vanilla:Item_Dice", 2) });
            Assert.Equal(20, r.Entries.First(e => e.AddedBy == "vanilla").Weight);
            Assert.Equal(1.0 / 3, r.ChanceOfItem("A:Berry"), 9);
        }

        [Fact]
        public void PickIsWeightedSkipsZeroAndReturnsNothingEntries()
        {
            var f = Mod("A");
            f.Slots.Add(new SlotDef
            {
                Id = "s", Scene = "S", Seller = "vanilla:N", Pos = new float[3],
                Pool = { new PoolEntry { Item = "One", Weight = 1 }, new PoolEntry { Item = "Zero", Weight = 0 }, new PoolEntry { Item = "nothing", Weight = 3 } },
            });
            var r = ShopCatalog.Build(new[] { f }).Resolve("A:s", null);
            Assert.Equal("A:One", r.Pick(0.0).Item);
            Assert.Equal("A:One", r.Pick(0.2499).Item);
            Assert.Equal("nothing", r.Pick(0.25).Item);
            Assert.Equal("nothing", r.Pick(0.999999).Item);
            Assert.DoesNotContain(Enumerable.Range(0, 1000).Select(i => r.Pick(i / 1000.0).Item), x => x == "A:Zero");
        }

        [Fact]
        public void EmptyOrAllZeroSlotPicksNull()
        {
            var r = ShopCatalog.Build(new[] { WithPatch(Mod("A"), new PatchOp { Slot = Slot, Item = "vanilla:Item_Potion_01", Weight = 0 }) })
                .Resolve(Slot, Potion);
            Assert.Equal(0, r.TotalWeight);
            Assert.Null(r.Pick(0.5));
        }

        [Fact]
        public void PatchOnAnotherModsContentNeedsRequires()
        {
            var refused = ShopCatalog.Build(new[]
            {
                WithAdd(Mod("A"), Slot, "Berry", 5),
                WithPatch(Mod("B"), new PatchOp { Slot = Slot, Item = "A:Berry", Remove = true }),
            });
            Assert.Contains(refused.Diagnostics, d => d.Severity == Severity.Error && d.Message.Contains("requires"));
            Assert.Contains(refused.Resolve(Slot, Potion).Entries, e => e.Item == "A:Berry");

            var allowed = ShopCatalog.Build(new[]
            {
                WithAdd(Mod("A"), Slot, "Berry", 5),
                WithPatch(Mod("B", "A"), new PatchOp { Slot = Slot, Item = "A:Berry", Remove = true }),
            });
            var r = allowed.Resolve(Slot, Potion);
            Assert.DoesNotContain(r.Entries, e => e.Item == "A:Berry");
            Assert.Contains(r.History, h => h.Mod == "B" && h.Text.Contains("removed A:Berry"));
        }

        [Fact]
        public void PatchOnVanillaNeedsNothingAndIsRecorded()
        {
            var r = ShopCatalog.Build(new[]
            {
                WithPatch(Mod("A"), new PatchOp { Slot = Slot, Item = "vanilla:Item_Potion_01", ReplaceWith = "Elixir" }),
            }).Resolve(Slot, Potion);
            Assert.Equal("A:Elixir", r.Entries.Single().Item);
            Assert.Equal("vanilla", r.Entries.Single().AddedBy);
            Assert.Contains(r.History, h => h.Text.Contains("replaced vanilla:Item_Potion_01") && h.Text.Contains("A:Elixir"));
        }

        [Fact]
        public void DependencyOrderDecidesWhichPatchWins()
        {
            // Z requires A, so A's patch applies first and Z's (applied later) wins — although "Z" > "A"
            // would also be the alphabetical order, so add M which A requires to prove it is not alphabetical.
            var files = new[]
            {
                WithPatch(Mod("Z", "A"), new PatchOp { Slot = Slot, Item = "vanilla:Item_Potion_01", Weight = 7 }),
                WithPatch(Mod("A", "M"), new PatchOp { Slot = Slot, Item = "vanilla:Item_Potion_01", Weight = 3 }),
                WithPatch(Mod("M"), new PatchOp { Slot = Slot, Item = "vanilla:Item_Potion_01", Weight = 1 }),
            };
            var c = ShopCatalog.Build(files);
            Assert.Equal(new[] { "M", "A", "Z" }, c.ModOrder);
            var r = c.Resolve(Slot, Potion);
            Assert.Equal(7, r.Entries.Single().Weight);
            Assert.Equal(new[] { "M", "A", "Z" }, r.History.Select(h => h.Mod));
        }

        [Fact]
        public void AddsApplyBeforePatchesWhateverTheModOrder()
        {
            // "A" sorts first, but its patch must still see the entry "B" adds.
            var r = ShopCatalog.Build(new[]
            {
                WithPatch(Mod("A", "B"), new PatchOp { Slot = Slot, Item = "B:Sword", Weight = 1 }),
                WithAdd(Mod("B"), Slot, "Sword", 50),
            }).Resolve(Slot, Potion);
            Assert.Equal(1, r.Entries.Single(e => e.Item == "B:Sword").Weight);
        }

        [Fact]
        public void RequiresCycleIsBrokenDeterministicallyAndReported()
        {
            var files = new[] { Mod("B", "A"), Mod("A", "B") };
            var c1 = ShopCatalog.Build(files);
            var c2 = ShopCatalog.Build(files.Reverse());
            Assert.Equal(c1.ModOrder, c2.ModOrder);
            Assert.Contains(c1.Diagnostics, d => d.Message.Contains("cycle"));
        }

        [Fact]
        public void PatchThatFindsNothingSaysSo()
        {
            var r = ShopCatalog.Build(new[] { WithPatch(Mod("A"), new PatchOp { Slot = Slot, Item = "vanilla:Item_Nope", Remove = true }) })
                .Resolve(Slot, Potion);
            Assert.Single(r.Entries);
            Assert.Contains(r.History, h => h.Text.Contains("found no such entry"));
        }

        [Fact]
        public void ModSlotGetsItsPoolPlusOtherModsAdds()
        {
            var a = Mod("A");
            a.Slots.Add(new SlotDef { Id = "stand", Scene = "S", Seller = "vanilla:N", Pos = new float[3], Pool = { new PoolEntry { Item = "Berry", Weight = 4 } } });
            var c = ShopCatalog.Build(new[] { a, WithAdd(Mod("B"), "A:stand", "Sword", 4) });
            Assert.True(c.ModSlots.ContainsKey("A:stand"));
            var r = c.Resolve("A:stand", null);
            Assert.Equal(0.5, r.ChanceOfItem("A:Berry"), 9);
            Assert.Equal(0.5, r.ChanceOfItem("B:Sword"), 9);
        }

        [Fact]
        public void MerchantSlotsUseTheirOwnStockAndCanBeAddedTo()
        {
            var a = Mod("A");
            a.Sellers.Add(new SellerDef { Id = "m", Preset = "EntranceCave_Shopkeeper_Curiosa", Slots = { new List<PoolEntry> { new PoolEntry { Item = "Berry", Weight = 1, Endless = true } }, null } });
            var c = ShopCatalog.Build(new[] { a, WithAdd(Mod("B"), "A:m#1", "Sword", 1) });
            var r1 = c.Resolve(ShopCatalog.SellerSlotId("A:m", 1), new[] { new VanillaEntry("vanilla:Item_Torch_Unlit", 1) });
            Assert.Equal(new[] { "A:Berry", "B:Sword" }, r1.Entries.Select(e => e.Item));   // preset stock replaced
            Assert.Equal(new[] { true, false }, r1.Entries.Select(e => e.Endless));
            var r2 = c.Resolve("A:m#2", new[] { new VanillaEntry("vanilla:Item_Key", 1) });
            Assert.True(r2.Untouched);                                                       // no stock given: preset's own
        }

        [Fact]
        public void StandingPointsAreForTheFilesOwnItemsOnly()
        {
            var d = new List<Diagnostic>();
            var bad = new ShopFile { Format = 1, Mod = "A", SourcePath = "a.json", Items = { new ItemDisplayDef { Item = "B:Sword", Standing = new TagDef { Pos = new float[] { 0, 0.1f, 0 } } } } };
            Assert.False(ShopFileIO.Validate(bad, d));
            Assert.Contains(d, x => x.Message.Contains("own items"));
            var good = new ShopFile { Format = 1, Mod = "A", SourcePath = "a.json", Items = { new ItemDisplayDef { Item = "Berry", Standing = new TagDef { Pos = new float[] { 0, 0.1f, 0 } } } } };
            Assert.True(ShopFileIO.Validate(good, d = new List<Diagnostic>()));
            Assert.Equal(0.1f, ShopCatalog.Build(new[] { good }).ItemDisplays["A:Berry"].standing.Pos[1]);
        }

        [Fact]
        public void UnqualifiedIdsBelongToTheFilesMod()
        {
            Assert.Equal("A:x", ShopIds.Qualify("x", "A"));
            Assert.Equal("B:x", ShopIds.Qualify("B:x", "A"));
            Assert.Equal("nothing", ShopIds.Qualify("nothing", "A"));
            Assert.Equal("PitOfGoblin_Lobby", ShopIds.SceneOfVanilla("vanilla:PitOfGoblin_Lobby/StarterShop/X"));
        }
    }
}
