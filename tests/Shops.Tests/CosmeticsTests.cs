using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using POGLib.Core;
using POGLib.Cosmetics;
using POGLib.Cosmetics.Model;
using Xunit;

namespace POGLib.Shops.Tests
{
    public class CosmeticsTests
    {
        private static GlbModel Model(string file) => Glb.ReadFile(Path.Combine(AppContext.BaseDirectory, "fixtures", file));

        [Theory]
        [InlineData("MyMod", "Coat", true)]
        [InlineData("My.Mod-2_x", "Coat_01", true)]
        [InlineData("", "Coat", false)]
        [InlineData("MyMod", null, false)]
        [InlineData("My:Mod", "Coat", false)]
        [InlineData("MyMod", "Coat, hat", false)]
        [InlineData("MyMod", "Co at", false)]
        public void Ids(string mod, string part, bool good) => Assert.Equal(good, CosmeticRules.CheckIds(mod, part) == null);

        [Theory]
        [InlineData(CosmeticSlot.Chest, "skinned.glb", true)]
        [InlineData(CosmeticSlot.Legs, "skinned.glb", true)]
        [InlineData(CosmeticSlot.Accessory, "skinned.glb", true)]
        [InlineData(CosmeticSlot.Accessory, "static.glb", true)]
        [InlineData(CosmeticSlot.Hair, "static.glb", true)]
        [InlineData(CosmeticSlot.Horns, "static.glb", true)]
        [InlineData(CosmeticSlot.Chest, "static.glb", false)]
        [InlineData(CosmeticSlot.Legs, "static.glb", false)]
        [InlineData(CosmeticSlot.Feet, "static.glb", false)]
        public void AModelWithoutWeightsIsForTheHeadOnly(CosmeticSlot slot, string file, bool good)
        {
            var wrong = CosmeticRules.CheckModel(slot, Model(file));
            Assert.Equal(good, wrong == null);
            if (!good) Assert.Contains("weighted", wrong);
        }

        [Fact]
        public void ABoneTheGameCannotFindIsNamed()
        {
            var m = Model("skinned.glb");
            m.JointNames[m.JointNames.IndexOf("mixamorig:Head")] = "UpperMouth";
            Assert.Contains("UpperMouth", CosmeticRules.CheckModel(CosmeticSlot.Chest, m));
        }

        [Fact]
        public void ABoneWithoutWeightDoesNotCount()
        {
            var m = Model("skinned.glb");
            m.JointNames.Add("UpperMouth");
            Assert.Null(CosmeticRules.CheckModel(CosmeticSlot.Chest, m));
            Assert.Empty(CosmeticRules.MissingBones(m, new[] { "mixamorig:Hips", "mixamorig:Spine", "mixamorig:Head", "mixamorig:LeftArm" }));
            Assert.Equal(new[] { "mixamorig:LeftArm" }, CosmeticRules.MissingBones(m, new[] { "mixamorig:Hips", "mixamorig:Spine", "mixamorig:Head" }));
        }

        [Fact]
        public void AVertexWithoutWeightsIsCounted()
        {
            var m = Model("skinned.glb");
            for (int k = 0; k < 4; k++) m.Weights[k] = 0f;
            Assert.Contains("1 of its", CosmeticRules.CheckModel(CosmeticSlot.Chest, m));
        }

        [Fact]
        public void WhatIsHiddenInFirstPersonIsNamedByItsObject()
        {
            var m = Model("twoskins.glb");
            Assert.Equal(new[] { "part0", "part1" }, m.Objects.OrderBy(x => x));
            Assert.All(m.Parts, p => Assert.Equal(p.Triangles.Count, p.ObjectOf.Count * 3));
            Assert.Null(CosmeticRules.CheckHidden(new string[0], m));
            Assert.Null(CosmeticRules.CheckHidden(new[] { "part1" }, m));
            var wrong = CosmeticRules.CheckHidden(new[] { "part1", "collar" }, m);
            Assert.Contains("'collar'", wrong);
            Assert.Contains("'part0'", wrong);
            Assert.DoesNotContain("names 'part1'", wrong);

            // part1 stands at x 1..2 (make_glb.py); without it only x 0..1 is left.
            float X(int v) => m.Positions[v * 3];
            var all = m.Parts.SelectMany(p => CosmeticRules.Seen(p, m, new string[0])).ToList();
            var left = m.Parts.SelectMany(p => CosmeticRules.Seen(p, m, new[] { "part1" })).ToList();
            Assert.Equal(6, all.Count);
            Assert.Equal(3, left.Count);
            Assert.All(left, v => Assert.InRange(X(v), 0f, 1f));
            Assert.Contains(all, v => X(v) > 1.5f);
            Assert.Empty(m.Parts.SelectMany(p => CosmeticRules.Seen(p, m, new[] { "part0", "part1" })));
        }

