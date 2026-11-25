using Il2CppEntities;
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

        /// <summary>
        /// Add your mod's dungeon merchants: one class per mod (<see cref="MerchantClass"/>). Call once, from OnInitializeMelon.
        /// False when something in it is wrong (each mistake is in the log, and none of the merchants is used).
        /// </summary>
        public static bool AddMerchants(MerchantClass merchants) => ShopsPlugin.Instance != null && MerchantStalls.Add(merchants);

        /// <summary>
        /// On the host: which mod's merchant this seller is ("BerryMod:trader"); null for anyone else, and always null on a client.
        /// From the moment the game is about to dress the seller (a prefix of NPCEntity.OnNetworkPostSpawn and later).
        /// </summary>
        public static string MerchantOf(NPCEntity seller) => MerchantStalls.StallOf(seller)?.FullId;

        /// <summary>Re-read every shop file (e.g. after editing one by hand); applies from the next scene.</summary>
        public static void Reload() => ShopsPlugin.Instance?.Reload();
    }
}
