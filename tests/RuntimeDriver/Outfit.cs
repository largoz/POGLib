// How the game dresses goblins (_research/COSMETICS_NATIVE.md), tried on the host.
//
// driver.txt:
//   outfits=1                 dump every part (id, name, how it attaches, lock), the palettes and every NPC's look
//   goblin1=<look>            a new NPC in that look, on the stage (Stage.cs); goblin2=, goblin3= ...
//   goblinprefab=<name>       the network prefab they are made of (default Enemy_NPC_ShopKeeper)
// A look is key:value pairs joined with ';':  chest, legs, feet, hair, horn = part name (as dumped);
//   acc = part names joined with ','; chestcolor, legscolor, haircolor, horncolor, skin, eyes = palette index;
//   name = the name tag. Parts not named stay empty.
// The look goes in the way the game's own sellers carry theirs: as JSON in m_predefinedCosmeticsJson, before Spawn.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Il2Cpp;
using Il2CppEntities;
using Il2CppGame.Spawning;
using Unity.Netcode;
using UnityEngine;

namespace POGLib.RuntimeDriver
{
    internal static class Outfit
    {
        internal sealed class Goblin { public string Key; public NPCEntity Npc; public int Expected; }

        internal static readonly List<Goblin> Made = new List<Goblin>();
        private static Action<string> _log;

        internal static CustomGoblinService Service() =>
            Resources.FindObjectsOfTypeAll<CustomGoblinService>().FirstOrDefault(s => s != null && s.m_chests != null && s.m_chests.Length > 0);

        private static IEnumerable<(string slot, GameObject part)> Parts(CustomGoblinService s)
        {
            foreach (var (slot, list) in new (string, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GameObject>)[]
                     { ("horn", s.m_horns), ("hair", s.m_hairs), ("chest", s.m_chests), ("legs", s.m_legs), ("feet", s.m_feet), ("acc", s.m_accessories) })
                if (list != null)
                    for (int i = 0; i < list.Length; i++)
                        if (list[i] != null) yield return (slot, list[i]);
        }

        internal static void Dump(Action<string> log)
        {
            var s = Service();
            if (s == null) { log("outfit: no CustomGoblinService"); return; }
            log($"outfit: service {s.name}; cache {(s.m_partCache != null ? s.m_partCache.Count : -1)}");
            foreach (var (slot, part) in Parts(s))
            {
                var c = part.GetComponent<CosmeticItem>();
                var smr = part.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                var mr = part.GetComponentsInChildren<MeshRenderer>(true).Length;
                log($"outfit part {slot} {part.name} id={(c != null ? c.m_id.ToString() : "-")} type={(c != null ? c.m_itemType.ToString() : "-")} lock={(c != null ? c.m_lockType.ToString() : "-")} ach={(c != null && c.m_lockType == CustomGoblinService.LockType.LockedAchievement ? ((int)c.m_lockedAchievement).ToString() : "-")} reward={(c != null ? c.m_lockedId.ToString() : "-")} thumb={(c != null && c.m_thumbnail != null ? c.m_thumbnail.name : "-")} skinned={smr} static={mr}");
            }
            void Palette(string name, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color> p)
            {
                if (p == null) { log($"outfit palette {name}: none"); return; }
                var sb = new StringBuilder();
                for (int i = 0; i < p.Length; i++) sb.Append(i).Append('=').Append(ColorUtility.ToHtmlStringRGB(p[i])).Append(' ');
                log($"outfit palette {name}: {sb}");
            }
            Palette("clothes", s.m_clothesColors); Palette("skin", s.m_skinColors); Palette("hair", s.m_hairColors);
            Palette("horn", s.m_hornColors); Palette("eye", s.m_eyeColors);
            foreach (var n in UnityEngine.Object.FindObjectsOfType<NPCEntity>(true))
                log($"outfit npc {n.name} tag='{n.m_npcName}' use={n.m_useCosmetics} random={n.m_useRandomCosmetics} appearance={(n.Appearance != null)} json={n.m_predefinedCosmeticsJson}");
        }

