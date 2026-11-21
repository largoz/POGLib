using System;
using System.Collections.Generic;
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
        public void SlotsOnTopOfEachOtherAreNoRow()
        {
            var stacked = new List<float[]> { new[] { 1f, 0f, 1f }, new[] { 1.05f, 0.5f, 1f } };
            Assert.False(SlotRow.TryPlaceAfter(stacked, 1, 1, out _, out var why));
            Assert.Contains("too close", why);
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
