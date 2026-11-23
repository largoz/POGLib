using System;
using System.Collections.Generic;
using MelonLoader;
using Unity.Netcode;
using UnityEngine;

namespace POGLib.Core
{
    /// <summary>
    /// Registers mod prefabs with NGO. Called BEFORE the session starts (ForceSamePrefabs=true
    /// forbids AddNetworkPrefab afterwards). Does not trust AddNetworkPrefab's void return:
    /// verifies registration via NetworkPrefabOverrideLinks and re-salts the hash on collision.
    /// Session-scoped: reset on Shutdown so it re-registers on the next session start.
    /// </summary>
    internal class NetworkRegistrar
    {
        private readonly ContentRegistry _registry;
        private bool _registered;

        public NetworkRegistrar(ContentRegistry registry) { _registry = registry; }

        /// <summary>Reset the flag (on session Shutdown).</summary>
        public void Reset()
        {
            if (!_registered) return;
            _registered = false;
            MelonLogger.Msg("[POGLib] Network registration reset (session stopped).");
        }

        /// <summary>Register all mod prefabs. Idempotent within one session.</summary>
        public void RegisterAll()
        {
            if (_registered) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.NetworkConfig == null)
            {
                MelonLogger.Warning("[POGLib] RegisterAll: NetworkManager/Config not ready.");
                return;
            }

            // Snapshot first: a hash collision re-keys the registry mid-loop, and mutating the
            // dictionary while enumerating its values throws "Collection was modified".
            var prefabs = new List<NetworkObject>(_registry.AllPrefabs);

            int ok = 0, fail = 0, already = 0;
            foreach (var prefab in prefabs)
            {
                if (prefab == null || prefab.gameObject == null) { fail++; continue; }
                switch (TryRegisterOne(nm, prefab))
                {
                    case RegisterResult.Registered: ok++; break;
                    case RegisterResult.AlreadyRegistered: already++; break;
                    default: fail++; break;
                }
            }

            _registered = true;
            MelonLogger.Msg($"[POGLib] Network prefabs registered: {ok} ok, {fail} failed" +
                            (already > 0 ? $", {already} already registered from an earlier session." : "."));
            AddToGameList(prefabs);
        }

        /// <summary>
        /// Puts the mod prefabs into the game's own network prefab list too (NetworkHandler's loaded
        /// <c>NetworkPrefabsList</c>), straight into its <c>List</c> so NGO is not asked to register them a second time.
        /// The game finds a saved item by its hash in that list, not in NGO's table: restoring the bucket and the hub's
        /// storage (<c>StorageVessel.LoadStorage</c>, through <c>NetworkHandler.NetworkPrefabsList</c>) skipped every mod
        /// item, so a mod item vanished from a save it was in (found 2026-11-23, test save-3: the saved torches and
        /// spear came back, the berry did not). Idempotent: an entry with the same hash is not added twice.
        /// </summary>
        private static void AddToGameList(List<NetworkObject> prefabs)
        {
            var list = GamePrefabs.Loaded?.List;
            if (list == null) { MelonLogger.Warning("[POGLib] The game's network prefab list is not loaded: mod items will not come back from saves this session."); return; }
            var present = new HashSet<uint>();
            for (int i = 0; i < list.Count; i++)
            {
                var no = list[i]?.Prefab != null ? list[i].Prefab.GetComponent<NetworkObject>() : null;
                if (no != null) present.Add(no.GlobalObjectIdHash);
            }
            int added = 0;
            foreach (var prefab in prefabs)
            {
                if (prefab == null || present.Contains(prefab.GlobalObjectIdHash)) continue;
                list.Add(new NetworkPrefab { Prefab = prefab.gameObject });
                present.Add(prefab.GlobalObjectIdHash);
                added++;
            }
            if (added > 0) MelonLogger.Msg($"[POGLib] Added {added} mod prefab(s) to the game's own prefab list (saves find items there).");
        }

        private enum RegisterResult { Registered, AlreadyRegistered, Failed }

        private RegisterResult TryRegisterOne(NetworkManager nm, NetworkObject prefab)
        {
            uint hash = prefab.GlobalObjectIdHash;

            // 0) Already ours from a previous session? NetworkManager.Singleton outlives a session, so
            //    its prefab table still holds our entries after Shutdown. Without this check we would
            //    mistake our OWN registration for a collision and re-salt the hash on every re-host —
            //    which silently changes the item's identity, breaking saves and multiplayer parity.
            if (IsRegisteredToUs(nm, hash, prefab)) return RegisterResult.AlreadyRegistered;

            // 1) Resolve a genuine collision BEFORE registering (a duplicate => LogWarning+ignore in NGO).
            if (IsHashTaken(nm, hash))
            {
                var def = _registry.GetDefinitionByHash(hash);
                uint resolved = hash;
                for (int salt = 1; salt <= 8 && IsHashTaken(nm, resolved); salt++)
                {
                    if (def != null) resolved = ContentRegistry.ComputeHash(def.ModId, def.ContentId, salt);
                    else resolved = unchecked(resolved * 2654435761u + 1u); // fallback mix
                }
                if (IsHashTaken(nm, resolved))
                {
                    MelonLogger.Error($"[POGLib] Could not find free hash for {prefab.name} (base {hash:X8}).");
                    return RegisterResult.Failed;
                }
                if (resolved != hash)
                {
                    MelonLogger.Warning($"[POGLib] Hash collision {hash:X8} -> re-salted {resolved:X8} for {prefab.name}.");
                    prefab.GlobalObjectIdHash = resolved;
                    _registry.Rekey(hash, resolved);
                    hash = resolved;
                }
            }

            // 2) Register through the official path.
            try { nm.AddNetworkPrefab(prefab.gameObject); }
            catch (Exception ex)
            {
                MelonLogger.Error($"[POGLib] AddNetworkPrefab threw for {prefab.name}: {ex.Message}");
                return RegisterResult.Failed;
            }

            // 3) Post-verify (do not trust void/log): is the hash actually in the table.
            if (!IsHashTaken(nm, hash))
            {
                MelonLogger.Error($"[POGLib] Post-verify FAILED: {prefab.name} ({hash:X8}) not registered.");
                return RegisterResult.Failed;
            }
            return RegisterResult.Registered;
        }

        /// <summary>
        /// Whether this exact prefab is already registered under this hash — i.e. it is OUR earlier
        /// registration, not somebody else's object colliding with us.
        /// </summary>
        private static bool IsRegisteredToUs(NetworkManager nm, uint hash, NetworkObject prefab)
        {
            try
            {
                var links = nm.NetworkConfig.Prefabs?.NetworkPrefabOverrideLinks;
                if (links == null || !links.ContainsKey(hash)) return false;
                var entry = links[hash];
                return entry != null && entry.Prefab != null && entry.Prefab == prefab.gameObject;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Whether a GlobalObjectIdHash is already present in NetworkPrefabOverrideLinks.</summary>
        private static bool IsHashTaken(NetworkManager nm, uint hash)
        {
            try
            {
                var links = nm.NetworkConfig.Prefabs?.NetworkPrefabOverrideLinks;
                if (links == null) return false;
                return links.ContainsKey(hash);
            }
            catch
            {
                return false; // if the dict access failed, don't block; rely on AddNetworkPrefab
            }
        }
    }
}
