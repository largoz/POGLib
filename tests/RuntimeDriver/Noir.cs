// A dungeon merchant that is sometimes the noir goblin instead, tried on the host: after the game has drawn a merchant
// preset and instantiated its seller, before the look is set (NPCEntity.OnNetworkPostSpawn). driver.txt:
//   noir=1                 on; looks come from goblin1=, goblin2=, ... (Outfit.cs), hat=/coat= from Wearables.cs
//   noirweights=5,3,2      how often each look comes up (default: all the same)
//   noirchance=0.15        chance that a merchant is ours (default 1)
//   noirskin=2             always this look
//   merchant=1             look at the dungeon's merchant, whoever it is (another mod decides)
//   entrance=<preset>      (test only) the entrance draws this preset every time
//   presets=1              list the entrance list's presets: seller, slots, scenery
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppEntities;
using Il2CppGame.Spawning;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Unity.Netcode;
using UnityEngine;

namespace POGLib.RuntimeDriver
{
    internal static class Noir
    {
        internal const string List = "PropList_EntranceCave_MerchantOrAdventurer";
        internal static NPCEntity Npc;
        internal static NpcSpawner Spawner;
        internal static int Skin;
        private static IDictionary<string, string> _cfg;
        private static Action<string> _log;
        private static bool _wearables;
        private static readonly System.Random Rng = new System.Random();

        internal static void Init(IDictionary<string, string> cfg, Action<string> log) { _cfg = cfg; _log = log; }

        /// <summary>The seller of the entrance's merchant stall, whoever it is (merchant=1: another mod decides).</summary>
        internal static void FindMerchant()
        {
            foreach (var x in UnityEngine.Object.FindObjectsOfType<NpcSpawner>())
                for (var t = x.transform; t != null; t = t.parent)
                    if (t.name.StartsWith("EntranceCave_Shopkeeper", StringComparison.Ordinal) && x.m_networkInstance != null) { Spawner = x; Npc = x.m_networkInstance.GetComponent<NPCEntity>(); }
        }

        internal static void Enable(HarmonyLib.Harmony harmony, IDictionary<string, string> cfg, Action<string> log)
        {
            var m = AccessTools.Method(typeof(NPCEntity), nameof(NPCEntity.OnNetworkPostSpawn));
            harmony.Patch(m, prefix: new HarmonyMethod(typeof(Noir), nameof(BeforePostSpawn)));
            // A seller's own armour, hat and hand items (the Tinkerer wears a barrel over any coat) come from here.
            var inv = AccessTools.Method(typeof(Il2CppInventory.EnemyInventory), nameof(Il2CppInventory.EnemyInventory.OnNetworkPostSpawn));
            harmony.Patch(inv, prefix: new HarmonyMethod(typeof(Noir), nameof(BeforeInventoryPostSpawn)));
            log("noir: patched " + m.DeclaringType.Name + "." + m.Name + " and " + inv.DeclaringType.Name + "." + inv.Name);
        }

        // The game draws the entrance from this list after GameScene loads; only a test narrows it.
        internal static void OnScene()
        {
            if (!_cfg.TryGetValue("entrance", out var want)) return;
            var list = MerchantList();
            if (list == null) { _log("noir: no " + List + " yet"); return; }
            var preset = list.PropList.FirstOrDefault(g => g != null && g.name == want);
            if (preset == null) { _log($"noir: '{want}' is not in the list: {string.Join(", ", list.PropList.Where(g => g != null).Select(g => g.name))}"); return; }
            var arr = new Il2CppReferenceArray<GameObject>(1); arr[0] = preset;
            int before = list.PropList.Length;
            list.PropList = arr;
            _log($"noir: the entrance list ({before} entries) is only {want} for this test");
        }

        internal static PropListAsset MerchantList() => Resources.FindObjectsOfTypeAll<PropListAsset>().FirstOrDefault(l => l != null && l.name == List);

