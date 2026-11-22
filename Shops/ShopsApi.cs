using POGLib.Shops.Model;

namespace POGLib.Shops
{
    /// <summary>
    /// Shops from code (POGShops). Most mods need none of this: ship a pogshop.json and Shops finds it (SHOPS.md).
    /// Use this to add a shop file built in code; it is a mod's file, so the same limits apply (its own slots and how
    /// its items stand).
    /// </summary>
    public static class ShopsApi
    {
        /// <summary>Add a shop file built in code. Same checks as a file on disk. Call from OnInitializeMelon.</summary>
        public static bool AddFile(ShopFile file) => ShopsPlugin.Instance != null && ShopsPlugin.Instance.AddFile(file);

        /// <summary>Re-read every shop file (e.g. after editing one by hand); applies from the next scene.</summary>
        public static void Reload() => ShopsPlugin.Instance?.Reload();
    }
}
