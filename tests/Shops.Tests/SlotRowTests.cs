using System;
using System.Collections.Generic;
using System.Linq;
using POGLib.Shops.Model;
using Xunit;

namespace POGLib.Shops.Tests
{
    public class SlotRowTests
    {
        // The hub's starter shop on build 26668562, from the host's log in LAN run 22 (2026-11-21): torch, bread, key and
        // rock on the mat, and the big item's slot on its own stand beside them.
        static readonly List<float[]> StarterShop = new List<float[]>
        {
            new[] { 8.8f, 3.8f, -11.7f },   // 0 torch
            new[] { 8.8f, 3.7f, -12.2f },   // 1 bread
            new[] { 8.8f, 3.8f, -12.6f },   // 2 key
            new[] { 8.9f, 3.8f, -13.1f },   // 3 rock
            new[] { 9.6f, 3.7f, -13.0f },   // 4 big item, off the row
        };

        static void Near(float want, float got, float tol = 0.06f) =>
            Assert.True(Math.Abs(want - got) <= tol, $"want {want}, got {got}");

        [Fact]
        public void AfterTheLastSlotIsOneStepFurtherAlongTheRow()
        {
            Assert.True(SlotRow.TryPlaceAfter(StarterShop, 3, 1, out var p, out var why), why);
            Near(8.93f, p[0]); Near(3.8f, p[1], 0.001f); Near(-13.57f, p[2]);
        }

        [Fact]
        public void AfterTheFirstSlotGoesTheOtherWay()
        {
            Assert.True(SlotRow.TryPlaceAfter(StarterShop, 0, 1, out var p, out var why), why);
            Near(8.77f, p[0]); Near(-11.23f, p[2]);
        }

        [Fact]
        public void ASecondNewSlotGoesAStepFurther()
        {
            Assert.True(SlotRow.TryPlaceAfter(StarterShop, 3, 1, out var p1, out _));
            Assert.True(SlotRow.TryPlaceAfter(StarterShop, 3, 2, out var p2, out _));
            float gap = (float)Math.Sqrt(Math.Pow(p2[0] - p1[0], 2) + Math.Pow(p2[2] - p1[2], 2));
            Near(0.5f, gap, 0.02f);   // the median gap between the four: 0.4, 0.5, 0.5
        }

        [Fact]
        public void TheBigItemStandIsNotPartOfTheRow()
        {
            Assert.False(SlotRow.TryPlaceAfter(StarterShop, 4, 1, out _, out var why));
            Assert.Contains("off the row", why);
        }

        [Fact]
        public void ASlotInTheMiddleIsRefused()
        {
            Assert.False(SlotRow.TryPlaceAfter(StarterShop, 1, 1, out _, out var why));
            Assert.Contains("middle", why);
        }

        [Fact]
        public void OneSlotIsNoRow()
        {
            Assert.False(SlotRow.TryPlaceAfter(new List<float[]> { new[] { 0f, 0f, 0f } }, 0, 1, out _, out var why));
            Assert.Contains("fewer than two", why);
        }

        [Fact]
        public void SlotsTooCloseAreNoRow()
        {
            var close = new List<float[]> { new[] { 1f, 0f, 1f }, new[] { 1.05f, 0f, 1f } };
            Assert.False(SlotRow.TryPlaceAfter(close, 1, 1, out _, out var why));
            Assert.Contains("too close", why);
        }

        [Fact]
        public void SlotsAtAnotherHeightAreNotInTheRow()
        {
            var stacked = new List<float[]> { new[] { 1f, 0f, 1f }, new[] { 1.5f, 0.7f, 1f } };
            Assert.False(SlotRow.TryPlaceAfter(stacked, 1, 1, out _, out var why));
            Assert.Contains("at that height", why);
        }

        // The hub's merchant Curiosa (dump, 2026-11-21), in its own list order: potion, ward, orb, dice on a table in a
        // row; a cog shown 0.7 m higher; armour and helmet on a dummy a metre away; a treasure display beside.
        static readonly List<float[]> Curiosa = new List<float[]>
        {
            new[] { 37.95f, 12.34f, -26.37f },   // 0 potion
            new[] { 38.30f, 12.34f, -25.97f },   // 1 ward
            new[] { 37.61f, 12.34f, -26.64f },   // 2 orb
            new[] { 38.46f, 12.32f, -25.66f },   // 3 dice
            new[] { 38.70f, 13.07f, -25.55f },   // 4 cog, higher
            new[] { 36.50f, 12.40f, -26.61f },   // 5 armour, on the dummy
            new[] { 36.50f, 12.90f, -26.59f },   // 6 helmet, on the dummy
            new[] { 38.81f, 12.03f, -24.48f },   // 7 treasure display
        };
        static readonly float[] CuriosaSeller = { 38.99f, 11.94f, -26.90f };

