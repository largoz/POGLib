using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using POGLib.Core;

namespace POGLib.Cosmetics.Model
{
    /// <summary>What a cosmetic must be to be taken, and how a look is written. No Unity types, so the unit tests compile it.</summary>
    internal static class CosmeticRules
    {
        /// <summary>The game finds a wearer's bones by these names only (its Stitcher's catalogue, from Hips down).</summary>
        public const string BonePrefix = "mixamorig:";

        /// <summary>The game's own limit (CustomGoblinService.MaxNumAccessories).</summary>
        public const int MaxAccessories = 6;

        /// <summary>Null, or what is wrong with the ids. They are written into looks and lists as "Mod:Part".</summary>
        /// <summary>Whether the wardrobe lists a part, and with the padlock: its own, or one of a part the player has not unlocked yet.</summary>
        public static (bool listed, bool padlock) InWardrobe(bool locked, bool hiddenWhileLocked, bool unlocked)
        {
            bool shut = locked && !unlocked;
            return shut ? (!hiddenWhileLocked, !hiddenWhileLocked) : (true, false);
        }

        public static string CheckIds(string modId, string contentId)
        {
            foreach (var (name, value) in new[] { ("ModId", modId), ("ContentId", contentId) })
            {
                if (string.IsNullOrWhiteSpace(value)) return $"{name} is empty";
                if (value.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')))
                    return $"{name} '{value}': only letters, digits, '_', '-' and '.'";
            }
            return null;
        }

        private static readonly byte[] PngMark = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public static bool IsPng(string path)
        {
            using var f = File.OpenRead(path);
            var head = new byte[PngMark.Length];
            return f.Read(head, 0, head.Length) == head.Length && head.SequenceEqual(PngMark);
        }

        /// <summary>Whether the model is bound to the skeleton; otherwise it sits on the head.</summary>
        public static bool OnBody(GlbModel m) => m.JointNames.Count > 0 && m.Unweighted < m.VertexCount;

        /// <summary>Null, or what is wrong with the model for this slot.</summary>
        public static string CheckModel(CosmeticSlot slot, GlbModel m)
        {
            if (!OnBody(m))
                return slot == CosmeticSlot.Chest || slot == CosmeticSlot.Legs || slot == CosmeticSlot.Feet
                    ? $"{slot} is worn on the body: its model must be weighted to the goblin's bones (an Armature in Blender)" : null;
            if (m.Unweighted > 0) return $"{m.Unweighted} of its {m.VertexCount} vertices have no weights: weight every mesh, or none";
            var used = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < m.Joints.Count; i++) if (m.Weights[i] > 0f) used.Add(m.JointNames[m.Joints[i]]);
            var other = used.Where(n => !n.StartsWith(BonePrefix, StringComparison.Ordinal)).ToList();
            return other.Count > 0 ? $"it is weighted to {string.Join(", ", other)}: the game finds only the bones named {BonePrefix}..." : null;
        }

        /// <summary>Null, or which of the objects to hide are not in the model.</summary>
        public static string CheckHidden(IEnumerable<string> hidden, GlbModel m)
        {
            var none = hidden.Where(h => !m.Objects.Contains(h)).ToList();
            return none.Count == 0 ? null : $"HiddenInFirstPerson names {string.Join(", ", none.Select(n => $"'{n}'"))}; its objects are {string.Join(", ", m.Objects.Select(n => $"'{n}'"))}";
        }

        /// <summary>A part's triangles without those of the hidden objects (three indices each).</summary>
        public static List<int> Seen(GlbPart p, GlbModel m, ICollection<string> hidden)
        {
            var seen = new List<int>();
            for (int t = 0; t < p.ObjectOf.Count; t++)
                if (!hidden.Contains(m.Objects[p.ObjectOf[t]]))
                    for (int k = 0; k < 3; k++) seen.Add(p.Triangles[t * 3 + k]);
            return seen;
        }

        /// <summary>The bones the model is weighted to that the wearer's skeleton does not have.</summary>
        public static List<string> MissingBones(GlbModel m, ICollection<string> skeleton)
        {
            var missing = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < m.Joints.Count; i++)
                if (m.Weights[i] > 0f && !skeleton.Contains(m.JointNames[m.Joints[i]])) missing.Add(m.JointNames[m.Joints[i]]);
            return missing.ToList();
        }

        /// <summary>A look as the game stores it. <paramref name="id"/> gives a part's number, 0 for none.</summary>
        public static string LookJson(GoblinLook l, Func<CosmeticSlot, string, uint> id)
        {
            string N(int v) => Math.Max(0, v).ToString(CultureInfo.InvariantCulture);
            uint P(CosmeticSlot s, string name) => string.IsNullOrWhiteSpace(name) ? 0u : id(s, name.Trim());
            var acc = l.Accessories.Select(a => P(CosmeticSlot.Accessory, a)).Where(a => a != 0).Distinct().Take(MaxAccessories).ToList();
            while (acc.Count < MaxAccessories) acc.Add(0);
            return $"{{\"HornId\":{P(CosmeticSlot.Horns, l.Horns)},\"HairId\":{P(CosmeticSlot.Hair, l.Hair)},\"ChestId\":{P(CosmeticSlot.Chest, l.Chest)}," +
                   $"\"LegsId\":{P(CosmeticSlot.Legs, l.Legs)},\"FeetId\":{P(CosmeticSlot.Feet, l.Feet)}," +
                   $"\"HornColorIndex\":{N(l.HornColor)},\"HairColorIndex\":{N(l.HairColor)},\"ChestColorIndex\":{N(l.ChestColor)},\"LegsColorIndex\":{N(l.LegsColor)}," +
                   $"\"EyeLeftTypeIndex\":{N(l.EyeLeft)},\"EyeRightTypeIndex\":{N(l.EyeRight)},\"EyeColorIndex\":{N(l.EyeColor)},\"SkinColorIndex\":{N(l.Skin)}," +
                   $"\"Accessories\":[{string.Join(",", acc)}]}}";
        }
    }
}
