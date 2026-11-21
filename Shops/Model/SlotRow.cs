using System;
using System.Collections.Generic;
using System.Linq;

namespace POGLib.Shops.Model
{
    /// <summary>
    /// Where a new slot goes when a shop file says <c>"after": "&lt;slot id&gt;"</c> instead of coordinates: one step past
    /// that slot along the row the seller's own slots form, at that slot's height. The game lays a seller's goods out in
    /// a row (the hub's starter shop: four on the mat, about 0.45 m apart); a slot off the row (a big item on its own
    /// stand) is left out of the fit. Pure geometry on the floor plane (x, z), so it is tested without the game.
    /// </summary>
    internal static class SlotRow
    {
        /// <summary>A slot farther than this from the row's line is not part of the row (metres).</summary>
        public const float OffRow = 0.3f;
        /// <summary>Slots closer than this along the row are not a row one can step along (metres).</summary>
        public const float MinStep = 0.15f;

        /// <summary>
        /// The position for a new slot <paramref name="stepsPast"/> steps past <c>row[afterIndex]</c> (1 = the next free
        /// place), away from the middle of the row. <paramref name="row"/> holds the seller's slots as world positions
        /// [x, y, z]. False, with the reason in <paramref name="why"/>, when there is no row to step along or the slot
        /// is not at one of its ends.
        /// </summary>
        public static bool TryPlaceAfter(IReadOnlyList<float[]> row, int afterIndex, int stepsPast, out float[] pos, out string why)
        {
            pos = null;
            if (row == null || afterIndex < 0 || afterIndex >= row.Count) { why = "the slot is not one of the seller's"; return false; }
            if (stepsPast < 1) { why = "steps past must be 1 or more"; return false; }
            var all = Enumerable.Range(0, row.Count).Where(i => row[i] != null && row[i].Length == 3).ToList();
            if (all.Count < 2) { why = "the seller has fewer than two slots, so there is no row to follow"; return false; }

            // Fit the row's line, drop slots off it, fit again on the rest.
            var (cx, cz, ax, az) = Fit(row, all);
            var inRow = all.Where(i => Distance(row[i], cx, cz, ax, az) <= OffRow).ToList();
            if (inRow.Count >= 2 && inRow.Count < all.Count) (cx, cz, ax, az) = Fit(row, inRow);
            inRow = all.Where(i => Distance(row[i], cx, cz, ax, az) <= OffRow).ToList();
            if (inRow.Count < 2) { why = "the seller's slots do not stand in a row"; return false; }
            if (!inRow.Contains(afterIndex)) { why = "the slot is off the row the seller's other slots form"; return false; }

            var along = inRow.Select(i => (i, t: (row[i][0] - cx) * ax + (row[i][2] - cz) * az)).OrderBy(p => p.t).ToList();
            var gaps = new List<float>();
            for (int k = 1; k < along.Count; k++) gaps.Add(along[k].t - along[k - 1].t);
            gaps.Sort();
            float step = gaps[gaps.Count / 2];
            if (step < MinStep) { why = $"the seller's slots are {step:0.00} m apart, too close to step along"; return false; }

            int dir;
            if (along[along.Count - 1].i == afterIndex) dir = 1;
            else if (along[0].i == afterIndex) dir = -1;
            else { why = "the slot is in the middle of the row; name the slot at one of its ends"; return false; }

            var a = row[afterIndex];
            float d = dir * step * stepsPast;
            pos = new[] { a[0] + ax * d, a[1], a[2] + az * d };
            why = null;
            return true;
        }

        // Centre and unit direction of the line through the given slots (principal axis on the floor plane).
        private static (float cx, float cz, float ax, float az) Fit(IReadOnlyList<float[]> row, List<int> idx)
        {
            float cx = idx.Average(i => row[i][0]), cz = idx.Average(i => row[i][2]);
            double sxx = 0, szz = 0, sxz = 0;
            foreach (var i in idx)
            {
                double dx = row[i][0] - cx, dz = row[i][2] - cz;
                sxx += dx * dx; szz += dz * dz; sxz += dx * dz;
            }
            double angle = 0.5 * Math.Atan2(2 * sxz, sxx - szz);
            return (cx, cz, (float)Math.Cos(angle), (float)Math.Sin(angle));
        }

        private static float Distance(float[] p, float cx, float cz, float ax, float az)
            => Math.Abs((p[0] - cx) * az - (p[2] - cz) * ax);
    }
}
