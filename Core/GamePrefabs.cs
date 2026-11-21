using System;
using Il2CppGame.Networking;
using MelonLoader;
using Unity.Netcode;
using UnityEngine;

namespace POGLib.Core
{
    /// <summary>
    /// The game's own list of network prefabs. Since the 2026-11-14 update the game keeps its prefabs in
    /// Addressables bundles: nothing (no item, no NPC) is in memory at the main menu. NetworkHandler loads
    /// <c>CoreNetworkPrefabs</c> (647 entries, 218 items, measured 2026-11-14) by address in
    /// <c>EnsureNetworkPrefabsRegistered</c>, which the game calls right before it hosts or joins; from then on every
    /// network prefab, and so every item shell, is loaded. The list is read through its concrete
    /// <c>List&lt;NetworkPrefab&gt;</c>: enumerating Addressables interfaces through the proxies crashed the game.
    /// </summary>
    internal static class GamePrefabs
    {
        /// <summary>The loaded list, or null before the game has loaded it.</summary>
        public static NetworkPrefabsList Loaded
        {
            get
            {
                try { return NetworkHandler.Singleton?.m_loadedNetworkPrefabsList; }
                catch { return null; }
            }
        }

        public static bool IsLoaded => Loaded?.List != null;

        /// <summary>
        /// Make the game load its list now (what it does itself before hosting or joining). Returns whether the list
        /// is loaded afterwards. Safe to call repeatedly: the game calls it several times per session itself.
        /// </summary>
        public static bool EnsureLoaded()
        {
            if (IsLoaded) return true;
            try
            {
                var h = NetworkHandler.Singleton;
                if (h == null) return false;
                h.EnsureNetworkPrefabsRegistered();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[POGLib] Could not make the game load its network prefab list: {ex.GetType().Name}: {ex.Message}");
            }
            return IsLoaded;
        }

        /// <summary>A prefab of the game's list by GameObject name, or null.</summary>
        public static GameObject Find(string prefabName)
        {
            var list = Loaded?.List;
            if (list == null || string.IsNullOrEmpty(prefabName)) return null;
            for (int i = 0; i < list.Count; i++)
            {
                var go = list[i]?.Prefab;
                if (go != null && go.name == prefabName) return go;
            }
            return null;
        }
    }
}
