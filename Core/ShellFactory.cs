using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using Unity.Netcode;
using POGLib.Items;

namespace POGLib.Core
{
    /// <summary>
    /// Template factory. Under IL2CPP an InventoryItem cannot be created from scratch — a
    /// vanilla shell prefab must be cloned. The clone gets a DETERMINISTIC GlobalObjectIdHash
    /// (otherwise NGO resolves the spawn to the shell — the old mod's core bug).
    /// </summary>
    internal class ShellFactory
    {
        private readonly ContentRegistry _registry;

        public ShellFactory(ContentRegistry registry) { _registry = registry; }

        public static string ShellPrefabName(ItemShellKind kind) => kind switch
        {
            ItemShellKind.Coin => GameNames.Shells.Coin,
            ItemShellKind.Gem => GameNames.Shells.GemEmerald,
            ItemShellKind.BigItem => GameNames.Shells.BigItem,
            _ => GameNames.Shells.Diamond,
        };

        /// <summary>
        /// Find a vanilla shell prefab by name. Prefers a "clean" asset instance (not a
        /// scene-spawned one) so we don't clone dirty live state.
        /// </summary>
        public static InventoryItem FindShell(string prefabName)
        {
            // The game's own prefab list first (2026-11-14 build: the only place shells come from).
            var listed = GamePrefabs.Find(prefabName);
            var fromList = listed != null ? listed.GetComponent<InventoryItem>() : null;
            if (fromList != null) return fromList;

            var all = Resources.FindObjectsOfTypeAll<InventoryItem>();
            InventoryItem live = null;
            foreach (var it in all)
            {
                if (it == null || it.gameObject == null) continue;
                if (it.gameObject.name != prefabName) continue;
                // An asset instance does not belong to a scene (scene.IsValid() == false).
                if (!it.gameObject.scene.IsValid()) return it;
                live = live ?? it;
            }
            return live; // fallback: whatever we found, if no clean asset exists
        }

        /// <summary>
        /// Build an item template: clone the shell, disable it, DontDestroyOnLoad, assign the
        /// deterministic hash. Visual/identity are applied separately by the domain module.
        /// Returns null if the shell is not in memory yet (retry on the next scene load).
        /// </summary>
        private static GameObject _templateHolder;

        /// <summary>Inactive, DontDestroyOnLoad parent of every item template (see BuildItemTemplate).</summary>
        internal static GameObject TemplateHolder
        {
            get
            {
                if (_templateHolder != null) return _templateHolder;
                _templateHolder = new GameObject("POGLib_ItemTemplates");
                _templateHolder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_templateHolder);
                _templateHolder.hideFlags = HideFlags.HideAndDontSave;
                return _templateHolder;
            }
        }

        public InventoryItem BuildItemTemplate(ItemShellKind shellKind, string modId, string contentId)
        {
            string shellName = ShellPrefabName(shellKind);
            var shell = FindShell(shellName);
            if (shell == null)
            {
                // Before the game loads its prefab list (main menu) no shell is in memory: expected, retried at
                // GamePrefabsLoaded. Once the list is loaded a missing shell is a real problem (renamed prefab).
                if (GamePrefabs.IsLoaded)
                    MelonLogger.Warning($"[POGLib] Shell '{shellName}' is not in the game's network prefab list; items on it cannot be built.");
                return null;
            }

            // The template stays ACTIVE under one INACTIVE holder. A template that is itself inactive makes
            // every copy the game instantiates from it (shop stock, loot) inactive too — invisible, found
            // 2026-11-12 when a shop slot stocked with a mod item showed nothing. Under the holder the
            // template never runs (no Awake: before, Awake ran on the clone before SetActive(false)), while
            // a copy made by Instantiate is a root object and active at once.
            var go = UnityEngine.Object.Instantiate(shell.gameObject, TemplateHolder.transform);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.name = $"ModItem_{modId}_{contentId}";

            var item = go.GetComponent<InventoryItem>();
            if (item == null)
            {
                MelonLogger.Error($"[POGLib] Shell {shellName} has no InventoryItem; aborting {contentId}.");
                UnityEngine.Object.Destroy(go);
                return null;
            }

            // Deterministic GlobalObjectIdHash (writable uint32) — BEFORE network registration.
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                MelonLogger.Error($"[POGLib] Shell {shellName} has no NetworkObject; aborting {contentId}.");
                UnityEngine.Object.Destroy(go);
                return null;
            }
            uint hash = ContentRegistry.ComputeHash(modId, contentId);
            netObj.GlobalObjectIdHash = hash;

            // Strip the dangerous self-spawn chain inherited from some shells.
            item.m_spawnOnDestroy = null;

            // The big-item shell is a bucket decoration: without this the mod item would decorate the bucket.
            // Same on every peer (each builds its own template), so the network behaviours still match.
            foreach (var upgrade in go.GetComponentsInChildren<Il2Cpp.BucketUpgrade>(true))
                if (upgrade != null) UnityEngine.Object.DestroyImmediate(upgrade);

            _registry.RegisterPrefab(netObj);
            MelonLogger.Msg($"[POGLib] Built template {go.name} (shell={shellName}, hash={hash:X8}).");
            return item;
        }
    }
}