        // The starter shop as the seller lists it (not in spatial order): key, rock, bread, torch, big item.
        static readonly List<float[]> StarterListed = new List<float[]>
        {
            new[] { 8.85f, 3.84f, -12.64f },     // 0 key
            new[] { 8.88f, 3.83f, -13.08f },     // 1 rock
            new[] { 8.77f, 3.71f, -12.23f },     // 2 bread
            new[] { 8.79f, 3.78f, -11.75f },     // 3 torch
            new[] { 9.64f, 3.71f, -13.00f },     // 4 big item
        };
        static readonly float[] StarterSeller = { 9.76f, 3.88f, -12.21f };

        [Fact]
        public void TheStarterShopsEndsAreTheRockOnTheRightAndTheTorchOnTheLeft()
        {
            // The seller faces -x, the customer +x; the customer's right is -z, where the rock is.
            Assert.True(SlotRow.TryEndOnSide(StarterListed, StarterSeller, true, out int right, out var why), why);
            Assert.Equal(1, right);
            Assert.True(SlotRow.TryEndOnSide(StarterListed, StarterSeller, false, out int left, out why), why);
            Assert.Equal(3, left);
        }

        [Fact]
        public void CuriosasRowIsTheTableNotTheCogNorTheDummy()
        {
            Assert.True(SlotRow.TryEndOnSide(Curiosa, CuriosaSeller, true, out int right, out var why), why);
            Assert.Equal(2, right);   // orb
            Assert.True(SlotRow.TryEndOnSide(Curiosa, CuriosaSeller, false, out int left, out why), why);
            Assert.Equal(3, left);    // dice
            Assert.True(SlotRow.TryPlaceAfter(Curiosa, left, 1, out var p, out why), why);
            Near(12.32f, p[1], 0.001f);   // at the table's height, not the cog's
        }

        // 2026-11-23, both sides through TryPlaceAtSide: the right end used to be found on one fit of the row and stepped
        // from on another, which put Curiosa's orb in the middle and refused the slot.
        [Fact]
        public void BothEndsOfCuriosasTableTakeASlot()
        {
            Assert.True(SlotRow.TryPlaceAtSide(Curiosa, CuriosaSeller, true, 1, out var r, out int ri, out var why), why);
            Assert.Equal(2, ri);   // past the orb
            Assert.True(SlotRow.TryPlaceAtSide(Curiosa, CuriosaSeller, false, 1, out var l, out int li, out why), why);
            Assert.Equal(3, li);   // past the dice
            Near(12.34f, r[1], 0.001f); Near(12.32f, l[1], 0.001f);
        }

        // The Darkburg smith (dump 2026-11-23): dagger, axe and pickaxe on a table; an item rack 4 m away at almost the
        // same height, an anvil, armour on a dummy. One far slot must not tilt the row.
        static readonly List<float[]> Smith = new List<float[]>
        {
            new[] { 2.04f, -17.62f, 24.70f },   // 0 dagger
            new[] { 2.66f, -17.62f, 24.96f },   // 1 axe
            new[] { 1.41f, -17.62f, 24.49f },   // 2 pickaxe
            new[] { 3.98f, -17.45f, 20.92f },   // 3 item rack
            new[] { 5.58f, -17.15f, 21.18f },   // 4 anvil
            new[] { 2.50f, -17.06f, 20.45f },   // 5 armour
            new[] { 2.50f, -16.57f, 20.47f },   // 6 helmet
        };
        static readonly float[] SmithSeller = { 1.64f, -18.01f, 26.12f };

        [Fact]
        public void TheSmithsTableIsARowDespiteTheFarRack()
        {
            Assert.True(SlotRow.TryPlaceAtSide(Smith, SmithSeller, false, 1, out var l, out int li, out var why), why);
            Assert.True(SlotRow.TryPlaceAtSide(Smith, SmithSeller, true, 1, out var r, out int ri, out why), why);
            Assert.Equal(new[] { 1, 2 }, new[] { li, ri }.OrderBy(x => x).ToArray());   // the ends are the axe and the pickaxe
            float gap = (float)Math.Sqrt(Math.Pow(r[0] - l[0], 2) + Math.Pow(r[2] - l[2], 2));
            Assert.True(gap < 4f, $"the two new slots are {gap} m apart: the row was taken through the rack");
        }

        [Fact]
        public void TheCogAndTheHelmetAreNoRow()
        {
            Assert.False(SlotRow.TryPlaceAfter(Curiosa, 4, 1, out _, out var why));
            Assert.Contains("not a row of goods", why);
        }

        [Fact]
        public void ARowAlongADiagonalIsFollowed()
        {
            var diag = new List<float[]> { new[] { 0f, 1f, 0f }, new[] { 0.5f, 1f, 0.5f }, new[] { 1f, 1f, 1f } };
            Assert.True(SlotRow.TryPlaceAfter(diag, 2, 1, out var p, out var why), why);
            Near(1.5f, p[0], 0.001f); Near(1.5f, p[2], 0.001f);
        }
    }
}
