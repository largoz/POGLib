using System.Collections;
using System.Collections.Generic;
using Il2CppGame.Spawning;
using MelonLoader;
using POGLib.Core;
using POGLib.Shops.Model;
using Unity.Netcode;
using UnityEngine;

namespace POGLib.Shops
{
    /// <summary>
    /// R1: a mod slot is a copy of the game's own slot prefab (<see cref="SlotTemplates"/>), so it reaches every
    /// player through the game's replication. What is not replicated is the slot's identity and its children: a
    /// client with this library asks the host about each slot copy it receives, and the host answers with the slot's
    /// id, its price tag's pose and whether the tag is hidden (<see cref="SlotInfo"/>). The question is asked once the
    /// client is connected, so a player who joins a running scene gets the same answers.
    /// </summary>
    internal static class SlotSync
    {
        public const string AskAction = "shops.slot?";
        public const string InfoAction = "shops.slot";
        private const float AskWithin = 60f;

        private static bool _registered;
        private static readonly List<ulong> _pending = new List<ulong>();
        private static bool _flushing;
        private static bool _badAnswerReported;

        public static void Register()
        {
            if (_registered || CoreServices.Bridge == null) return;
            CoreServices.Bridge.RegisterHandler(AskAction, OnAsk);
            CoreServices.Bridge.RegisterHandler(InfoAction, OnInfo);
            CoreServices.Bridge.RegisterHandler(SellerSync.InfoAction, SellerSync.OnInfo);
            _registered = true;
        }

        public static void Reset() { _pending.Clear(); }

        // ---- client ----

        /// <summary>A slot spawned on this client. A runtime copy of the game's slot prefab (not a slot the scene holds)
        /// may be a mod slot: ask the host what it is.</summary>
        public static void OnClientSlotSpawned(ShopLocation slot)
        {
            var nm = NetworkManager.Singleton;
            if (slot == null || nm == null || nm.IsServer) return;
            var no = slot.GetComponent<NetworkObject>();
            if (no == null) return;
            string name = slot.gameObject.name;
            if (name.StartsWith(ShopScene.ModSlotPrefix) || !name.EndsWith("(Clone)")) return;
            AskAbout(no.NetworkObjectId);
        }

        /// <summary>Ask the host what this object is, once this client is connected.</summary>
        internal static void AskAbout(ulong networkId)
        {
            _pending.Add(networkId);
            if (_flushing) return;
            _flushing = true;
            MelonCoroutines.Start(AskWhenConnected());
        }

        // Objects that come with the scene's synchronisation spawn before the client counts as connected, when a
        // named message cannot be sent yet: the questions wait for the connection.
        private static IEnumerator AskWhenConnected()
        {
            float deadline = Time.realtimeSinceStartup + AskWithin;
            while (Time.realtimeSinceStartup < deadline)
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || !nm.IsListening) break;
                if (nm.IsConnectedClient)
                {
                    foreach (var id in _pending) CoreServices.Bridge?.SendToServer(AskAction, SlotInfo.Ask(id));
                    _pending.Clear();
                    break;
                }
                yield return new WaitForSeconds(0.25f);
            }
            _pending.Clear();
            _flushing = false;
        }

        private static void OnInfo(ulong sender, string payload)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || sender != NetworkManager.ServerClientId) return;
            if (!SlotInfo.TryDecode(payload, out var info))
            {
                if (!_badAnswerReported) MelonLogger.Warning($"[POGLib] [Shops] The host described a slot in a form this version cannot read: '{payload}'.");
                _badAnswerReported = true;
                return;
            }
            var slot = SpawnedSlot(nm, info.NetworkId);
            if (slot == null) { MelonLogger.Msg($"[POGLib] [Shops] {info.SlotId}: the slot left before the host's answer came."); return; }
            slot.gameObject.name = ShopScene.ModSlotPrefix + info.SlotId;
            var tag = slot.m_priceTagText != null ? SlotTemplates.TopChildOf(slot.m_priceTagText.transform, slot.transform) : null;
            if (tag != null)
            {
                if (info.TagPos != null) tag.localPosition = new Vector3(info.TagPos[0], info.TagPos[1], info.TagPos[2]);
                if (info.TagRot != null) tag.localEulerAngles = new Vector3(info.TagRot[0], info.TagRot[1], info.TagRot[2]);
                if (info.TagHidden) tag.gameObject.SetActive(false);
            }
            MelonLogger.Msg($"[POGLib] [Shops] {info.SlotId}: shown as the host placed it (price tag {(tag == null ? "not found" : info.TagHidden ? "hidden" : "posed")}).");
        }

        // ---- host ----

        private static void OnAsk(ulong sender, string payload)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || !SlotInfo.TryReadAsk(payload, out var id)) return;
            var slot = SpawnedSlot(nm, id);
            if (slot == null)
            {
                var seller = SellerSync.Describe(nm, id);
                if (seller != null) CoreServices.Bridge?.SendToClient(sender, SellerSync.InfoAction, seller.Encode());
                return;
            }
            if (!slot.gameObject.name.StartsWith(ShopScene.ModSlotPrefix)) return; // the game's own slot copy
            CoreServices.Bridge?.SendToClient(sender, InfoAction, Describe(id, slot).Encode());
        }

        /// <summary>The slot as the host has it: id, price-tag pose, and whether the tag is hidden.</summary>
        internal static SlotInfo Describe(ulong id, ShopLocation slot)
        {
            float[] pos = null, rot = null;
            bool hidden = false;
            if (slot.m_priceTagText != null)
            {
                var tag = SlotTemplates.TopChildOf(slot.m_priceTagText.transform, slot.transform);
                var p = tag.localPosition; var r = tag.localEulerAngles;
                pos = new[] { p.x, p.y, p.z };
                rot = new[] { r.x, r.y, r.z };
                hidden = !tag.gameObject.activeSelf;
            }
            return new SlotInfo(id, ShopScene.SlotId(slot), pos, rot, hidden);
        }

        private static ShopLocation SpawnedSlot(NetworkManager nm, ulong id)
        {
            var spawned = nm.SpawnManager?.SpawnedObjects;
            if (spawned == null || !spawned.ContainsKey(id)) return null;
            var no = spawned[id];
            return no != null ? no.GetComponent<ShopLocation>() : null;
        }
    }
}
