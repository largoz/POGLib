using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppGame.Spawning;
using MelonLoader;

namespace POGLib.Shops
{
    /// <summary>
    /// The one hook the shop rules need: right before a slot spawns on the network (where the game decides
    /// what it offers), let the host choose by weight. A prefix that throws must never break the game's
    /// own slot, so everything is guarded.
    /// </summary>
    [HarmonyPatch(typeof(ShopLocation), nameof(ShopLocation.OnNetworkSpawn))]
    internal static class Patch_ShopLocation_OnNetworkSpawn
    {
        static void Prefix(ShopLocation __instance)
        {
            try { ShopRuntime.OnSlotSpawning(__instance); }
            catch (Exception ex) { MelonLogger.Error($"[POGLib] [Shops] stocking failed, the game's own choice stays: {ex}"); }
        }

        static void Postfix(ShopLocation __instance)
        {
            // A mod slot spawned from our template: an ordinary scene object from here on (see TemplateCopies).
            try { if (__instance != null && __instance.gameObject.name.StartsWith(ShopScene.ModSlotPrefix)) POGLib.Core.TemplateCopies.Release(__instance.gameObject); }
            catch (Exception ex) { MelonLogger.Warning($"[POGLib] [Shops] {ex.GetType().Name}: {ex.Message}"); }
            try { ShopEmptySlots.HideTagIfEmpty(__instance); }
            catch (Exception ex) { MelonLogger.Warning($"[POGLib] [Shops] {ex.GetType().Name}: {ex.Message}"); }
            // A client asks the host what a runtime copy of the game's slot prefab is (R1).
            try { SlotSync.OnClientSlotSpawned(__instance); }
            catch (Exception ex) { MelonLogger.Warning($"[POGLib] [Shops] asking the host about a slot: {ex.GetType().Name}: {ex.Message}"); }
        }
    }

    /// <summary>A dungeon merchant of a mod's is dressed as theirs (host; the look reaches everyone through the game).</summary>
    [HarmonyPatch(typeof(Il2CppEntities.NPCEntity), nameof(Il2CppEntities.NPCEntity.OnNetworkPostSpawn))]
    internal static class Patch_NPCEntity_OnNetworkPostSpawn
    {
        [HarmonyPriority(Priority.High)]   // before a content mod's own prefix, which may ask ShopsApi.MerchantOf
        static void Prefix(Il2CppEntities.NPCEntity __instance)
        {
            try { MerchantStalls.BeforeSeller(__instance); }
            catch (Exception ex) { MelonLogger.Error($"[POGLib] [Shops] dressing a merchant failed, the game's own look stays: {ex}"); }
        }
    }

    [HarmonyPatch(typeof(Il2CppInventory.EnemyInventory), nameof(Il2CppInventory.EnemyInventory.OnNetworkPostSpawn))]
    internal static class Patch_EnemyInventory_OnNetworkPostSpawn
    {
        static void Prefix(Il2CppInventory.EnemyInventory __instance)
        {
            try { MerchantStalls.BeforeEquipment(__instance); }
            catch (Exception ex) { MelonLogger.Warning($"[POGLib] [Shops] a merchant's equipment: {ex.GetType().Name}: {ex.Message}"); }
        }
    }

    /// <summary>
    /// D8: a shop slot places its item by the slot's holder (<c>ShopLocation.get_SpawnTransform</c>: holder pose times
    /// the inverse of the item's <c>m_standingTransform</c>; build 26668562), and the holder is posed for the slot's
    /// vanilla item: the torch slot lays its torch flat and laid the Ember Lantern on its side too (2026-11-19, host
    /// and client). Right after that, still on the host and before the item spawns, <c>CreateShopItems</c> calls
    /// <c>ClientNetworkParenting.SetParentBeforeSpawn(slot)</c>, which records the item's pose relative to the slot;
    /// every client puts the item at that recorded pose (<c>OnNetworkSpawn</c> -> <c>Synch</c>). A mod item is stood
    /// upright just before that record, so host and clients get the same upright pose.
    /// Earlier tries that never ran on the host: <c>ShopLocation.SetShopItemInternal</c> (only the client's
    /// <c>ResolveShopItemOnClient</c> calls it) and <c>SpawnerBase.InstantiateNetworkPrefab</c> (no log line in the
    /// 2026-11-19 08:33 run; CreateShopItems reaches it through the vtable).
    /// </summary>
    [HarmonyPatch(typeof(Il2Cpp.ClientNetworkParenting), nameof(Il2Cpp.ClientNetworkParenting.SetParentBeforeSpawn))]
    internal static class Patch_ClientNetworkParenting_SetParentBeforeSpawn
    {
        static void Prefix(Il2Cpp.ClientNetworkParenting __instance, Unity.Netcode.NetworkObject parent)
        {
            try
            {
                if (__instance == null || parent == null) return;
                var slot = parent.GetComponent<ShopLocation>();
                if (slot == null) return;
                ShopItemPose.StandUpright(slot, __instance.GetComponent<Il2Cpp.InventoryItem>());
            }
            catch (Exception ex) { MelonLogger.Warning($"[POGLib] [Shops] standing an item upright: {ex.GetType().Name}: {ex.Message}"); }
        }
    }