        [Fact]
        public void ALookIsWrittenAsTheGameStoresIt()
        {
            var asked = new List<string>();
            var look = new GoblinLook { Hair = " Mod:Hat ", Chest = "Mod:Coat", Legs = "CC_Goblin_Legs", HairColor = 13, ChestColor = 11, LegsColor = 3, EyeColor = 2, Skin = 6, EyeLeft = 1, EyeRight = 4, HornColor = -5 };
            look.Accessories.AddRange(new[] { "a", "b", "none", "a", "c", "d", "e", "f", "g" });
            var ids = new Dictionary<string, uint> { ["Mod:Hat"] = 4294967295, ["Mod:Coat"] = 7, ["CC_Goblin_Legs"] = 8, ["a"] = 1, ["b"] = 2, ["c"] = 3, ["d"] = 4, ["e"] = 5, ["f"] = 6, ["g"] = 9 };
            var json = CosmeticRules.LookJson(look, (s, n) => { asked.Add($"{s} {n}"); return ids.TryGetValue(n, out var v) ? v : 0u; });
            using var d = JsonDocument.Parse(json);
            uint U(string n) => d.RootElement.GetProperty(n).GetUInt32();
            Assert.Equal(new uint[] { 0, 4294967295, 7, 8, 0 }, new[] { U("HornId"), U("HairId"), U("ChestId"), U("LegsId"), U("FeetId") });
            Assert.Equal(new uint[] { 0, 13, 11, 3, 1, 4, 2, 6 }, new[] { U("HornColorIndex"), U("HairColorIndex"), U("ChestColorIndex"), U("LegsColorIndex"), U("EyeLeftTypeIndex"), U("EyeRightTypeIndex"), U("EyeColorIndex"), U("SkinColorIndex") });
            Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 6 }, d.RootElement.GetProperty("Accessories").EnumerateArray().Select(x => x.GetUInt32()));
            Assert.Contains("Hair Mod:Hat", asked);
            Assert.DoesNotContain(asked, a => a.StartsWith("Horns") || a.StartsWith("Feet"));
        }

        [Fact]
        public void AnEmptyLookHasSixEmptyAccessories() =>
            Assert.EndsWith("\"Accessories\":[0,0,0,0,0,0]}", CosmeticRules.LookJson(new GoblinLook(), (s, n) => throw new InvalidOperationException()));

        [Fact]
        public void APictureIsToldByItsFirstBytes()
        {
            var path = Path.GetTempFileName();
            try
            {
                Assert.False(CosmeticRules.IsPng(path));
                File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A });
                Assert.False(CosmeticRules.IsPng(path));
                File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1 });
                Assert.True(CosmeticRules.IsPng(path));
                File.WriteAllText(path, "glTF binary, not a picture");
                Assert.False(CosmeticRules.IsPng(path));
            }
            finally { File.Delete(path); }
        }

        // ---- the list of what is unlocked ----

        private static string Temp() => Path.Combine(Path.GetTempPath(), "pogtests-" + Guid.NewGuid().ToString("N"), "POGLib", "Cosmetics.json");

        [Fact]
        public void UnlockedIsKept_AlsoWhatNoInstalledModKnows()
        {
            var path = Temp();
            try
            {
                var a = new UnlockList(path, s => throw new Exception(s));
                Assert.False(a.Contains("M:Coat"));
                Assert.True(a.Add("M:Coat"));
                Assert.False(a.Add("M:Coat"));
                Assert.True(a.Add("Gone:Hat"));
                var b = new UnlockList(path, s => throw new Exception(s));
                Assert.True(b.Contains("M:Coat"));
                Assert.True(b.Add("M:Belt"));
                var c = new UnlockList(path, s => throw new Exception(s));
                Assert.True(c.Contains("Gone:Hat") && c.Contains("M:Belt") && c.Contains("M:Coat"));
                Assert.Equal(new[] { Path.GetFileName(path) }, Directory.GetFiles(Path.GetDirectoryName(path)).Select(Path.GetFileName));
            }
            finally { Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(path)), true); }
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("{\"version\":1}")]
        [InlineData("{\"version\":2,\"unlocked\":[\"M:Coat\"],\"new\":true}")]
        public void AFileItCannotReadOrANewerOne_IsNotWrittenOver(string text)
        {
            var path = Temp();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text);
                int warned = 0;
                var a = new UnlockList(path, s => warned++);
                Assert.Equal(text.Contains("M:Coat"), a.Contains("M:Coat"));
                Assert.True(a.Add("M:Hat"));
                Assert.True(a.Contains("M:Hat"));
                Assert.Equal(text, File.ReadAllText(path));
                Assert.Equal(1, warned);
            }
            finally { Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(path)), true); }
        }
    
        [Theory]
        [InlineData(false, false, false, true, false)]   // an open part
        [InlineData(false, true, false, true, false)]    // "hidden while locked" means nothing for a part that is never locked
        [InlineData(true, false, false, true, true)]     // locked: there, under the padlock
        [InlineData(true, false, true, true, false)]     // unlocked: there, open
        [InlineData(true, true, false, false, false)]    // a secret: not there
        [InlineData(true, true, true, true, false)]      // a secret found: there, open
        public void TheWardrobeShowsALockedPartUnderThePadlockUnlessItIsASecret(bool locked, bool hidden, bool unlocked, bool listed, bool padlock) =>
            Assert.Equal((listed, padlock), CosmeticRules.InWardrobe(locked, hidden, unlocked));
}
}
