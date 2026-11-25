using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppEntities;
using Il2CppGame.Gameplay;
using Il2CppGame.Spawning;
using Il2CppInventory;
using MelonLoader;
using POGLib.Shops.Model;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace POGLib.Shops
{
    /// <summary>
    /// Dungeon merchants of mods (R2). The game draws its own merchant preset for the entrance cave first
    /// (PropList_EntranceCave_MerchantOrAdventurer, left as it is, so the dungeon of a seed is the game's); then the host
    /// decides, once per stall, by whichever of its parts spawns first (a slot or the seller), whether a mod's merchant
    /// keeps it (<see cref="MerchantDraw"/>). Everything a merchant needs is checked before the draw, so a stall is wholly a
    /// mod's or wholly the game's. Clients run none of this: the look and the goods reach them through the game.
    /// </summary>
    internal static class MerchantStalls
    {
        public const string PresetPrefix = "EntranceCave_Shopkeeper_";
        /// <summary>The dungeon. The same presets stand elsewhere too (a Tinkerer's in DarkBurg, the town); those stay the game's.</summary>
        public const string DungeonScene = "GameScene";

        internal sealed class Stall
        {
            public string Name;
            public MerchantClass Class;
            public MerchantDef Merchant;
            public string Look;
            /// <summary>What is put out, place by place along the row; null when the goods are not the merchant's own list.</summary>
            public List<PoolEntry> Goods;
            /// <summary>The places (GetInstanceID) along the row, so what comes together stands side by side.</summary>
            public List<int> Row;
            public Transform Root;
            public bool Ours => Merchant != null;
            public string FullId => Ours ? Class.ModId + ":" + Merchant.Id : null;
        }

        private static readonly List<MerchantClass> _fromCode = new List<MerchantClass>();
        private static List<MerchantClass> _all = new List<MerchantClass>();
        private static readonly Dictionary<int, Stall> _stalls = new Dictionary<int, Stall>();     // by the preset's root (GetInstanceID)
        private static readonly Dictionary<ulong, Stall> _sellers = new Dictionary<ulong, Stall>();   // by network object id; null: no stall of ours to care about
        private static readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);

        public static IReadOnlyList<MerchantClass> Classes => _all;

        // ---- the classes ----

        public static bool Add(MerchantClass given)
        {
            var c = given?.Copy();
            var e = MerchantDraw.Validate(c);
            if (c?.ModId != null && _fromCode.Any(x => x.ModId == c.ModId)) e.Add("this mod has its class already (one mod = one class; put all merchants in it)");
            foreach (var m in e) MelonLogger.Error($"[POGLib] [Shops] merchants of '{c?.ModId}': {m}; they are not used.");
            if (e.Count > 0) return false;
            _fromCode.Add(c);
            Rebuild(ShopsPlugin.Instance?.Catalog);
            return true;
        }

        /// <summary>After the shop files were read again: their sellers are classes too (a mod's own class comes first).</summary>
        public static void Rebuild(ShopCatalog catalog)
        {
            var all = new List<MerchantClass>(_fromCode);
            foreach (var c in MerchantDraw.FromFiles(catalog, PresetPrefix))
            {
                var e = MerchantDraw.Validate(c);
                if (all.Any(x => x.ModId == c.ModId)) e.Add("the mod has a class of its own from code; the file's sellers are left out");
                foreach (var m in e) if (_reported.Add(c.ModId + m)) MelonLogger.Error($"[POGLib] [Shops] \"sellers\" of '{c.ModId}': {m}; they are not used.");
                if (e.Count == 0) all.Add(c);
            }
            _all = all;
            foreach (var c in all) MerchantSettings.Add(c);
            ShopsPage.Changed();
        }

        public static void Reset() { _stalls.Clear(); _sellers.Clear(); }

        /// <summary>The game may give a later object a network id of the last scene. The stalls keep: an object's id is never given twice.</summary>
        public static void OnSceneLoaded() => _sellers.Clear();

        // ---- the decision (host) ----

        /// <summary>The stall a part belongs to, decided now if it was not; null when the part is in no merchant preset of the dungeon.</summary>
        public static Stall Of(Transform part)
        {
            if (_all.Count == 0 || SceneManager.GetActiveScene().name != DungeonScene) return null;
            for (var t = part; t != null; t = t.parent)
            {
                if (!t.name.StartsWith(PresetPrefix, StringComparison.Ordinal)) continue;
                int key = t.gameObject.GetInstanceID();
                if (!_stalls.TryGetValue(key, out var s)) _stalls[key] = s = Decide(t);
                return s;
            }
            return null;
        }

        private static Stall Decide(Transform root)
        {
            var s = new Stall { Name = ShopScene.CleanName(root.name).Substring(PresetPrefix.Length), Root = root };
            var looks = new Dictionary<MerchantDef, string>();
            s.Row = RowOf(root);
            int places = s.Row.Count > 0 ? s.Row.Count : int.MaxValue;
            var o = MerchantDraw.Draw(_all, s.Name, MerchantSettings.Ceiling, MerchantSettings.ChanceOf, (c, m) => Ready(c, m, places, looks), ShopRuntime.NextRandom);
            if (o.Merchant != null)
            {
                s.Class = o.Class;
                s.Merchant = o.Merchant;
                looks.TryGetValue(o.Merchant, out s.Look);
                if (!o.Merchant.FromFile && o.Merchant.Goods.Count > 0)
                    s.Goods = MerchantDraw.GoodsOrder(Loaded(o.Merchant), places, ShopRuntime.NextRandom);
            }
            MelonLogger.Msg($"[POGLib] [Shops] merchant stall {s.Name}: {s.FullId ?? "the game's own"} " +
                            $"({string.Join(", ", o.Shares.Select(x => $"{x.c.ModId} {100 * x.share:0.#}%").Concat(new[] { $"the game's own {100 * o.GameShare:0.#}%" }))})");
            return s;
        }

        private static List<PoolEntry> Loaded(MerchantDef m) => m.Goods.Where(g => ShopScene.ResolveItemPrefab(g.Item) != null).ToList();

        // Along the row: every preset has its counter along its own x (read from the game's files, 26791993).
        private static List<int> RowOf(Transform root)
        {
            var slots = root.GetComponentsInChildren<ShopLocation>(true).ToList();
            if (slots.Any(x => x.gameObject.activeInHierarchy)) slots.RemoveAll(x => !x.gameObject.activeInHierarchy);   // none on: the preset is not shown yet
            return slots.OrderBy(x => root.InverseTransformPoint(x.transform.position).x).Select(x => x.GetInstanceID()).ToList();
        }

        /// <summary>What stands at this place, null for nothing.</summary>
        public static PoolEntry GoodAt(Stall s, ShopLocation slot)
        {
            int id = slot.GetInstanceID();
            if (!s.Row.Contains(id)) s.Row.Add(id);
            int i = s.Row.IndexOf(id);
            return i < s.Goods.Count ? s.Goods[i] : null;
        }

        // Everything the merchant needs is here now; a merchant that lacks something takes no part, and the log says what.
        private static bool Ready(MerchantClass c, MerchantDef m, int places, Dictionary<MerchantDef, string> looks)
        {
            string id = c.ModId + ":" + m.Id;
            if (!m.FromFile && m.Goods.Count > 0)
            {
                foreach (var g in m.Goods)
                {
                    var prefab = ShopScene.ResolveItemPrefab(g.Item);
                    if (prefab == null) ShopChecks.NotLoaded(g.Item, id);
                    else ShopChecks.CheckPrice(g.Item, prefab, id);
                }
                var loaded = Loaded(m);
                var none = new List<PoolEntry>();
                bool any = false;
                foreach (var g in loaded.Where(g => WeightedPick.Usable(g.Weight)))
                {
                    int needs = MerchantDraw.Together(g, loaded, none).Count;
                    if (needs <= places) any = true;
                    else Once($"{id}|{g.Item}|{places}", $"merchant {id}: {g.Item} and what comes with it need {needs} places, the stall has {places}; it is left out there.");
                }
                if (!any) { Once(id + "|goods", $"merchant {id}: none of their goods {(loaded.Count == 0 ? "is loaded" : "can be put out here")}; they do not come."); return false; }
            }
            if (m.Look == null) return true;
            string look = null;
            try { look = m.Look(); }
            catch (Exception ex) { Once(id + "|look", $"merchant {id}: Look threw {ex.GetType().Name}: {ex.Message}; they do not come."); return false; }
            if (string.IsNullOrEmpty(look)) { Once(id + "|nolook", $"merchant {id}: no look now; they do not come."); return false; }
            looks[m] = look;
            return true;
        }

        private static void Once(string key, string text) { if (_reported.Add(key)) MelonLogger.Warning("[POGLib] [Shops] " + text); }

        /// <summary>Id of a slot of a stall a mod's merchant keeps: "Mod:merchant#n", n in the seller's slot order.</summary>
        public static string SlotId(Stall s, ShopLocation slot)
        {
            var spawner = s.Root != null ? s.Root.GetComponentInChildren<NpcSpawner>(true) : null;
            int n = 0;
            if (spawner != null && spawner.m_itemsForSale != null)
                for (int i = 0; i < spawner.m_itemsForSale.Length; i++)
                    if (spawner.m_itemsForSale[i] != null && spawner.m_itemsForSale[i].Pointer == slot.Pointer) { n = i + 1; break; }
            if (n == 0 && s.Root != null) n = Array.IndexOf(s.Root.GetComponentsInChildren<ShopLocation>(true).Select(x => x.Pointer).ToArray(), slot.Pointer) + 1;
            return ShopCatalog.SellerSlotId(s.FullId, n);
        }

        // ---- the seller (host) ----

        /// <summary>The stall this NPC sells at, when a mod's merchant keeps it; null otherwise.</summary>
        public static Stall StallOf(NPCEntity npc)
        {
            if (npc == null || _all.Count == 0 || !ShopRuntime.IsHost) return null;
            var no = npc.GetComponent<NetworkObject>();
            if (no == null || !no.IsSpawned) return null;
            if (_sellers.TryGetValue(no.NetworkObjectId, out var known)) return known;
            var spawner = UnityEngine.Object.FindObjectsOfType<NpcSpawner>()
                .FirstOrDefault(x => x != null && x.m_networkInstance != null && x.m_networkInstance.Pointer == no.Pointer);
            var s = spawner != null ? Of(spawner.transform) : null;
            return _sellers[no.NetworkObjectId] = s != null && s.Ours ? s : null;
        }

        /// <summary>Before the game dresses a seller: a mod's merchant gets their own look, and nobody's faction.</summary>
        public static void BeforeSeller(NPCEntity npc)
        {
            var s = StallOf(npc);
            if (s == null) return;
            // The base price, and no reputation to lose by killing them: the stall's faction is not theirs.
            npc.m_decreaseFactionReputationOnDeath = Faction.None;
            if (s.Merchant.Name != null) npc.m_npcName = s.Merchant.Name;   // the other players ask for it (SellerSync)
            if (s.Look == null) return;
            if (npc.Appearance == null) { MelonLogger.Error($"[POGLib] [Shops] {s.FullId}: the seller of the {s.Name} stall cannot be dressed (no goblin of parts on this build); they keep their own look."); return; }
            npc.m_useCosmetics = true;
            npc.m_useRandomCosmetics = false;
            npc.m_predefinedCosmeticsJson = s.Look;
        }

        /// <summary>
        /// Before a seller's own armour, hat and hand items are spawned: over a look of a mod's they are not (the Tinkerer wears a barrel),
        /// and their hands get what the merchant holds, through the game's own equipment tables.
        /// </summary>
        public static void BeforeEquipment(EnemyInventory inventory)
        {
            var npc = inventory != null ? inventory.GetComponent<NPCEntity>() : null;
            var s = npc != null ? StallOf(npc) : null;
            if (s?.Look == null) return;
            inventory.m_torsoSlot = null; inventory.m_headSlot = null; inventory.m_backSlot = null;
            inventory.m_leftHandSlot = InHand(s, s.Merchant.LeftHand);
            inventory.m_rightHandSlot = InHand(s, s.Merchant.RightHand);
        }

        private static InventoryLootTable InHand(Stall s, string id)
        {
            if (id == null) return null;
            var prefab = ShopScene.ResolveItemPrefab(id);
            var item = prefab != null ? prefab.GetComponent<Il2Cpp.InventoryItem>() : null;
            if (item == null) { Once($"{s.FullId}|{id}", $"merchant {s.FullId}: {id} for their hand is not loaded; the hand stays empty."); return null; }
            var table = ScriptableObject.CreateInstance<InventoryLootTable>();
            table.Items = new Il2CppSystem.Collections.Generic.List<Il2Cpp.InventoryItem>();
            table.Items.Add(item);
            return table;
        }
    }
}
