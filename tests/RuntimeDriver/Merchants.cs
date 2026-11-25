// Dungeon merchants of mods (POGShops, MerchantStalls): classes the driver adds through the public call, and checks of what
// the GAME then shows at the entrance's stall (its own objects, not POGShops' log). driver.txt:
//   merchants=1             add the driver's classes (DrvSolo, DrvBroken) and try wrong ones
//   expectmerchant=<who>    POGBlackMarket | DrvSolo | DrvPair | game: who must keep the stall (the run's prefs decide: POG_PREFS)
//   trade=1                 then buy there as a player does, what runs out and what does not, kill the seller and take what is left
//   configpage=1            at the main menu: POG Config's page of POGShops, its rows moved and put back
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Il2Cpp;
using Il2CppGame.Spawning;
using MelonLoader;
using POGLib.Items;
using POGLib.Shops;
using POGLib.Shops.Model;
using UnityEngine;

namespace POGLib.RuntimeDriver
{
    internal static class Merchants
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        static readonly List<string> All = new List<string> { "Antique", "Curiosa", "Foodlover", "MysticTent", "Smith", "Tinkerer" };
        // Copied by hand from what the user asked for (human things), not read from the mod.
        static readonly HashSet<string> Human = new HashSet<string> { "Item_Crossbow", "Item_CrossbowBolt", "Item_Weapon_Dagger", "Item_Weapon_Ward", "Item_MagicTome_Book_Ghost", "Item_MagicTome_Book_Necromancy", "Item_MagicTome_Book_Greed", "Item_MagicTome_Book_Rat", "Item_MagicTome_Book_ChaosFire", "Item_MagicTome_Book_Void", "Item_MagicTome_Book_Berserking", "Item_MagicTome_Book_Engineering", "Item_MagicTome_Book_MushroomHead" };
        static uint Coat => Content.Cosmetics.IdOf("POGBlackMarket:Coat");

        static MerchantClass Class(string mod, params MerchantDef[] m) => new MerchantClass { ModId = mod, Chance = 0.1, Merchants = m.ToList() };
        static PoolEntry Item(string id) => new PoolEntry { Item = id, Weight = 1 };

        internal static void Register()
        {
            Checks.Check("a class is taken", ShopsApi.AddMerchants(Class("DrvSolo",
                new MerchantDef { Id = "solo", Stalls = All.ToList(), Goods = { Item("vanilla:Item_Key") } })), "DrvSolo");
            // A pair: the key never without the torch, which does not run out and never stands alone.
            Checks.Check("a class with companions, a name and a thing in hand is taken", ShopsApi.AddMerchants(Class("DrvPair",
                new MerchantDef
                {
                    Id = "pair", Stalls = All.ToList(), Name = "Pair | Seller", Look = () => Content.Cosmetics.LookJson(new POGLib.Cosmetics.GoblinLook { Skin = 3 }), LeftHand = "vanilla:Item_Weapon_Dagger", RightHand = "vanilla:Item_Torch_Unlit",
                    Goods =
                    {
                        new PoolEntry { Item = "vanilla:Item_Key", With = { "vanilla:Item_Torch_Unlit" } },
                        new PoolEntry { Item = "vanilla:Item_Torch_Unlit", Weight = 0, Endless = true },
                    },
                })), "DrvPair");
            Checks.Check("a class whose merchants cannot come is taken", ShopsApi.AddMerchants(Class("DrvBroken",
                new MerchantDef { Id = "throws", Weight = 100, Stalls = All.ToList(), Look = () => throw new InvalidOperationException("on purpose") },
                new MerchantDef { Id = "nolook", Weight = 100, Stalls = All.ToList(), Look = () => null },
                new MerchantDef { Id = "nogoods", Weight = 100, Stalls = All.ToList(), Goods = { Item("vanilla:Item_NoSuchThing"), Item("NoSuchItem") } })), "DrvBroken");
            Checks.Check("a second class of one mod is refused", !ShopsApi.AddMerchants(Class("DrvSolo", new MerchantDef { Id = "x", Stalls = All.ToList() })), "DrvSolo again");
            var greedy = Class("DrvGreedy", new MerchantDef { Id = "x", Stalls = All.ToList() }); greedy.Chance = 0.5;
            Checks.Check("asking for half of the stalls is refused", !ShopsApi.AddMerchants(greedy), "Chance 0.5");
            Checks.Check("a stall the game has not is refused", !ShopsApi.AddMerchants(Class("DrvTypo", new MerchantDef { Id = "x", Stalls = { "Curioso" } })), "Curioso");
        }

