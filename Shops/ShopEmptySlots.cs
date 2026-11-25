using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Il2CppGame.Spawning;
using MelonLoader;
using Unity.Netcode;
using UnityEngine;

namespace POGLib.Shops
{
    /// <summary>
    /// Slots the rules left empty, on the host: no price tag, for anyone. The game replicates an item and its price, not "no
    /// tag": up to 0.1.2 the host hid only its own, and every other player saw a tag of "0" (LAN, 2026-11-25). The host
    /// still hides its own at once; everyone else is told by the game's own call (what it uses when an item is sold), so it
    /// works for a player without the mod too. A player who is still loading can miss that, so it is said again when a
    /// player has loaded the scene or joined. Saying it twice changes nothing: the tag is gone already.
    /// </summary>
    internal static class ShopEmptySlots
    {
        private const float WaitSeconds = 1f;
        private static readonly Dictionary<int, ShopLocation> _empty = new Dictionary<int, ShopLocation>();   // by GetInstanceID, not the reusable pointer
        private static NetworkSceneManager _listeningTo;
        private static NetworkSceneManager.SceneEventDelegate _onSceneEvent;
        private static float _due;
        private static bool _waiting, _reported;

        public static void Mark(ShopLocation slot) => _empty[slot.GetInstanceID()] = slot;

        public static void HideTagIfEmpty(ShopLocation slot)
        {
            if (slot == null || !_empty.ContainsKey(slot.GetInstanceID())) return;
            // The whole tag (its board too), not only the text: a blank board looked like a broken tag.
            if (slot.m_priceTagText != null) SlotTemplates.TopChildOf(slot.m_priceTagText.transform, slot.transform).gameObject.SetActive(false);
            Listen();
            TellSoon();   // not now: the slot itself is still on its way to the players
        }

        public static void Reset()
        {
            _empty.Clear();
            try { if (_listeningTo != null && _onSceneEvent != null) _listeningTo.remove_OnSceneEvent(_onSceneEvent); } catch { }
            _listeningTo = null;
        }

        // Only a session that has an empty slot listens; its scene manager lives as long as the session.
        private static void Listen()
        {
            var scenes = NetworkManager.Singleton?.SceneManager;
            if (scenes == null || (_listeningTo != null && _listeningTo.Pointer == scenes.Pointer)) return;
            _listeningTo = scenes;
            if (_onSceneEvent == null) { Action<SceneEvent> h = OnSceneEvent; _onSceneEvent = h; }
            scenes.add_OnSceneEvent(_onSceneEvent);
        }

        private static void OnSceneEvent(SceneEvent e)
        {
            try
            {
                if (e == null || _empty.Count == 0 || e.ClientId == NetworkManager.ServerClientId) return;
                if (e.SceneEventType == SceneEventType.LoadComplete || e.SceneEventType == SceneEventType.SynchronizeComplete) TellSoon();
            }
            catch (Exception ex) { Report(ex); }
        }

        private static void TellSoon()
        {
            _due = Time.realtimeSinceStartup + WaitSeconds;
            if (_waiting) return;
            _waiting = true;
            MelonCoroutines.Start(TellWhenDue());
        }

        private static IEnumerator TellWhenDue()
        {
            while (Time.realtimeSinceStartup < _due) yield return new WaitForSecondsRealtime(0.2f);
            _waiting = false;
            foreach (var id in _empty.Where(x => x.Value == null).Select(x => x.Key).ToList()) _empty.Remove(id);   // gone with their scene
            if (!ShopRuntime.IsHost) yield break;
            foreach (var slot in _empty.Values)
                try { if (slot.IsSpawned) HideForEveryone(slot); }
                catch (Exception ex) { Report(ex); }
        }

        // On its own: a game update without this call then costs a price tag, not the method around it.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void HideForEveryone(ShopLocation slot) => slot.HidePriceTagRpc();

        private static void Report(Exception ex)
        {
            if (!_reported) MelonLogger.Warning($"[POGLib] [Shops] hiding the price tag of an empty place for the other players: {ex.GetType().Name}: {ex.Message}");
            _reported = true;
        }
    }
}
