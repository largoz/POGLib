using Il2CppEntities;
using MelonLoader;
using POGLib.Core;
using POGLib.Shops.Model;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace POGLib.Shops
{
    /// <summary>
    /// A merchant's name for the players who are not the host: the game sends a seller's look and goods, not the name. A client asks the host
    /// about each NPC of the dungeon (the question of <see cref="SlotSync"/>, which an older host takes for a slot of no interest), and the host
    /// answers for a mod's merchant that has a name. Asked after the connection, so a player who joins a running dungeon is told too.
    /// </summary>
    internal static class SellerSync
    {
        public const string InfoAction = "shops.seller";
        private static bool _badAnswerReported;

        /// <summary>An NPC spawned on this client.</summary>
        public static void OnClientNpcSpawned(NPCEntity npc)
        {
            var nm = NetworkManager.Singleton;
            if (npc == null || nm == null || nm.IsServer || SceneManager.GetActiveScene().name != MerchantStalls.DungeonScene) return;
            var no = npc.GetComponent<NetworkObject>();
            if (no != null) SlotSync.AskAbout(no.NetworkObjectId);
        }

        public static void OnInfo(ulong sender, string payload)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || sender != NetworkManager.ServerClientId) return;
            if (!SellerInfo.TryDecode(payload, out var info))
            {
                if (!_badAnswerReported) MelonLogger.Warning($"[POGLib] [Shops] The host described a merchant in a form this version cannot read: '{payload}'.");
                _badAnswerReported = true;
                return;
            }
            var npc = Npc(nm, info.NetworkId);
            if (npc == null) { MelonLogger.Msg($"[POGLib] [Shops] {info.FullId}: the merchant left before the host's answer came."); return; }
            npc.m_npcName = info.Name;
            npc.SetNameInternal();
            MelonLogger.Msg($"[POGLib] [Shops] {info.FullId}: named '{info.Name}' as on the host.");
        }

        /// <summary>On the host: the answer about a spawned object, null when it is no mod's merchant with a name.</summary>
        public static SellerInfo Describe(NetworkManager nm, ulong id)
        {
            var s = MerchantStalls.StallOf(Npc(nm, id));
            return s?.Merchant.Name == null ? null : new SellerInfo(id, s.FullId, s.Merchant.Name);
        }

        private static NPCEntity Npc(NetworkManager nm, ulong id)
        {
            var spawned = nm.SpawnManager?.SpawnedObjects;
            if (spawned == null || !spawned.ContainsKey(id)) return null;
            var no = spawned[id];
            return no != null ? no.GetComponent<NPCEntity>() : null;
        }
    }
}