        static string Clean(string name) => name.Replace("(Clone)", "").Trim();
        static string Held(ShopLocation s) => s.CurrentShopItem != null ? Clean(s.CurrentShopItem.name) : null;

        internal static void CheckStall(string who)
        {
            var npc = Noir.Npc; var sp = Noir.Spawner;
            Checks.Check("there is a merchant at the entrance", npc != null && sp != null, npc != null ? npc.name : "none");
            if (npc == null || sp == null) return;
            var slots = sp.m_itemsForSale.Where(s => s != null && !s.name.StartsWith("POGShopSlot")).ToList();
            var held = slots.Select(Held).ToList();
            uint chest = npc.CosmeticsData.ChestId;
            string of = ShopsApi.MerchantOf(npc);
            var inv = npc.GetComponent<Il2CppInventory.EnemyInventory>();
            bool bare = inv != null && inv.m_torsoSlot == null && inv.m_headSlot == null && inv.m_backSlot == null;
            // The game's sellers keep the weapon of their left hand hidden until a fight.
            var armed = inv?.LeftHandItem ?? (inv?.HiddenItems != null && inv.HiddenItems.Count > 0 ? inv.HiddenItems[0] : null);
            string hand = armed != null ? Clean(armed.name) : null, right = inv?.RightHandItem != null ? Clean(inv.RightHandItem.name) : null;
            if (inv != null) Checks.Say($"hands: left {inv.LeftHandItem?.name ?? "-"} right {inv.RightHandItem?.name ?? "-"} back {inv.BackItem?.name ?? "-"} hidden {(inv.HiddenItems != null ? string.Join(",", inv.HiddenItems.ToArray().Select(i => i?.name)) : "-")}; left table {(inv.m_leftHandSlot != null ? string.Join(",", inv.m_leftHandSlot.Items.ToArray().Select(i => i?.name)) : "-")} right table {(inv.m_rightHandSlot != null ? "yes" : "-")}; under them {string.Join(",", npc.GetComponentsInChildren<InventoryItem>(true).Select(i => i.name + (i.IsInInventory ? "*" : "")))}");
            string over = npc.GetComponentsInChildren<Il2CppTMPro.TMP_Text>(true).Select(x => x.text).FirstOrDefault(x => x == npc.m_npcName);
            var presets = UnityEngine.Object.FindObjectsOfType<Transform>().Where(t => t.name.StartsWith("EntranceCave_Shopkeeper_")).Select(t => t.name).ToList();
            Checks.Say($"stall: {string.Join(", ", presets)}; merchantOf={of ?? "null"} tag='{npc.m_npcName}' over the head '{over ?? "-"}' chest={chest} bare={bare} weapon {hand ?? "-"} right hand {right ?? "-"} faction={npc.m_decreaseFactionReputationOnDeath}; goods {string.Join(", ", slots.Select(s => $"{Held(s) ?? "-"} {s.CurrentPrice} {s.m_shopFaction?.Value}"))}");
            switch (who)
            {
                case "POGBlackMarket":
                    Checks.Check("POGShops says whose he is", of != null && of.StartsWith("POGBlackMarket:"), of ?? "null");
                    Checks.Check("the game dressed him in the coat", Coat != 0 && chest == Coat, chest.ToString());
                    Checks.Check("his name", npc.m_npcName == "Nobody" && over == "Nobody", $"{npc.m_npcName}, over the head {over ?? "-"}");
                    Checks.Check("the stall seller's equipment is off", bare, bare ? "no slots" : "slots left");
                    // The right hand: one seller in five holds the first thing of their loot there (NPCEntity.CreateTransitions), a sausage say.
                    Checks.Check("his weapon is the dagger", hand == "Item_Weapon_Dagger", $"{hand ?? "nothing"}, right hand {right ?? "nothing"}");
                    Checks.Check("a crossbow stands with bolts beside it or not at all", !held.Contains("Item_Crossbow") || (held.Contains("Item_CrossbowBolt") && SideBySide(slots, "Item_Crossbow", "Item_CrossbowBolt")), string.Join(", ", held));
                    Checks.Check("only the bolts do not run out", slots.All(s => s.CurrentShopItem == null || s.CurrentShopItem.SpawnCopyInsteadOfPickup == (Held(s) == "Item_CrossbowBolt")), string.Join(", ", slots.Select(s => $"{Held(s)} {s.CurrentShopItem?.SpawnCopyInsteadOfPickup}")));
                    Checks.Check("nobody's faction", npc.m_decreaseFactionReputationOnDeath == Il2CppGame.Gameplay.Faction.None, npc.m_decreaseFactionReputationOnDeath.ToString());
                    Checks.Check("every slot holds a human thing", held.All(h => h != null && Human.Contains(h)), string.Join(", ", held.Select(h => h ?? "-")));
                    Checks.Check("no item twice", held.Distinct().Count() == held.Count, string.Join(", ", held.Select(h => h ?? "-")));
                    Checks.Check("every item has a price", slots.All(s => s.CurrentPrice > 0), string.Join(", ", slots.Select(s => s.CurrentPrice)));
                    break;
                case "DrvSolo":
                    Checks.Check("POGShops says whose he is", of == "DrvSolo:solo", of ?? "null");
                    Checks.Check("one key and nothing else", held.Count(h => h == "Item_Key") == 1 && held.Count(h => h != null) == 1, string.Join(", ", held.Select(h => h ?? "-")));
                    Checks.Check("an empty place shows no price tag", slots.Where(s => Held(s) == null).All(s => s.m_priceTagText == null || !s.m_priceTagText.gameObject.activeInHierarchy), $"{slots.Count(s => Held(s) == null)} empty");
                    Checks.Check("without a look the seller keeps theirs", chest != Coat && !bare, $"chest {chest}, bare {bare}");
                    break;
                case "DrvPair":
                    Checks.Check("POGShops says whose he is", of == "DrvPair:pair", of ?? "null");
                    Checks.Check("their name", npc.m_npcName == "Pair | Seller" && over == npc.m_npcName, $"{npc.m_npcName}, over the head {over ?? "-"}");
                    Checks.Check("their weapon is the dagger, a torch in the right hand", hand == "Item_Weapon_Dagger" && right == "Item_Torch_Unlit" && bare, $"{hand ?? "nothing"}, {right ?? "nothing"}, bare {bare}");
                    Checks.Check("a key with a torch beside it and nothing else", string.Join(",", held.Where(h => h != null).OrderBy(h => h)) == "Item_Key,Item_Torch_Unlit", string.Join(", ", held.Select(h => h ?? "-")));
                    Checks.Check("the torch beside the key", SideBySide(slots, "Item_Key", "Item_Torch_Unlit"), string.Join(", ", held.Select(h => h ?? "-")));
                    Checks.Check("only the torch does not run out", slots.All(s => s.CurrentShopItem == null || s.CurrentShopItem.SpawnCopyInsteadOfPickup == (Held(s) == "Item_Torch_Unlit")), string.Join(", ", slots.Select(s => $"{Held(s)} {s.CurrentShopItem?.SpawnCopyInsteadOfPickup}")));
                    Checks.Check("an empty place shows no price tag", slots.Where(s => Held(s) == null).All(s => s.m_priceTagText == null || !s.m_priceTagText.gameObject.activeInHierarchy), $"{slots.Count(s => Held(s) == null)} empty");
                    break;
                default:
                    Checks.Check("the stall is the game's own", of == null && chest != Coat, $"{of ?? "null"}, chest {chest}");
                    Checks.Check("no slot of it was touched", ShopRuntime.Records.Keys.All(k => !k.Contains("#") || k.StartsWith("vanilla:")), string.Join(", ", ShopRuntime.Records.Keys.Where(k => k.Contains("#") && !k.StartsWith("vanilla:"))));
                    break;
            }
            // Every mod merchant slot POGShops recorded holds in the game what was chosen.
            foreach (var r in ShopRuntime.Records.Values.Where(r => r.Slot != null && r.Picked != null && slots.Any(s => s.Pointer == r.Slot.Pointer)))
                Checks.Check($"{r.SlotId} holds what was chosen", (Held(r.Slot) == null ? "nothing" : "vanilla:" + Held(r.Slot)) == r.Picked, $"chose {r.Picked}, holds {Held(r.Slot) ?? "nothing"}");
        }

