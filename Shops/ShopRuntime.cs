using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Il2CppGame.Gameplay;
using Il2CppGame.Spawning;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using POGLib.Shops.Model;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace POGLib.Shops
{
    /// <summary>What happened to one slot this session (read by the dev tools and the logs).</summary>
    public sealed class SlotRecord
    {
        public string SlotId { get; internal set; }
        public ShopLocation Slot { get; internal set; }
        public ResolvedSlot Resolved { get; internal set; }
        /// <summary>What the game itself put in the slot, captured before any rule changed it.</summary>
        public List<VanillaEntry> Vanilla { get; internal set; } = new List<VanillaEntry>();
        /// <summary>Item id the host chose, "nothing", or null when the game's own choice was left alone.</summary>
        public string Picked { get; internal set; }
        public List<string> Notes { get; } = new List<string>();
    }

    /// <summary>
    /// Host-side application of the shop rules in a loaded scene:
    ///  • a vanilla slot touched by any rule gets its item chosen by weight here and handed to the game as
    ///    that slot's fixed item (the path most vanilla slots already use), so the game spawns, prices and
    ///    sells it as usual;
    ///  • mod slots are spawned next to their anchor once the scene's own objects exist, bound to their
    ///    seller (faction, theft, the seller's death) and stocked the same way.
    /// Clients run none of this: they see the result through the game's own replication.
    /// </summary>
    public static class ShopRuntime
    {
        private static readonly Dictionary<string, SlotRecord> _records = new Dictionary<string, SlotRecord>(StringComparer.Ordinal);
        private static System.Random _rng = new System.Random();

        public static IReadOnlyDictionary<string, SlotRecord> Records => _records;

        /// <summary>Tests can fix the random source; null restores a fresh one.</summary>
        public static void SetRandomSeed(int? seed) => _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

        internal static void Reset()
        {
            _records.Clear();
            _memo.Clear();
            _placedAfter.Clear();
            ShopChoices.Reset();
            SellerTemplates.Reset();
        }

        private static ShopCatalog Catalog => ShopsPlugin.Instance?.Catalog;

        private static bool IsHost
        {
            get { var nm = NetworkManager.Singleton; return nm != null && nm.IsListening && nm.IsServer; }
        }

        // ---- Stocking (vanilla and mod slots) ----

        /// <summary>Called from the ShopLocation.OnNetworkSpawn prefix: choose this slot's item on the host.</summary>
        // One choice per slot object per session: OnNetworkSpawn can run more than once for the same
        // in-scene slot (seen 2026-11-12: twice per hub load), and the second call must neither re-roll
        // nor mistake the first choice for the game's own content.
        // Keyed by GetInstanceID (unique per object), not by the IL2CPP pointer, which a later object can get
        // once this one is freed after a scene change.
        private sealed class Memo { public List<VanillaEntry> Vanilla; public string Picked; public NetworkObject Prefab; }
        private static readonly Dictionary<int, Memo> _memo = new Dictionary<int, Memo>();

        internal static void OnSlotSpawning(ShopLocation slot)
        {
            var catalog = Catalog;
            if (catalog == null || slot == null || !IsHost) return;
            int key = slot.GetInstanceID();
            if (_memo.TryGetValue(key, out var seen))
            {
                if (seen.Prefab != null || seen.Picked == ShopIds.Nothing) ApplyChoice(slot, seen.Prefab);
                return;
            }
            string id = SellerTemplates.VariantSlotId(slot, catalog, _rng) ?? ShopScene.SlotId(slot);
            var vanilla = ShopScene.VanillaEntriesOf(slot);
            var memo = new Memo { Vanilla = vanilla };
            _memo[key] = memo;
            var resolved = catalog.Resolve(id, vanilla);
            var rec = new SlotRecord { SlotId = id, Slot = slot, Resolved = resolved, Vanilla = vanilla };
            _records[id] = rec;
            if (resolved.Untouched) return;

            // Entries whose item is not loaded (its mod is missing, or a typo) take no part in the draw.
            var prefabs = new Dictionary<string, NetworkObject>(StringComparer.Ordinal);
            foreach (var e in resolved.Entries)
            {
                if (!WeightedPick.Usable(e.Weight) || e.Item == ShopIds.Nothing || prefabs.ContainsKey(e.Item)) continue;
                var prefab = ShopScene.ResolveItemPrefab(e.Item);
                prefabs[e.Item] = prefab;
                string usedBy = $"{e.AddedBy} in {id}";
                if (prefab == null)
                {
                    rec.Notes.Add($"{e.Item} (from {e.AddedBy}) is not loaded; left out");
                    ShopChecks.NotLoaded(e.Item, usedBy);
                }
                else ShopChecks.CheckPrice(e.Item, prefab, usedBy);
            }
            Func<SlotEntry, bool> usable = e => e.Item == ShopIds.Nothing || (prefabs.TryGetValue(e.Item, out var p) && p != null);

            var chosen = resolved.Pick(_rng.NextDouble(), usable);
            if (chosen == null)
            {
                rec.Picked = memo.Picked = ShopIds.Nothing;
                rec.Notes.Add("no usable entry: the slot stays empty");
                MakeEmpty(slot, rec);
                return;
            }
            rec.Picked = memo.Picked = chosen.Item;
            var chosenPrefab = chosen.Item == ShopIds.Nothing ? null : prefabs[chosen.Item];
            memo.Prefab = chosenPrefab;
            if (chosenPrefab == null) { MakeEmpty(slot, rec); return; }
            ApplyChoice(slot, chosenPrefab);
            MelonLogger.Msg($"[POGLib] [Shops] {id}: {chosen.Item} ({100 * resolved.ChanceAmong(chosen, usable):0.#}% of {resolved.Entries.Count(e => usable(e) && WeightedPick.Usable(e.Weight))} candidate(s))");
        }

        private static void MakeEmpty(ShopLocation slot, SlotRecord rec)
        {
            // The price tag would still show the last price, so hide it.
            ApplyChoice(slot, null);
            ShopEmptySlots.Mark(slot);
            MelonLogger.Msg($"[POGLib] [Shops] {rec.SlotId}: stays empty");
        }

        /// <summary>
        /// Hand the host's choice to the game: <paramref name="prefab"/> to sell, or null for an empty slot. The game
        /// asks for it through LoadNetworkPrefab and friends when its CreateShopItems coroutine runs (ShopChoices).
        /// </summary>
        private static void ApplyChoice(ShopLocation slot, NetworkObject prefab) => ShopChoices.Set(slot, prefab);

        // ---- Mod slots ----

        internal static void OnSceneLoaded()
        {
            var catalog = Catalog;
            if (catalog == null) return;
            string scene = SceneManager.GetActiveScene().name;
            bool modSlots = catalog.ModSlots.Values.Any(s => s.Def.Scene == scene);
            bool vanillaRules = ShopChecks.VanillaTargetsIn(scene, catalog).Count > 0;
            if (!modSlots && !vanillaRules) return;
            MelonCoroutines.Start(WhenSlotsSpawned(scene, modSlots, vanillaRules));
        }

        private static IEnumerator WhenSlotsSpawned(string scene, bool modSlots, bool vanillaRules)
        {
            // Wait for the session and for the scene's own sellers/slots to be spawned by NGO.
            float deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (SceneManager.GetActiveScene().name != scene) yield break;
                if (IsHost && ShopScene.SlotsInLoadedScenes().Any(s => s.IsSpawned)) break;
                yield return new WaitForSeconds(0.5f);
            }
            if (!IsHost) yield break;
            yield return new WaitForSeconds(1f);
            if (modSlots) SpawnModSlots(scene);
            if (!vanillaRules) yield break;
            yield return new WaitForSeconds(ShopChecks.SceneCheckDelaySeconds - 1f);
            if (SceneManager.GetActiveScene().name == scene) ShopChecks.ReportUnmatchedSlots(scene, Catalog);
        }

        internal static void SpawnModSlots(string scene)
        {
            var catalog = Catalog;
            int ok = 0, failed = 0;
            foreach (var ms in catalog.ModSlots.Values.Where(s => s.Def.Scene == scene))
            {
                try
                {
                    if (SpawnOne(ms)) ok++; else failed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    MelonLogger.Error($"[POGLib] [Shops] slot {ms.FullId} failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
            MelonLogger.Msg($"[POGLib] [Shops] {scene}: placed {ok} mod slot(s){(failed > 0 ? $", {failed} failed" : "")}.");
        }

        private static bool SpawnOne(ShopCatalog.ModSlot ms)
        {
            var def = ms.Def;
            if (ShopScene.FindSlot(ms.FullId) != null) return true;   // already spawned this scene visit
            var template = SlotTemplates.Get(ms.FullId);
            if (template == null) { SlotTemplates.BuildPending(Catalog); template = SlotTemplates.Get(ms.FullId); }
            if (template == null) { MelonLogger.Warning($"[POGLib] [Shops] slot {ms.FullId}: no template (the game's slot prefab is not in memory)"); return false; }
            // The copy keeps the game's slot prefab's network hash: only a hash the game registered reaches clients.
            uint hash = template.GetComponent<NetworkObject>().GlobalObjectIdHash;
            var links = NetworkManager.Singleton?.NetworkConfig?.Prefabs?.NetworkPrefabOverrideLinks;
            if (links != null && !links.ContainsKey(hash)) { MelonLogger.Warning($"[POGLib] [Shops] slot {ms.FullId}: the game's slot prefab ({hash}) is not a registered network prefab on this build; not placed"); return false; }

            NpcSpawner seller = string.IsNullOrEmpty(def.Seller) ? null : ShopScene.FindSeller(def.Seller);
            if (!string.IsNullOrEmpty(def.Seller) && seller == null)
                MelonLogger.Warning($"[POGLib] [Shops] slot {ms.FullId}: seller '{def.Seller}' not found; placed without a seller");
            Transform anchor;
            Vector3 pos;
            if (!string.IsNullOrEmpty(def.After))
            {
                bool? side = SlotDef.SideOf(def.After);
                ShopLocation after;
                if (side != null)
                {
                    if (seller == null) { MelonLogger.Warning($"[POGLib] [Shops] slot {ms.FullId}: \"after\": \"{def.After}\" needs its seller, which is not here; not placed"); return false; }
                    after = EndOnSide(seller, side.Value, out string sideWhy);
                    if (after == null) { MelonLogger.Warning($"[POGLib] [Shops] slot {ms.FullId}: no {def.After} end at {ShopScene.SellerId(seller)}: {sideWhy}; not placed"); return false; }
                }
                else
                {
                    after = ShopScene.FindSlot(def.After);
                    if (after == null) { MelonLogger.Warning($"[POGLib] [Shops] slot {ms.FullId}: the slot it goes after, '{def.After}', is not in scene {def.Scene}"); return false; }
                    if (seller == null) seller = after.m_shopKeeper;
                }
                if (!PlaceAfter(after, seller, ms.FullId, ShopScene.SlotId(after), out pos, out string why))
                { MelonLogger.Warning($"[POGLib] [Shops] slot {ms.FullId}: cannot go after '{def.After}': {why}; not placed"); return false; }
                anchor = after.transform;
            }
            else
            {
                string anchorId = string.IsNullOrEmpty(def.Anchor) ? def.Seller : def.Anchor;
                anchor = ShopScene.FindAnchor(anchorId);
                if (anchor == null) { MelonLogger.Warning($"[POGLib] [Shops] slot {ms.FullId}: anchor '{anchorId}' is not in scene {def.Scene}"); return false; }
                pos = anchor.TransformPoint(new Vector3(def.Pos[0], def.Pos[1], def.Pos[2]));
            }

            var go = UnityEngine.Object.Instantiate(template.gameObject);
            go.name = ShopScene.ModSlotPrefix + ms.FullId;
            var rot = anchor.rotation * Quaternion.Euler(def.Rot != null ? new Vector3(def.Rot[0], def.Rot[1], def.Rot[2]) : Vector3.zero);
            go.transform.SetPositionAndRotation(pos, rot);
            var slot = go.GetComponent<ShopLocation>();

            Faction faction = Faction.None;
            if (seller != null)
            {
                slot.m_shopKeeper = seller;
                var sibling = seller.m_itemsForSale.FirstOrDefault(s => s != null && s.m_shopFaction != null);
                if (sibling != null) faction = sibling.m_shopFaction.Value;
                AppendToSeller(seller, slot);
            }
            else if (!string.IsNullOrEmpty(def.Faction) && !Enum.TryParse(def.Faction, out faction))
                MelonLogger.Warning($"[POGLib] [Shops] slot {ms.FullId}: unknown faction '{def.Faction}' (use {string.Join(", ", Enum.GetNames(typeof(Faction)))})");

            go.GetComponent<NetworkObject>().Spawn(true);   // OnNetworkSpawn → OnSlotSpawning makes the choice
            if (faction != Faction.None && slot.m_shopFaction != null) slot.m_shopFaction.Value = faction;
            MelonLogger.Msg($"[POGLib] [Shops] placed {ms.FullId} at {pos:F2} {(string.IsNullOrEmpty(def.After) ? "next to" : "in the row after")} {anchor.name}{(seller != null ? $", sold by {ShopScene.SellerId(seller)} ({faction})" : "")}");
            MelonCoroutines.Start(StockIfMissed(slot, ms.FullId));
            return true;
        }

        // Mod slot full id -> the slot id it was placed after, so a second slot after the same one goes a step further.
        private static readonly Dictionary<string, string> _placedAfter = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Where a slot "after" <paramref name="after"/> goes: one step past it along the row of its seller's own slots
        /// (<see cref="SlotRow"/>), a step further for each mod slot already standing after the same slot.
        /// </summary>
        /// <summary>The game's own slot at the left or right end of the seller's row (<see cref="SlotRow.TryEndOnSide"/>).</summary>
        private static ShopLocation EndOnSide(NpcSpawner seller, bool right, out string why)
        {
            var own = OwnSlots(seller);
            var sp = seller.transform.position;
            if (!SlotRow.TryEndOnSide(own.Select(s => { var p = s.transform.position; return new[] { p.x, p.y, p.z }; }).ToList(),
                                      new[] { sp.x, sp.y, sp.z }, right, out int i, out why)) return null;
            return own[i];
        }

        // The seller's own slots as the game placed them (the library's new slots left out).
        private static List<ShopLocation> OwnSlots(NpcSpawner seller) =>
            seller?.m_itemsForSale == null ? new List<ShopLocation>()
            : seller.m_itemsForSale.Where(s => s != null && !s.gameObject.name.StartsWith(ShopScene.ModSlotPrefix)).ToList();

        private static bool PlaceAfter(ShopLocation after, NpcSpawner seller, string fullId, string afterId, out Vector3 pos, out string why)
        {
            pos = default;
            if (seller?.m_itemsForSale == null) { why = "it has no seller, so there is no row to follow"; return false; }
            var row = new List<float[]>();
            int afterIndex = -1;
            foreach (var s in OwnSlots(seller))
            {
                if (s.Pointer == after.Pointer) afterIndex = row.Count;
                var p = s.transform.position;
                row.Add(new[] { p.x, p.y, p.z });
            }
            if (afterIndex < 0) { why = $"it is not one of the slots of {ShopScene.SellerId(seller)}"; return false; }
            int before = _placedAfter.Count(kv => kv.Value == afterId && kv.Key != fullId && ShopScene.FindSlot(kv.Key) != null);
            if (!SlotRow.TryPlaceAfter(row, afterIndex, before + 1, out var at, out why)) return false;
            _placedAfter[fullId] = afterId;
            pos = new Vector3(at[0], at[1], at[2]);
            return true;
        }

        /// <summary>
        /// The 2026-11-14 build stocks a scene's slots in one batch a few seconds after they spawn (the CreateShopItems
        /// coroutine is no longer started from OnNetworkSpawn; who starts it was not read). A slot placed after that
        /// batch stayed empty (seen 2026-11-14). So once the scene's own slots hold their items, start the coroutine
        /// for ours, unless the game's batch took it after all.
        /// </summary>
        private static IEnumerator StockIfMissed(ShopLocation slot, string id)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (slot == null || !IsHost) yield break;
                if (slot.CurrentShopItem != null) yield break;
                if (ShopScene.SlotsInLoadedScenes().Any(s => s != null && s.GetInstanceID() != slot.GetInstanceID() && s.IsSpawned && s.CurrentShopItem != null)) break;
                yield return new WaitForSeconds(0.5f);
            }
            yield return new WaitForSeconds(1f);
            if (slot == null || !IsHost || slot.CurrentShopItem != null) yield break;
            slot.StartCoroutine(slot.CreateShopItems());
            MelonLogger.Msg($"[POGLib] [Shops] {id}: stocked by the library (placed after the scene's own slots were stocked).");
        }

        private static void AppendToSeller(NpcSpawner seller, ShopLocation slot)
        {
            var old = seller.m_itemsForSale;
            var arr = new Il2CppReferenceArray<ShopLocation>(old.Length + 1);
            for (int i = 0; i < old.Length; i++) arr[i] = old[i];
            arr[old.Length] = slot;
            seller.m_itemsForSale = arr;
        }
    }
}
