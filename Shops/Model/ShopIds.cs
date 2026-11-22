using System;
using System.Text.RegularExpressions;

namespace POGLib.Shops.Model
{
    /// <summary>
    /// Identifiers used by shop files. Everything is "owner:local":
    ///   items   — "vanilla:Item_Key" (a game prefab) or "BerryMod:HealingBerry" (a registered mod item);
    ///   slots   — "vanilla:&lt;scene&gt;/&lt;object path&gt;" (a slot placed by the game) or "BerryMod:berry_stand";
    ///   sellers — same shape as slots.
    /// The special item "nothing" means "this slot stays empty" and has a weight like any other entry.
    /// This file has no Unity dependency: the whole Model folder is unit-tested outside the game.
    /// </summary>
    internal static class ShopIds
    {
        public const string Vanilla = "vanilla";
        public const string Nothing = "nothing";

        private static readonly Regex ModIdPattern = new Regex("^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant);

        public static bool IsValidModId(string modId) =>
            !string.IsNullOrEmpty(modId) && modId != Vanilla && ModIdPattern.IsMatch(modId);

        /// <summary>Split "owner:local". False when there is no ':' or either part is empty.</summary>
        public static bool TrySplit(string id, out string owner, out string local)
        {
            owner = local = null;
            if (string.IsNullOrEmpty(id)) return false;
            int i = id.IndexOf(':');
            if (i <= 0 || i == id.Length - 1) return false;
            owner = id.Substring(0, i);
            local = id.Substring(i + 1);
            return true;
        }

        /// <summary>The owner part of an id; "vanilla" for game content, null for "nothing" or a malformed id.</summary>
        public static string OwnerOf(string id) => TrySplit(id, out var owner, out _) ? owner : null;

        /// <summary>Make an id fully qualified: "berry_stand" written in BerryMod's file means "BerryMod:berry_stand".</summary>
        public static string Qualify(string id, string defaultOwner) =>
            string.IsNullOrEmpty(id) || id == Nothing || id.IndexOf(':') >= 0 ? id : defaultOwner + ":" + id;

        public static string VanillaItem(string prefabName) => Vanilla + ":" + prefabName;

        public static string VanillaSlot(string scene, string objectPath) => Vanilla + ":" + scene + "/" + objectPath;

        /// <summary>The scene a vanilla slot or seller id lives in ("vanilla:PitOfGoblin_Lobby/..." → "PitOfGoblin_Lobby").</summary>
        public static string SceneOfVanilla(string id)
        {
            if (!TrySplit(id, out var owner, out var local) || owner != Vanilla) return null;
            int slash = local.IndexOf('/');
            return slash > 0 ? local.Substring(0, slash) : null;
        }

        public static bool IsVanilla(string id) => string.Equals(OwnerOf(id), Vanilla, StringComparison.Ordinal);
    }
}
