using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace POGLib.Core
{
    /// <summary>
    /// Read-only lookups in the game's Addressables catalog (2026-11-14 build). An address is the asset path, e.g.
    /// "Assets/Gameplay/Pickups/Food/Food Tier 1 Items/Item_Bread.prefab", and the file name is the prefab name
    /// (measured on the hub's slots). Each lookup takes and releases its own handle, so the game's own handles and
    /// load state are never touched. Only by key: enumerating the catalog through the interface proxies crashed
    /// the game (2026-11-14).
    /// </summary>
    internal static class Addresses
    {
        /// <summary>The address (primary key) a GUID or key resolves to, or null.</summary>
        public static string AddressOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var h = Addressables.LoadResourceLocationsAsync((Il2CppSystem.Object)(Il2CppSystem.String)key, null);
            try
            {
                var list = h.WaitForCompletion();
                return Count(list) > 0 ? list[0].PrimaryKey : null;
            }
            finally { Addressables.Release(h); }
        }

        /// <summary>"…/Item_Bread.prefab" → "Item_Bread"; null stays null.</summary>
        public static string NameOf(string address)
        {
            if (string.IsNullOrEmpty(address)) return null;
            int slash = address.LastIndexOf('/');
            string file = slash >= 0 ? address.Substring(slash + 1) : address;
            int dot = file.LastIndexOf('.');
            return dot > 0 ? file.Substring(0, dot) : file;
        }

        /// <summary>The prefab name an asset reference points at, without loading the asset; null when unknown.</summary>
        public static string PrefabNameOf(AssetReference r)
        {
            try { return r == null ? null : NameOf(AddressOf(r.AssetGUID)); }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[POGLib] address lookup failed: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Load an asset by key right now with a handle of its own, hand it to <paramref name="read"/>, release.
        /// For assets that are small and that the game loads anyway (a slot's loot table).
        /// </summary>
        public static TResult ReadNow<TAsset, TResult>(string key, Func<TAsset, TResult> read) where TAsset : Il2CppObjectBase
        {
            if (string.IsNullOrEmpty(key)) return default;
            var h = Addressables.LoadAssetAsync<TAsset>((Il2CppSystem.Object)(Il2CppSystem.String)key);
            try { var asset = h.WaitForCompletion(); return asset == null ? default : read(asset); }
            finally { Addressables.Release(h); }
        }

        private static int Count(Il2CppSystem.Collections.Generic.IList<IResourceLocation> l) =>
            l == null ? 0 : l.Cast<Il2CppSystem.Collections.Generic.ICollection<IResourceLocation>>().Count;
    }
}
