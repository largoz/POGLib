using System;
using System.Globalization;
using System.Linq;

namespace POGLib.Shops.Model
{
    /// <summary>
    /// What a client needs to show a mod slot that the host spawned from the game's own slot prefab (R1): the game
    /// replicates the slot object, its place and its item, but not its children, so the client learns from the host
    /// which slot it is, where its price tag sits and whether the tag is hidden (a slot the rules left empty).
    /// Wire: <c>1|netId|hidden|tagPos|tagRot|slotId</c>, numbers in the invariant culture, a vector as <c>x,y,z</c> or
    /// <c>-</c> when not set; the slot id comes last, so a '|' inside it cannot break the fields. Format "1": a change
    /// gets a new number.
    /// </summary>
    internal sealed class SlotInfo
    {
        public const string Format = "1";

        public ulong NetworkId { get; }
        public string SlotId { get; }
        public float[] TagPos { get; }
        public float[] TagRot { get; }
        public bool TagHidden { get; }

        public SlotInfo(ulong networkId, string slotId, float[] tagPos, float[] tagRot, bool tagHidden)
        {
            NetworkId = networkId;
            SlotId = slotId ?? "";
            TagPos = Valid(tagPos) ? tagPos : null;
            TagRot = Valid(tagRot) ? tagRot : null;
            TagHidden = tagHidden;
        }

        private static bool Valid(float[] v) => v != null && v.Length == 3 && v.All(x => !float.IsNaN(x) && !float.IsInfinity(x));

        public string Encode() => string.Join("|", Format, NetworkId.ToString(CultureInfo.InvariantCulture), TagHidden ? "1" : "0",
            Vec(TagPos), Vec(TagRot), SlotId);

        private static string Vec(float[] v) => v == null ? "-" : string.Join(",", v.Select(x => x.ToString("R", CultureInfo.InvariantCulture)));

        public static bool TryDecode(string text, out SlotInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(text)) return false;
            var f = text.Split(new[] { '|' }, 6);
            if (f.Length != 6 || f[0] != Format || f[5].Length == 0) return false;
            if (!ulong.TryParse(f[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id)) return false;
            if (f[2] != "0" && f[2] != "1") return false;
            if (!TryVec(f[3], out var pos) || !TryVec(f[4], out var rot)) return false;
            info = new SlotInfo(id, f[5], pos, rot, f[2] == "1");
            return true;
        }

        private static bool TryVec(string s, out float[] v)
        {
            v = null;
            if (s == "-") return true;
            var parts = s.Split(',');
            if (parts.Length != 3) return false;
            var r = new float[3];
            for (int i = 0; i < 3; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out r[i]) || float.IsNaN(r[i]) || float.IsInfinity(r[i])) return false;
            v = r;
            return true;
        }

        /// <summary>A client's question about one spawned object: its network id, as text.</summary>
        public static string Ask(ulong networkId) => networkId.ToString(CultureInfo.InvariantCulture);

        public static bool TryReadAsk(string text, out ulong networkId) =>
            ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out networkId);
    }
}