        // What comes together stands side by side: nothing between them along the counter (the preset's own x).
        static bool SideBySide(List<ShopLocation> slots, string a, string b)
        {
            var root = Noir.Spawner.transform; while (root != null && !root.name.StartsWith("EntranceCave_Shopkeeper_")) root = root.parent;
            var row = slots.OrderBy(s => root.InverseTransformPoint(s.transform.position).x).Select(Held).ToList();
            Checks.Say("row: " + string.Join(" | ", row.Select(h => h ?? "-")));
            return Math.Abs(row.IndexOf(a) - row.IndexOf(b)) == 1;
        }

        // ---- trade: as a player who presses E at a thing; what is checked is the game's own state ----

        static int _tr, _gold, _had, _price, _times; static float _until; static string _state;
        static ShopLocation _slot; static InventoryItem _shown;
        static Il2CppEntities.EntityPlayer Me() => UnityEngine.Object.FindObjectsOfType<Il2CppEntities.EntityPlayer>().FirstOrDefault(p => p != null && p.IsOwner);
        static int Gold => ResourceManager.Instance.PlayerGold;
        // Things of this name in somebody's bag (the host is alone here).
        static int InBags(string name) => UnityEngine.Object.FindObjectsOfType<InventoryItem>(true).Count(i => i != null && i.IsInInventory && Clean(i.name) == name && !IsNpcs(i));
        static bool IsNpcs(InventoryItem i) => Noir.Npc != null && Noir.Npc.GetComponent<Il2CppInventory.EnemyInventory>()?.LeftHandItem?.Pointer == i.Pointer;
        static List<ShopLocation> Places => Noir.Spawner.m_itemsForSale.Where(s => s != null).ToList();