    internal static class ShopItemPose
    {
        /// <summary>Tilt below this many degrees is left as the slot made it.</summary>
        public const float TiltToleranceDegrees = 5f;
        private static readonly HashSet<string> _logged = new HashSet<string>(StringComparer.Ordinal);

        public static void StandUpright(ShopLocation slot, Il2Cpp.InventoryItem item)
        {
            if (slot == null || item == null) return;
            var items = POGLib.Items.ItemsPlugin.Instance;
            if (items == null || !items.IsModItem(item)) return;

            var t = item.transform;
            float tilt = UnityEngine.Vector3.Angle(t.up, UnityEngine.Vector3.up);
            string key = slot.gameObject.name + "|" + item.m_itemID;
            bool first = _logged.Add(key);
            if (tilt <= TiltToleranceDegrees)
            {
                if (first) MelonLogger.Msg($"[POGLib] [Shops] {item.m_itemID} in '{slot.gameObject.name}': upright already (tilt {tilt:0}°).");
                return;
            }

            // Turn about the standing point (the spot that sits on the holder), so the item keeps its place on the
            // shelf. Heading: where the item's front pointed, flattened; a front that pointed straight up or down
            // gives no heading, so the direction its top pointed is used instead.
            var stand = item.m_standingTransform;
            var pivot = stand != null ? stand.position : t.position;
            var heading = UnityEngine.Vector3.ProjectOnPlane(t.forward, UnityEngine.Vector3.up);
            if (heading.sqrMagnitude < 1e-4f) heading = UnityEngine.Vector3.ProjectOnPlane(t.up, UnityEngine.Vector3.up);
            if (heading.sqrMagnitude < 1e-4f) heading = UnityEngine.Vector3.forward;
            var upright = UnityEngine.Quaternion.LookRotation(heading.normalized, UnityEngine.Vector3.up);
            t.rotation = upright;
            if (stand != null) t.position += pivot - stand.position;

            if (first)
                MelonLogger.Msg($"[POGLib] [Shops] {item.m_itemID} in '{slot.gameObject.name}': stood upright (the slot tilted it {tilt:0}°).");
        }
    }
}

namespace POGLib.Shops
{
    /// <summary>
    /// The host's choice per slot, handed to the game's own pipeline (2026-11-14 build, read from traces of the
    /// hub's 13 slots): CreateShopItems takes a fixed item through LoadNetworkPrefab → CanSpawn →
    /// InstantiateNetworkPrefab (which calls LoadNetworkPrefab again), and a pool slot, whose HasNetworkPrefab is
    /// false, through LoadLootTable → GetRandomItem. A chosen slot is made to look like a fixed slot holding the
    /// chosen prefab: HasNetworkPrefab true, LoadNetworkPrefab returns it, no loot table, and the game's preload of
    /// its own reference is skipped. "Nothing" is a slot with neither (how CreateShopItems takes that on this
    /// build is not yet seen). Keyed by GetInstanceID; cleared when the session stops.
    /// </summary>
    internal static class ShopChoices
    {
        private static readonly System.Collections.Generic.Dictionary<int, Unity.Netcode.NetworkObject> _chosen =
            new System.Collections.Generic.Dictionary<int, Unity.Netcode.NetworkObject>();