        static float F(string key, float fallback) => _cfg.TryGetValue(key, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;

        static void BeforeInventoryPostSpawn(Il2CppInventory.EnemyInventory __instance)
        {
            try
            {
                var npc = __instance != null ? __instance.GetComponent<NPCEntity>() : null;
                if (npc == null) return;
                BeforePostSpawn(npc);
                if (Npc == null || Npc.Pointer != npc.Pointer || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
                string Name(Il2CppInventory.InventoryLootTable t) => t != null ? t.name : "-";
                _log($"noir: own equipment taken off: torso {Name(__instance.m_torsoSlot)}, head {Name(__instance.m_headSlot)}, back {Name(__instance.m_backSlot)}, hands {Name(__instance.m_leftHandSlot)} / {Name(__instance.m_rightHandSlot)}");
                __instance.m_torsoSlot = null; __instance.m_headSlot = null; __instance.m_backSlot = null;
                __instance.m_leftHandSlot = null; __instance.m_rightHandSlot = null;
            }
            catch (Exception ex) { _log($"noir: inventory FAILED {ex.GetType().Name}: {ex.Message}"); }
        }

        private static readonly HashSet<IntPtr> Seen = new HashSet<IntPtr>();

        static void BeforePostSpawn(NPCEntity __instance)
        {
            try
            {
                var nm = NetworkManager.Singleton;
                if (__instance == null || nm == null || !Seen.Add(__instance.Pointer)) return;
                if (!nm.IsServer)
                {
                    // A client with the mod: our parts are known before the game dresses the NPC; the coat's id tells whose tag it is.
                    if (!_wearables && (_cfg.ContainsKey("hat") || _cfg.ContainsKey("coat"))) { var sv = Outfit.Service(); if (sv != null) { _wearables = true; Wearables.Register(sv, __instance.gameObject, _cfg, _log); } }
                    uint chest = __instance.CosmeticsData.ChestId;
                    if (chest == Wearables.Ids["#coat"]) { _log($"noir: client sees {__instance.name} in our coat (tag '{__instance.m_npcName}')"); Npc = __instance; if (_wearables) __instance.m_npcName = "Nobody"; }
                    return;
                }
                var no = __instance.GetComponent<NetworkObject>();
                var sp = UnityEngine.Object.FindObjectsOfType<NpcSpawner>().FirstOrDefault(s => s.m_networkInstance != null && no != null && s.m_networkInstance.Pointer == no.Pointer);
                string preset = null;
                for (var x = sp != null ? sp.transform : null; x != null; x = x.parent)
                    if (x.name.StartsWith("EntranceCave_Shopkeeper", StringComparison.Ordinal)) { preset = x.name.Replace("(Clone)", "").Trim(); break; }
                if (preset == null) return;
                _log($"noir: merchant {preset}: seller {__instance.name} tag='{__instance.m_npcName}' use={__instance.m_useCosmetics} random={__instance.m_useRandomCosmetics} appearance={(__instance.Appearance != null)} json={(string.IsNullOrEmpty(__instance.m_predefinedCosmeticsJson) ? "-" : "yes")} faction={__instance.m_decreaseFactionReputationOnDeath}");
                double roll = Rng.NextDouble(); float chance = F("noirchance", 1f);
                if (roll >= chance) { _log($"noir: stays the game's own (roll {roll:0.00}, chance {chance:0.00})"); return; }
                if (__instance.Appearance == null) { _log("noir: this seller is no goblin of parts: left alone"); return; }
                var s = Outfit.Service();
                if (!_wearables) { _wearables = true; Wearables.Register(s, __instance.gameObject, _cfg, _log); }
                var looks = Outfit.Looks(_cfg);
                var weights = _cfg.TryGetValue("noirweights", out var ws) ? ws.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToList() : looks.Select(_ => 1.0).ToList();
                int forced = (int)F("noirskin", 0);
                int i = forced > 0 ? forced - 1 : POGLib.Shops.Model.WeightedPick.Index(Enumerable.Range(0, looks.Count).ToList(), k => weights[k], Rng.NextDouble());
                string json = Outfit.Json(s, looks[i].Value, out _, out string tag, _log);
                __instance.m_useCosmetics = true;
                __instance.m_useRandomCosmetics = false;
                __instance.m_predefinedCosmeticsJson = json;
                if (tag != null) __instance.m_npcName = tag;
                Npc = __instance; Spawner = sp; Skin = i + 1;
                _log($"noir: {preset} is the noir goblin, look {i + 1} of {looks.Count} (weights {string.Join("/", weights)})");
            }
            catch (Exception ex) { _log($"noir: FAILED {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
        }

        /// <summary>What stands in each preset of the entrance list: its seller, its slots and its scenery.</summary>
        internal static List<GameObject> DumpPresets(GameObject[] presets)
        {
            foreach (var g in presets)
            {
                var sp = g.GetComponentInChildren<NpcSpawner>(true);
                var slots = g.GetComponentsInChildren<ShopLocation>(true);
                var meshes = g.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null).Select(m => m.sharedMesh.name).GroupBy(n => n).OrderByDescending(x => x.Count()).Select(x => x.Key + (x.Count() > 1 ? "x" + x.Count() : ""));
                _log($"preset {g.name}: spawner {(sp != null ? sp.name + " flag " + sp.m_npcFlag : "-")}; slots {slots.Length}: {string.Join(", ", slots.Select(x => x.name.Replace("Game.Spawning.ShopLocation ", "")))}");
                _log($"preset {g.name}: scenery {string.Join(", ", meshes)}");
                _log($"preset {g.name}: lights {g.GetComponentsInChildren<Light>(true).Length}, children {g.GetComponentsInChildren<Transform>(true).Length}");
            }
            return presets.ToList();
        }

        /// <summary>items=1: every item the game has as a network prefab, with what a shop would ask for it.</summary>
        internal static void DumpItems(Action<string> log)
        {
            var list = POGLib.Core.GamePrefabs.Loaded?.List;
            for (int i = 0; list != null && i < list.Count; i++)
            {
                var it = list[i]?.Prefab != null ? list[i].Prefab.GetComponent<InventoryItem>() : null;
                if (it == null) continue;
                string active = string.Join("+", it.GetComponents<Component>().Select(c => c.GetIl2CppType().Name).Where(n => n.StartsWith("ActiveItem") || n.StartsWith("PassiveItem") || n.StartsWith("Magic")));
                log($"item {it.name} cost={it.m_shopCost} gold={it.m_goldValue} type={it.m_itemType} big={it.m_isBigItem} {active}");
            }
        }

        internal static void DumpNpcPrefabs(Action<string> log)
        {
            var list = POGLib.Core.GamePrefabs.Loaded?.List;
            for (int i = 0; list != null && i < list.Count; i++)
            {
                var p = list[i]?.Prefab; var n = p != null ? p.GetComponent<NPCEntity>() : null;
                if (n == null) continue;
                bool body = p.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.name == "CH_Goblin_Default");
                var ec = p.GetComponentInChildren<Il2CppGame.Players.EntityCosmetics>(true);
                log($"npcprefab {p.name} tag='{n.m_npcName}' use={n.m_useCosmetics} random={n.m_useRandomCosmetics} cosmetics={(ec != null)} goblinbody={body} json={(string.IsNullOrEmpty(n.m_predefinedCosmeticsJson) ? "-" : "yes")} faction={n.m_decreaseFactionReputationOnDeath}");
            }
        }
    }
}