        static void Take(ShopLocation s)
        {
            _slot = s; _shown = s.CurrentShopItem; _gold = Gold; _price = s.CurrentPrice; _had = InBags(Held(s));
            _shown.Interact(Me());
        }

        /// <summary>One step per call; the seconds to wait before the next, 0 when done.</summary>
        internal static float TradeStep()
        {
            switch (_tr)
            {
                case 0:
                {
                    ResourceManager.Instance.SetGold(500);
                    var me = Me();
                    me.transform.position = Noir.Npc.transform.position + Noir.Npc.transform.forward * 1.6f + Vector3.up * 0.2f;
                    _tr = 1; return 2f;
                }
                case 1:   // twice (the bag is small) the thing that does not run out
                {
                    var s = Places.FirstOrDefault(x => x.CurrentShopItem != null && x.CurrentShopItem.SpawnCopyInsteadOfPickup);
                    if (s == null) { Checks.Say("trade: nothing here that does not run out"); _tr = 3; return 0.5f; }
                    Take(s); _tr = 2; return 3f;
                }
                case 2:
                {
                    string n = _shown != null ? Clean(_shown.name) : "gone";
                    _times++;
                    Checks.Check($"bought {n} for {_price}, time {_times}", _price > 0 && Gold == _gold - _price && InBags(n) == _had + 1, $"gold {_gold} -> {Gold}, in bags {_had} -> {InBags(n)}");
                    Checks.Check($"{n} still stands there, for sale", _slot.CurrentShopItem != null && _shown != null && _slot.CurrentShopItem.Pointer == _shown.Pointer && _shown.IsShopItem && _slot.CurrentPrice == _price && _shown.SpawnCopyInsteadOfPickup,
                                 $"{Held(_slot) ?? "-"} {_slot.CurrentPrice} shop item {_shown?.IsShopItem}");
                    if (_times < 2) { Take(_slot); return 3f; }
                    _tr = 3; return 0.5f;
                }
                case 3:   // a thing that runs out, once
                {
                    var s = Places.Where(x => x.CurrentShopItem != null && !x.CurrentShopItem.SpawnCopyInsteadOfPickup && x.CurrentPrice > 0 && x.CurrentPrice <= Gold).OrderBy(x => x.CurrentPrice).FirstOrDefault();
                    if (s == null) { Checks.Say("trade: nothing here that runs out"); _tr = 5; return 0.5f; }
                    Take(s); _tr = 4; return 3f;
                }
                case 4:
                {
                    string n = _shown != null ? Clean(_shown.name) : "gone";
                    Checks.Check($"bought {n} for {_price}", Gold == _gold - _price && InBags(n) == _had + 1, $"gold {_gold} -> {Gold}, in bags {_had} -> {InBags(n)}");
                    Checks.Check($"{n} is sold: no longer for sale, no tag", _shown != null && !_shown.IsShopItem && (_slot.m_priceTagText == null || !_slot.m_priceTagText.gameObject.activeInHierarchy), $"shop item {_shown?.IsShopItem} price {_slot.CurrentPrice}");
                    _tr = 5; return 0.5f;
                }
                case 5:   // hit him once: the weapon comes out
                    Noir.Npc.TakeDamage(1f, false, (Il2CppEntities.Entity.DamageType)1, (Il2CppEntities.Entity.DamageEffectType)0, Noir.Npc.transform.position, false, new Unity.Netcode.NetworkObjectReference(Me().NetworkObject));
                    _tr = 9; _until = Time.unscaledTime + 12f; return 0.2f;
                case 9:   // some run away first, or lose sight of the player: whether the dagger comes out within 12 s
                {
                    var inv = Noir.Npc.GetComponent<Il2CppInventory.EnemyInventory>();
                    _shown = inv.LeftHandItem;
                    string state = null; try { state = Noir.Npc.StateMachine?.CurrentState?.GetIl2CppType().Name; } catch { }
                    if (state != _state) { _state = state; Checks.Say($"trade: the seller's state {state ?? "?"}, {Vector3.Distance(Noir.Npc.transform.position, Me().transform.position):0.0} m from me"); }
                    if (_shown == null && Time.unscaledTime < _until) return 0.2f;
                    Checks.Check("hit, the seller draws the dagger", _shown != null && Clean(_shown.name) == "Item_Weapon_Dagger" && _shown.gameObject.activeInHierarchy,
                                 $"left {_shown?.name ?? "-"}, hidden {inv.HiddenItems?.Count}, health {Noir.Npc.Health.Health:0.#}, renderers on {_shown?.GetComponentsInChildren<Renderer>().Count(r => r.enabled)}");
                    Checks.Say("shot drawn");
                    _tr = 10; return 1.5f;   // no longer: a seller kills in seconds
                }
                case 10:
                    Checks.Say($"trade: killing the seller, in the left hand {Noir.Npc.GetComponent<Il2CppInventory.EnemyInventory>().LeftHandItem?.name ?? "-"}, in the right {Noir.Npc.GetComponent<Il2CppInventory.EnemyInventory>().RightHandItem?.name ?? "-"}");
                    Noir.Npc.Kill();
                    _tr = 6; return 6f;
                case 6:
                {
                    Checks.Say($"trade: after the death the weapon {(_shown == null ? "is gone" : $"lies {Vector3.Distance(_shown.transform.position, Noir.Spawner.transform.position):0.0} m from the stall, held {_shown.IsInInventory}")}; " +
                               $"lying loose near: {string.Join(", ", UnityEngine.Object.FindObjectsOfType<InventoryItem>().Where(i => i != null && !i.IsInInventory && !i.IsShopItem && Vector3.Distance(i.transform.position, Noir.Spawner.transform.position) < 5f).Select(i => Clean(i.name)))}");
                    var left = Places.Where(x => x.CurrentShopItem != null && x.CurrentShopItem.IsShopItem).ToList();
                    Checks.Check("what is left is free to take, once", left.All(x => x.CurrentShopItem.IsStealable && !x.CurrentShopItem.SpawnCopyInsteadOfPickup), string.Join(", ", left.Select(x => $"{Held(x)} stealable {x.CurrentShopItem.IsStealable} copy {x.CurrentShopItem.SpawnCopyInsteadOfPickup}")));
                    if (left.Count == 0) { _tr = 8; return 0.5f; }
                    Take(left.FirstOrDefault(x => Held(x) == "Item_CrossbowBolt" || Held(x) == "Item_Torch_Unlit") ?? left[0]); _tr = 7; return 3f;
                }
                case 7:
                {
                    string n = _shown != null ? Clean(_shown.name) : "gone";
                    Checks.Check($"took {n} from the dead seller's stall", Gold == _gold && InBags(n) == _had + 1 && _shown != null && !_shown.IsShopItem, $"gold {_gold} -> {Gold}, in bags {_had} -> {InBags(n)}, shop item {_shown?.IsShopItem}");
                    _tr = 8; return 0.5f;
                }
                default: return 0;
            }
        }