        public static void Set(Il2CppGame.Spawning.ShopLocation slot, Unity.Netcode.NetworkObject prefab)
        {
            _chosen[slot.GetInstanceID()] = prefab;
            slot.m_loadedPrefab = prefab;   // what LoadNetworkPrefab would have cached
            // The HasNetworkPrefab prefix alone did not reach a slot without a reference (a pool slot, a new slot:
            // 2026-11-14, both stayed empty), so the game reads the field itself. Give such a slot a reference;
            // LoadNetworkPrefab answers for it and the preload is skipped, so it is never loaded.
            var r = slot.m_networkPrefabRef;
            if (r == null || string.IsNullOrEmpty(r.AssetGUID))
            {
                var standIn = StandInGuid();
                if (standIn != null) slot.m_networkPrefabRef = new UnityEngine.AddressableAssets.AssetReferenceGameObject(standIn);
            }
        }

        // A GUID of a real GameObject in the game's catalog (the persistent player prefab), so that even an
        // unexpected load of the stand-in would find an asset.
        private static string _standIn;
        private static string StandInGuid()
        {
            if (!string.IsNullOrEmpty(_standIn)) return _standIn;
            try { _standIn = Il2CppGame.Networking.NetworkHandler.Singleton?.m_persistentPlayer?.AssetGUID; }
            catch { _standIn = null; }
            if (string.IsNullOrEmpty(_standIn))
                MelonLoader.MelonLogger.Warning("[POGLib] [Shops] no stand-in reference for slots without one; their choice may not reach the game.");
            return _standIn;
        }

        public static bool TryGet(UnityEngine.Object spawner, out Unity.Netcode.NetworkObject prefab)
        {
            prefab = null;
            return spawner != null && _chosen.Count > 0 && _chosen.TryGetValue(spawner.GetInstanceID(), out prefab);
        }

        public static void Reset() => _chosen.Clear();
    }

    [HarmonyPatch(typeof(Il2CppGame.Spawning.SpawnerBase), nameof(Il2CppGame.Spawning.SpawnerBase.HasNetworkPrefab), MethodType.Getter)]
    internal static class Patch_SpawnerBase_HasNetworkPrefab
    {
        static bool Prefix(Il2CppGame.Spawning.SpawnerBase __instance, ref bool __result)
        {
            if (!ShopChoices.TryGet(__instance, out var prefab)) return true;
            __result = prefab != null;
            return false;
        }
    }

    [HarmonyPatch(typeof(Il2CppGame.Spawning.SpawnerBase), nameof(Il2CppGame.Spawning.SpawnerBase.LoadNetworkPrefab))]
    internal static class Patch_SpawnerBase_LoadNetworkPrefab
    {
        static bool Prefix(Il2CppGame.Spawning.SpawnerBase __instance, ref Unity.Netcode.NetworkObject __result)
        {
            if (!ShopChoices.TryGet(__instance, out var prefab)) return true;
            __result = prefab;
            return false;
        }
    }

    [HarmonyPatch(typeof(Il2CppGame.Spawning.SpawnerBase), nameof(Il2CppGame.Spawning.SpawnerBase.PreloadNetworkPrefab))]
    internal static class Patch_SpawnerBase_PreloadNetworkPrefab
    {
        // The game's own item is not needed in a chosen slot: do not load it.
        static bool Prefix(Il2CppGame.Spawning.SpawnerBase __instance) => !ShopChoices.TryGet(__instance, out _);
    }

    [HarmonyPatch(typeof(Il2CppGame.Spawning.ShopLocation), nameof(Il2CppGame.Spawning.ShopLocation.HasLootTable), MethodType.Getter)]
    internal static class Patch_ShopLocation_HasLootTable
    {
        static bool Prefix(Il2CppGame.Spawning.ShopLocation __instance, ref bool __result)
        {
            if (!ShopChoices.TryGet(__instance, out _)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Il2CppGame.Spawning.ShopLocation), nameof(Il2CppGame.Spawning.ShopLocation.LoadLootTable))]
    internal static class Patch_ShopLocation_LoadLootTable
    {
        static bool Prefix(Il2CppGame.Spawning.ShopLocation __instance, ref Il2CppInventory.InventoryLootTable __result)
        {
            if (!ShopChoices.TryGet(__instance, out _)) return true;
            __result = null;
            return false;
        }
    }
}
