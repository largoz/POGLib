using System.Globalization;

namespace POGLib.Shops.Model
{
    /// <summary>
    /// What a client needs to show a mod's dungeon merchant as the host does beyond what the game sends: the name over their head.
    /// Wire: <c>1|netId|merchant's full id|name</c>; the name comes last, so a '|' inside it cannot break the fields. Format "1": a change
    /// gets a new number.
    /// </summary>
    internal sealed class SellerInfo
    {
        public const string Format = "1";

        public ulong NetworkId { get; }
        public string FullId { get; }
        public string Name { get; }

        public SellerInfo(ulong networkId, string fullId, string name)
        {
            NetworkId = networkId;
            FullId = fullId ?? "";
            Name = name ?? "";
        }

        public string Encode() => string.Join("|", Format, NetworkId.ToString(CultureInfo.InvariantCulture), FullId.Replace('|', '_'), Name);

        public static bool TryDecode(string text, out SellerInfo info)
        {
            info = null;
            var f = (text ?? "").Split(new[] { '|' }, 4);
            if (f.Length != 4 || f[0] != Format || f[2].Length == 0 || f[3].Length == 0 || f[3].Length > MerchantDraw.MaxName) return false;
            if (!ulong.TryParse(f[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id)) return false;
            info = new SellerInfo(id, f[2], f[3]);
            return true;
        }
    }
}