        // ---- POG Config's page ----

        static Assembly Config => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "POGConfig");
        static int _page;
        static List<object> _rows;

        /// <summary>One step per call; the seconds to wait before the next, 0 when done.</summary>
        internal static float PageStep()
        {
            var asm = Config;
            switch (_page++)
            {
                case 0:
                {
                    if (asm == null) { Checks.Say("page: no POG Config here"); return 0; }
                    var recs = ((System.Collections.IEnumerable)asm.GetType("POGMods.Config.POGConfig").GetMethod("GetAllRecords", Any).Invoke(null, null)).Cast<object>().ToList();
                    int idx = recs.FindIndex(r => (string)r.GetType().GetField("Name", Any).GetValue(r) == "POG Shops");
                    Checks.Check("POG Config has the page", idx >= 0, $"{recs.Count} pages");
                    if (idx < 0) return 0;
                    _rows = ((System.Collections.IEnumerable)recs[idx].GetType().GetField("Entries", Any).GetValue(recs[idx])).Cast<object>().ToList();
                    foreach (var r in _rows) Checks.Say($"page: {r.GetType().Name} '{Label(r)}' {Shown(r)}");
                    var beh = asm.GetType("POGMods.Config.ConfigBehaviour");
                    object inst = beh.GetField("_instance", Any).GetValue(null);
                    beh.GetMethod("OpenPanel", Any).Invoke(inst, null);
                    beh.GetMethod("SelectMod", Any).Invoke(inst, new object[] { idx });
                    return 3f;
                }
                case 1: Checks.Say("shot config_default"); return 4f;
                case 2:
                    Set("All mods, at most", "50%"); Set("DrvSolo", "40%"); Set("DrvBroken", "off");
                    return 4f;
                case 3: Checks.Say("shot config_moved"); return 4f;
                case 4:
                {
                    // The file on disk, not the entry in memory.
                    var cfg = File.ReadAllText(Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "MelonPreferences.cfg"));
                    foreach (var want in new[] { "MerchantCeiling = 0.5", "DrvSolo = 0.4", "DrvBroken = 0" })
                        Checks.Check($"saved: {want}", cfg.Split('\n').Any(l => l.Trim().StartsWith(want)), string.Join(" | ", cfg.Split('\n').Where(l => l.Contains(want.Split(' ')[0])).Select(l => l.Trim())));
                    foreach (var r in _rows) r.GetType().GetMethod("RestoreDefault", Any).Invoke(r, null);
                    return 3f;
                }
                case 5:
                {
                    var cfg = File.ReadAllText(Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "MelonPreferences.cfg"));
                    foreach (var want in new[] { "MerchantCeiling = 0.75", "DrvSolo = -1", "DrvBroken = -1" })
                        Checks.Check($"restored: {want}", cfg.Split('\n').Any(l => l.Trim().StartsWith(want)), string.Join(" | ", cfg.Split('\n').Where(l => l.Contains(want.Split(' ')[0])).Select(l => l.Trim())));
                    Checks.Say("shot config_restored");
                    return 4f;
                }
                case 6:   // Black Market's own page, when it is here
                {
                    var recs = ((System.Collections.IEnumerable)asm.GetType("POGMods.Config.POGConfig").GetMethod("GetAllRecords", Any).Invoke(null, null)).Cast<object>().ToList();
                    int idx = recs.FindIndex(r => (string)r.GetType().GetField("Name", Any).GetValue(r) == "Black Market");
                    if (idx < 0) { _page = 99; return 0.5f; }
                    _rows = ((System.Collections.IEnumerable)recs[idx].GetType().GetField("Entries", Any).GetValue(recs[idx])).Cast<object>().ToList();
                    foreach (var r in _rows) Checks.Say($"page: {r.GetType().Name} '{Label(r)}' {(F(r, "_get") is Func<bool> g ? g().ToString() : Shown(r))}");
                    var beh = asm.GetType("POGMods.Config.ConfigBehaviour");
                    beh.GetMethod("SelectMod", Any).Invoke(beh.GetField("_instance", Any).GetValue(null), new object[] { idx });
                    return 3f;
                }
                case 7: Checks.Say("shot config_bm"); return 4f;
                case 8: Set("Coat and hat", "7"); Set("In a thief's mask", "never"); Flip("Smith", true); Flip("Curiosa", false); return 3f;
                case 9:
                {
                    Saved("WeightPlain = 7", "WeightMask = 0", "Stalls = \"Antique,MysticTent,Smith,Tinkerer\"");
                    foreach (var r in _rows) r.GetType().GetMethod("RestoreDefault", Any).Invoke(r, null);
                    return 3f;
                }
                case 10: Saved("WeightPlain = 5", "WeightMask = 3", "WeightBandana = 2", "Stalls = \"Antique,Curiosa,MysticTent,Tinkerer\""); return 0.5f;
                default:
                {
                    var beh = asm.GetType("POGMods.Config.ConfigBehaviour");
                    beh.GetMethod("DoClose", Any)?.Invoke(beh.GetField("_instance", Any).GetValue(null), null);
                    return 0;
                }
            }
        }

        static object F(object o, string name) { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, Any); if (f != null) return f.GetValue(o); } return null; }
        static string Label(object row) => (string)row.GetType().GetProperty("Label", Any).GetValue(row);
        static string Shown(object row)
        {
            if (!(F(row, "_options") is string[] o)) return "";
            int i = ((Func<int>)F(row, "_get"))();
            return $"shows '{o[i]}' of {string.Join(" / ", o)}";
        }

        // The file on disk, not the entry in memory.
        static void Saved(params string[] lines)
        {
            var cfg = File.ReadAllText(Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "MelonPreferences.cfg")).Split('\n').Select(l => l.Trim()).ToList();
            foreach (var want in lines) Checks.Check($"saved: {want}", cfg.Any(l => l.StartsWith(want)), string.Join(" | ", cfg.Where(l => l.StartsWith(want.Split(' ')[0] + " "))));
        }

        static void Flip(string label, bool on)
        {
            var row = _rows.FirstOrDefault(r => Label(r) == label);
            var set = row == null ? null : F(row, "_set") as Action<bool>;
            Checks.Check($"the page has the switch '{label}'", set != null, row?.GetType().Name ?? "no such row");
            set?.Invoke(on);
        }

        static void Set(string label, string option)
        {
            var row = _rows.FirstOrDefault(r => Label(r) == label);
            int i = row == null ? -1 : Array.IndexOf((string[])F(row, "_options"), option);
            Checks.Check($"the page has '{label}' with '{option}'", i >= 0, row == null ? "no such row" : $"option {i}");
            if (i >= 0) ((Action<int>)F(row, "_set"))(i);
        }
    }
}
