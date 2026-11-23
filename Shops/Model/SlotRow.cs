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
        /// <summary>A slot higher or lower than this from the row's level is not part of the row (metres).</summary>
        public const float OffRow = 0.3f;
        /// <summary>
        /// A slot farther than this from the row's line is not part of the row (metres). The goods on Curiosa's table lie
        /// within 0.08 m of their line; at 0.3 m a display stand a metre past the table joined the row (2026-11-23).
        /// </summary>
        public const float OffLine = 0.15f;
        /// <summary>Slots closer than this along the row are not a row one can step along (metres).</summary>
        public const float MinStep = 0.15f;
        /// <summary>Slots farther apart than this along the row are not a row of goods (metres): two lone stands far apart.</summary>
        public const float MaxStep = 1.5f;

        /// <summary>
        /// The position for a new slot <paramref name="stepsPast"/> steps past <c>row[afterIndex]</c> (1 = the next free
        /// place), away from the middle of the row. <paramref name="row"/> holds the seller's slots as world positions
        /// [x, y, z]; only slots at that slot's height (within <see cref="OffRow"/>) make the row. False, with the reason
        /// in <paramref name="why"/>, when there is no row to step along or the slot is not at one of its ends.
        /// </summary>
        public static bool TryPlaceAfter(IReadOnlyList<float[]> row, int afterIndex, int stepsPast, out float[] pos, out string why)
        {
            pos = null;
            if (row == null || afterIndex < 0 || afterIndex >= row.Count || !Valid(row[afterIndex])) { why = "the slot is not one of the seller's"; return false; }
            if (stepsPast < 1) { why = "steps past must be 1 or more"; return false; }
            if (!TryRow(row, row[afterIndex][1], out var r, out why)) return false;
            if (!r.Along.Any(p => p.i == afterIndex)) { why = "the slot is off the row the seller's other slots form"; return false; }

            int dir;
            if (r.Along[r.Along.Count - 1].i == afterIndex) dir = 1;
            else if (r.Along[0].i == afterIndex) dir = -1;
            else { why = "the slot is in the middle of the row; name the slot at one of its ends"; return false; }

            var a = row[afterIndex];
            float d = dir * r.Step * stepsPast;
            pos = new[] { a[0] + r.Ax * d, a[1], a[2] + r.Az * d };
            why = null;
            return true;
        }

        /// <summary>
        /// The slot at the <paramref name="right"/> (or left) end of the seller's row, as a customer facing the seller
        /// sees it: the seller stands behind its goods, so the customer looks from the row towards the seller. The row is
        /// the level most of the seller's slots stand at. This names a place by the seller alone, so it survives the game
        /// renaming its slots (their names carry the default item) or reordering the seller's list (not spatial, seen
        /// 2026-11-21: the starter shop lists key, rock, bread, torch).
        /// </summary>
        public static bool TryEndOnSide(IReadOnlyList<float[]> row, float[] seller, bool right, out int index, out string why)
            => TrySide(row, seller, right, out _, out index, out _, out why);

        /// <summary>
        /// The position for a new slot <paramref name="stepsPast"/> steps past the <paramref name="right"/> (or left) end
        /// of the seller's row, measured on the same row that chose the end (<see cref="TryEndOnSide"/>). Placing after
        /// the end's slot id instead would fit the row again at that slot's height and could find the end in the middle
        /// (2026-11-23, Curiosa's right end: the orb).
        /// </summary>
        public static bool TryPlaceAtSide(IReadOnlyList<float[]> row, float[] seller, bool right, int stepsPast, out float[] pos, out int endIndex, out string why)
        {
            pos = null;
            if (stepsPast < 1) { endIndex = -1; why = "steps past must be 1 or more"; return false; }
            if (!TrySide(row, seller, right, out var r, out endIndex, out int dir, out why)) return false;
            var a = row[endIndex];
            float d = dir * r.Step * stepsPast;
            pos = new[] { a[0] + r.Ax * d, a[1], a[2] + r.Az * d };
            return true;
        }

        // The row at the level most of the seller's slots stand at (ties: the lower), and its end on the given side as a
        // customer facing the seller sees it; dir is +1 when that end is the last one along the row, -1 when the first.
        private static bool TrySide(IReadOnlyList<float[]> row, float[] seller, bool right, out Line r, out int index, out int dir, out string why)
        {
            r = null; index = -1; dir = 0;
            if (row == null || !Valid(seller)) { why = "the seller is not known"; return false; }
            var all = Enumerable.Range(0, row.Count).Where(i => Valid(row[i])).ToList();
            if (all.Count < 2) { why = "the seller has fewer than two slots, so there is no row to follow"; return false; }
            float level = all.Select(i => row[i][1]).OrderBy(y => -all.Count(j => Math.Abs(row[j][1] - y) <= OffRow)).ThenBy(y => y).First();
            if (!TryRow(row, level, out r, out why)) return false;
            float fx = seller[0] - r.Cx, fz = seller[2] - r.Cz;          // the customer's forward, on the floor
            if (Math.Abs(fx) + Math.Abs(fz) < 1e-3f) { why = "the seller stands in the middle of its own row, so it has no left or right"; return false; }
            float rx = fz, rz = -fx;                                      // the customer's right (Unity: up × forward)
            var first = r.Along[0].i; var last = r.Along[r.Along.Count - 1].i;
            float dFirst = (row[first][0] - r.Cx) * rx + (row[first][2] - r.Cz) * rz;
            float dLast = (row[last][0] - r.Cx) * rx + (row[last][2] - r.Cz) * rz;
            bool lastIsIt = (dLast > dFirst) == right;
            index = lastIsIt ? last : first;
            dir = lastIsIt ? 1 : -1;
            why = null;
            return true;
        }

        private sealed class Line
        {
            public float Cx, Cz, Ax, Az, Step;
            public List<(int i, float t)> Along;
        }

        // The row at height `level`: slots within OffRow of it; the line through two of them that most of them lie
        // on (one far slot cannot tilt it: 2026-11-23, the Darkburg smith's item rack 4 m from its table of weapons),
        // fitted again on those; sorted along it, with the median gap as the step.
        private static bool TryRow(IReadOnlyList<float[]> row, float level, out Line line, out string why)
        {
            line = null;
            var all = Enumerable.Range(0, row.Count).Where(i => Valid(row[i]) && Math.Abs(row[i][1] - level) <= OffRow).ToList();
            if (all.Count < 2) { why = "the seller has fewer than two slots at that height, so there is no row to follow"; return false; }
            List<int> inRow = null; double bestSpread = double.MaxValue; bool tooClose = false;
            for (int a = 0; a < all.Count; a++)
                for (int b = a + 1; b < all.Count; b++)
                {
                    var p = row[all[a]]; var q = row[all[b]];
                    float dx = q[0] - p[0], dz = q[2] - p[2], len = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (len < MinStep) { tooClose = true; continue; }
                    float ux = dx / len, uz = dz / len;
                    var on = all.Where(i => Distance(row[i], p[0], p[2], ux, uz) <= OffLine).ToList();
                    double spread = on.Sum(i => Distance(row[i], p[0], p[2], ux, uz));
                    if (inRow == null || on.Count > inRow.Count || (on.Count == inRow.Count && spread < bestSpread)) { inRow = on; bestSpread = spread; }
                }
            if (inRow == null && tooClose) { why = $"the seller's slots are closer than {MinStep:0.00} m, too close to step along"; return false; }
            if (inRow == null || inRow.Count < 2) { why = "the seller's slots do not stand in a row"; return false; }
            var (cx, cz, ax, az) = Fit(row, inRow);
            inRow = inRow.Where(i => Distance(row[i], cx, cz, ax, az) <= OffLine).ToList();
            if (inRow.Count < 2) { why = "the seller's slots do not stand in a row"; return false; }

            var along = inRow.Select(i => (i, t: (row[i][0] - cx) * ax + (row[i][2] - cz) * az)).OrderBy(p => p.t).ToList();
            var gaps = new List<float>();
            for (int k = 1; k < along.Count; k++) gaps.Add(along[k].t - along[k - 1].t);
            gaps.Sort();
            float step = gaps[gaps.Count / 2];
            if (step < MinStep) { why = $"the seller's slots are {step:0.00} m apart, too close to step along"; return false; }
            if (step > MaxStep) { why = $"the seller's slots at that height are {step:0.00} m apart, which is not a row of goods"; return false; }
            line = new Line { Cx = cx, Cz = cz, Ax = ax, Az = az, Step = step, Along = along };
            why = null;
            return true;
        }

        private static bool Valid(float[] p) => p != null && p.Length == 3;

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