        internal static void Make(IDictionary<string, string> cfg, Action<string> log)
        {
            _log = log;
            var s = Service();
            var looks = Looks(cfg);
            if (looks.Count == 0 || cfg.ContainsKey("noir")) return;   // noir=1: the looks are for the dungeon's merchant
            if (s == null) { log("goblin: no CustomGoblinService"); return; }
            string prefabName = cfg.TryGetValue("goblinprefab", out var p) ? p : "Enemy_NPC_ShopKeeper";
            GameObject prefab = null;
            var list = POGLib.Core.GamePrefabs.Loaded?.List;
            for (int i = 0; list != null && i < list.Count; i++)
                if (list[i]?.Prefab != null && list[i].Prefab.name == prefabName) { prefab = list[i].Prefab; break; }
            if (prefab == null)
            {
                var like = new List<string>();
                for (int i = 0; list != null && i < list.Count; i++) if (list[i]?.Prefab != null && list[i].Prefab.name.Contains("NPC")) like.Add(list[i].Prefab.name);
                log($"goblin: network prefab '{prefabName}' not in the game's list; NPC prefabs: {string.Join(", ", like)}");
                return;
            }
            var seller = UnityEngine.Object.FindObjectsOfType<NpcSpawner>().FirstOrDefault(x => x.m_npcFlag != null && x.m_npcFlag.Contains("StarterShop"));
            bool stage = Stage.Ready(log);      // the plate; by the starter shop it is dark and crowded
            if (seller == null && !stage) { log("goblin: no starter shop seller to stand before"); return; }
            if (cfg.ContainsKey("hat") || cfg.ContainsKey("coat")) Wearables.Register(s, prefab, cfg, log);
            var fwd = stage ? Stage.CamDir : Vector3.ProjectOnPlane(seller.transform.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, fwd);
            for (int i = 0; i < looks.Count; i++)
            {
                try
                {
                    var at = (stage ? Stage.Spot : seller.transform.position + fwd * 3.2f) + right * ((i - (looks.Count - 1) / 2f) * 1.3f);
                    if (Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
                    string tag; int expected;
                    string json = CosmeticsRun.Enabled || RunShow.Wanted ? CosmeticsRun.Json(looks[i].Value, out expected, out tag) : Json(s, looks[i].Value, out expected, out tag, log);
                    var go = UnityEngine.Object.Instantiate(prefab, at, Quaternion.LookRotation(fwd));
                    var npc = go.GetComponent<NPCEntity>();
                    if (npc == null) { log($"goblin: {prefabName} has no NPCEntity"); UnityEngine.Object.Destroy(go); return; }
                    npc.m_useCosmetics = true;
                    npc.m_useRandomCosmetics = false;
                    npc.m_predefinedCosmeticsJson = json;
                    if (tag != null) npc.m_npcName = tag;
                    go.GetComponent<NetworkObject>().Spawn();
                    Made.Add(new Goblin { Key = looks[i].Key, Npc = npc, Expected = expected });
                    log($"goblin: {looks[i].Key} = {prefabName} spawned at {at:F2}; look {json}");
                }
                catch (Exception ex) { log($"goblin: {looks[i].Key} FAILED {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
            }
        }

        // What the NPC ended up wearing, read from the scene: every renderer under it, its material and colour.
        internal static void Report(Goblin g)
        {
            var n = g.Npc;
            if (n == null) { _log($"goblin: {g.Key} is gone"); return; }
            var app = n.Appearance;
            _log($"goblin: {g.Key} at {n.transform.position:F2} spawned={n.IsSpawned} appearance={(app != null)} tag='{n.m_npcName}' clothes asked {g.Expected}, worn {(app != null && app.m_currentPartsTP != null ? app.m_currentPartsTP.Count : -1)}");
            foreach (var r in n.GetComponentsInChildren<Renderer>(false))
                _log($"goblin:   {g.Key} draws {r.name} ({r.GetIl2CppType().Name}) mat {(r.sharedMaterial != null ? r.sharedMaterial.name : "-")}");
        }

        internal static List<KeyValuePair<string, string>> Looks(IDictionary<string, string> cfg) =>
            cfg.Where(kv => kv.Key.StartsWith("goblin") && kv.Key.Length == 7 && char.IsDigit(kv.Key[6])).OrderBy(kv => kv.Key).ToList();

        internal static string Json(CustomGoblinService s, string look, out int expected, out string tag, Action<string> log)
        {
            _log = log;
            var parts = Parts(s).ToList();
            uint Id(string slot, string name)
            {
                if (Wearables.Ids.TryGetValue(name, out var own)) return own;
                foreach (var (sl, part) in parts)
                    if (sl == slot && part.name == name) { var c = part.GetComponent<CosmeticItem>(); if (c != null) return c.m_id; }
                _log($"goblin: no {slot} part '{name}'");
                return 0;
            }
            var ids = new Dictionary<string, uint>();
            var nums = new Dictionary<string, int>();
            var acc = new List<uint>();
            tag = null;
            foreach (var pair in look.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                int i = pair.IndexOf(':'); if (i <= 0) continue;
                string k = pair.Substring(0, i).Trim(), v = pair.Substring(i + 1).Trim();
                if (k == "name") tag = v;
                else if (k == "acc") acc.AddRange(v.Split(',').Select(a => Id("acc", a.Trim())).Where(a => a != 0));
                else if (k == "chest" || k == "legs" || k == "feet" || k == "hair" || k == "horn") ids[k] = Id(k, v);
                else nums[k] = int.Parse(v, CultureInfo.InvariantCulture);
            }
            expected = ids.Values.Count(v => v != 0) + acc.Count;
            while (acc.Count < 6) acc.Add(0);
            uint P(string k) => ids.TryGetValue(k, out var v) ? v : 0;
            int C(string k) => nums.TryGetValue(k, out var v) ? v : 0;
            return $"{{\"HornId\":{P("horn")},\"HairId\":{P("hair")},\"ChestId\":{P("chest")},\"LegsId\":{P("legs")},\"FeetId\":{P("feet")}," +
                   $"\"HornColorIndex\":{C("horncolor")},\"HairColorIndex\":{C("haircolor")},\"ChestColorIndex\":{C("chestcolor")},\"LegsColorIndex\":{C("legscolor")}," +
                   $"\"EyeLeftTypeIndex\":{C("eyel")},\"EyeRightTypeIndex\":{C("eyer")},\"EyeColorIndex\":{C("eyes")},\"SkinColorIndex\":{C("skin")}," +
                   $"\"Accessories\":[{string.Join(",", acc)}]}}";
        }
    }
}
