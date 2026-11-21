using System.Collections.Generic;
using System.Linq;
using Il2CppGame.Spawning;
using MelonLoader;
using POGLib.Core;
using POGLib.Shops.Model;
using Unity.Netcode;
using UnityEngine;

namespace POGLib.Shops
{
    /// <summary>
    /// The host's template per mod slot (R1, 2026-11-19): a copy of the game's own standalone slot prefab
    /// (<c>ShopLocation</c>, which the game registers as a network prefab on every peer, G2), with its price tag
    /// posed as the shop file says and the donor's item references cleared. It keeps the donor's network hash, so
    /// a copy the host spawns arrives on every client as the game's own slot, a client without the shop file or
    /// without this library included; nothing new is registered. Children are not replicated, so a client with
    /// the library learns the slot's id and its price-tag pose from the host (<see cref="SlotSync"/>).
    ///
    /// Templates sit ACTIVE under one INACTIVE holder: they never run themselves, but a copy made by Instantiate
    /// is a root object and is active at once. They can be built at any time, a running session included.
    /// </summary>
    internal static class SlotTemplates
    {
        /// <summary>
        /// The game's slot prefab without a table (item holder at the slot's origin): the look of a slot that stands in
        /// a row of goods on the floor or a mat, the default for a slot placed with "after" (2026-11-21: the standard
        /// prefab, 'ShopLocation', holds its item 0.75 m up on a small table; the starter shop's goods lie 0.11 m up).
        /// </summary>
        internal const string FloorTemplate = "ShopLocationSlim";

        private static GameObject _holder;
        private static readonly Dictionary<string, ShopLocation> _built = new Dictionary<string, ShopLocation>();

        public static ShopLocation Get(string fullSlotId) => _built.TryGetValue(fullSlotId, out var t) ? t : null;

        /// <summary>The vanilla slot new slots are copied from (null before it is loaded).</summary>
        internal static ShopLocation Donor => FindDonor(null);
        public static int Count => _built.Count;

        public static void BuildPending(ShopCatalog catalog)
        {
            if (catalog == null) return;
            var pending = catalog.ModSlots.Values.Where(s => !_built.ContainsKey(s.FullId)).ToList();
            if (pending.Count == 0) return;

            var donor = FindDonor(null);
            if (donor == null)
            {
                MelonLogger.Msg("[POGLib] [Shops] No vanilla shop slot in memory yet to copy for new slots (will retry on the next scene).");
                return;
            }
            EnsureHolder();
            foreach (var s in pending)
            {
                string look = !string.IsNullOrEmpty(s.Def.Template) ? s.Def.Template : !string.IsNullOrEmpty(s.Def.After) ? FloorTemplate : null;
                var src = FindDonor(look) ?? donor;
                var t = Build(src, s);
                if (t != null) _built[s.FullId] = t;
            }
            MelonLogger.Msg($"[POGLib] [Shops] Slot templates ready: {_built.Count} (copied from the game's '{donor.gameObject.name}', network hash {donor.GetComponent<NetworkObject>().GlobalObjectIdHash}; nothing new to register).");
        }

        /// <summary>A ShopLocation from a loaded prefab asset (not a scene instance), preferring a named one.</summary>
        private static ShopLocation FindDonor(string preferredName)
        {
            var all = Resources.FindObjectsOfTypeAll<ShopLocation>()
                .Where(s => s != null && !s.gameObject.scene.IsValid() && s.GetComponent<NetworkObject>() != null && s.m_priceTagText != null && s.m_itemHolder != null)
                .ToList();
            if (!string.IsNullOrEmpty(preferredName))
            {
                string local = ShopIds.TrySplit(preferredName, out _, out var l) ? l : preferredName;
                var named = all.FirstOrDefault(s => s.gameObject.name == local || s.gameObject.name.EndsWith(local));
                if (named != null) return named;
            }
            return all.FirstOrDefault();
        }

        /// <summary>Inactive parent of every shop template (slots and merchants).</summary>
        internal static GameObject Holder { get { EnsureHolder(); return _holder; } }

        private static void EnsureHolder()
        {
            if (_holder != null) return;
            _holder = new GameObject("POGLib_ShopTemplates");
            _holder.SetActive(false);
            Object.DontDestroyOnLoad(_holder);
            _holder.hideFlags = HideFlags.HideAndDontSave;
        }

        private static ShopLocation Build(ShopLocation donor, ShopCatalog.ModSlot s)
        {
            var go = Object.Instantiate(donor.gameObject, _holder.transform);
            go.name = ShopScene.ModSlotPrefix + s.FullId;
            go.SetActive(true);   // active itself, inactive through the holder
            var slot = go.GetComponent<ShopLocation>();
            slot.m_networkPrefabRef = null;
            slot.m_lootTableRef = null;
            slot.m_shopKeeper = null;

            var tag = s.Def.PriceTag;
            if (tag != null && slot.m_priceTagText != null)
            {
                var tagRoot = TopChildOf(slot.m_priceTagText.transform, go.transform);
                if (tag.Pos != null) tagRoot.localPosition = new Vector3(tag.Pos[0], tag.Pos[1], tag.Pos[2]);
                if (tag.Rot != null) tagRoot.localEulerAngles = new Vector3(tag.Rot[0], tag.Rot[1], tag.Rot[2]);
            }
            return slot;
        }

        /// <summary>The direct child of <paramref name="root"/> that contains <paramref name="t"/>.</summary>
        internal static Transform TopChildOf(Transform t, Transform root)
        {
            var x = t;
            while (x.parent != null && x.parent != root) x = x.parent;
            return x.parent == root ? x : t;
        }
    }
}
