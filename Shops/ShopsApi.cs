using System.Collections.Generic;
using POGLib.Shops.Model;

namespace POGLib.Shops
{
    /// <summary>
    /// Shops from code (POGShops). Most mods need none of this: ship a pogshop.json and Shops finds it (see SHOPS.md).
    /// Use this to add a shop file built in code, or to inspect what the rules resolved to.
    /// </summary>
    public static class ShopsApi
    {
        /// <summary>Add a shop file built in code. Same checks as a file on disk. Call from OnInitializeMelon.</summary>
        public static bool AddFile(ShopFile file) => ShopsPlugin.Instance != null && ShopsPlugin.Instance.AddFile(file);

        /// <summary>Every rule from every loaded shop file (null before the first scene load).</summary>
        public static ShopCatalog Catalog => ShopsPlugin.Instance?.Catalog;

        /// <summary>What happened to each slot in the scenes loaded this session (host only).</summary>
        public static IReadOnlyDictionary<string, SlotRecord> Slots => ShopRuntime.Records;

        /// <summary>Re-read every shop file (e.g. after editing one by hand).</summary>
        public static void Reload() => ShopsPlugin.Instance?.Reload();
    }
}
