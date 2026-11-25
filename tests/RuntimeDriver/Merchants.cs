// Dungeon merchants of mods (POGShops, MerchantStalls): classes the driver adds through the public call, and checks of what
// the GAME then shows at the entrance's stall (its own objects, not POGShops' log). driver.txt:
//   merchants=1             add the driver's classes (DrvSolo, DrvBroken) and try wrong ones
//   expectmerchant=<who>    POGBlackMarket | DrvSolo | game: who must keep the stall (the run's prefs decide: POG_PREFS)
//   configpage=1            at the main menu: POG Config's page of POGShops, its rows moved and put back
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Il2Cpp;
using Il2CppGame.Spawning;
using MelonLoader;
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
        static readonly HashSet<string> Human = new HashSet<string> { "Item_Crossbow", "Item_CrossbowBolt", "Item_Weapon_Dagger", "Item_Weapon_Ward", "Item_MagicTome_Book_Ghost", "Item_MagicTome_Book_Necromancy", "Item_MagicTome_Book_Greed", "Item_MagicTome_Book_Rat", "Item_MagicTome_Book_ChaosFire", "Item_MagicTome_Book_Void", "Item_MagicTome_Book_Berserking" };
        const uint Coat = 3194361209;

        static MerchantClass Class(string mod, params MerchantDef[] m) => new MerchantClass { ModId = mod, Chance = 0.1, Merchants = m.ToList() };
        static PoolEntry Item(string id) => new PoolEntry { Item = id, Weight = 1 };

        internal static void Register()
        {
            Checks.Check("a class is taken", ShopsApi.AddMerchants(Class("DrvSolo",
                new MerchantDef { Id = "solo", Stalls = All.ToList(), Goods = { Item("vanilla:Item_Key") } })), "DrvSolo");
            Checks.Check("a class whose merchants cannot come is taken", ShopsApi.AddMerchants(Class("DrvBroken",
                new MerchantDef { Id = "throws", Weight = 100, Stalls = All.ToList(), Look = () => throw new InvalidOperationException("on purpose") },
                new MerchantDef { Id = "nolook", Weight = 100, Stalls = All.ToList(), Look = () => null },
                new MerchantDef { Id = "nogoods", Weight = 100, Stalls = All.ToList(), Goods = { Item("vanilla:Item_NoSuchThing"), Item("NoSuchItem") } })), "DrvBroken");
            Checks.Check("a second class of one mod is refused", !ShopsApi.AddMerchants(Class("DrvSolo", new MerchantDef { Id = "x", Stalls = All.ToList() })), "DrvSolo again");
            var greedy = Class("DrvGreedy", new MerchantDef { Id = "x", Stalls = All.ToList() }); greedy.Chance = 0.5;
            Checks.Check("asking for half of the stalls is refused", !ShopsApi.AddMerchants(greedy), "Chance 0.5");
            Checks.Check("a stall the game has not is refused", !ShopsApi.AddMerchants(Class("DrvTypo", new MerchantDef { Id = "x", Stalls = { "Curioso" } })), "Curioso");
        }

        static string Held(ShopLocation s) => s.CurrentShopItem != null ? s.CurrentShopItem.name.Replace("(Clone)", "").Trim() : null;

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
            bool bare = inv != null && inv.m_torsoSlot == null && inv.m_headSlot == null && inv.m_backSlot == null && inv.m_leftHandSlot == null && inv.m_rightHandSlot == null;
            var presets = UnityEngine.Object.FindObjectsOfType<Transform>().Where(t => t.name.StartsWith("EntranceCave_Shopkeeper_")).Select(t => t.name).ToList();
            Checks.Say($"stall: {string.Join(", ", presets)}; merchantOf={of ?? "null"} tag='{npc.m_npcName}' chest={chest} bare={bare} faction={npc.m_decreaseFactionReputationOnDeath}; goods {string.Join(", ", slots.Select(s => $"{Held(s) ?? "-"} {s.CurrentPrice} {s.m_shopFaction?.Value}"))}");
            switch (who)
            {
                case "POGBlackMarket":
                    Checks.Check("POGShops says whose he is", of != null && of.StartsWith("POGBlackMarket:"), of ?? "null");
                    Checks.Check("the game dressed him in the coat", chest == Coat, chest.ToString());
                    Checks.Check("his name", npc.m_npcName == "Nobody", npc.m_npcName);
                    Checks.Check("his own equipment is off", bare, bare ? "no slots" : "slots left");
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
                default:
                    Checks.Check("the stall is the game's own", of == null && chest != Coat, $"{of ?? "null"}, chest {chest}");
                    Checks.Check("no slot of it was touched", ShopRuntime.Records.Keys.All(k => !k.Contains("#") || k.StartsWith("vanilla:")), string.Join(", ", ShopRuntime.Records.Keys.Where(k => k.Contains("#") && !k.StartsWith("vanilla:"))));
                    break;
            }
            // Every mod merchant slot POGShops recorded holds in the game what was chosen.
            foreach (var r in ShopRuntime.Records.Values.Where(r => r.Slot != null && r.Picked != null && slots.Any(s => s.Pointer == r.Slot.Pointer)))
                Checks.Check($"{r.SlotId} holds what was chosen", (Held(r.Slot) == null ? "nothing" : "vanilla:" + Held(r.Slot)) == r.Picked, $"chose {r.Picked}, holds {Held(r.Slot) ?? "nothing"}");
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

        static void Set(string label, string option)
        {
            var row = _rows.FirstOrDefault(r => Label(r) == label);
            int i = row == null ? -1 : Array.IndexOf((string[])F(row, "_options"), option);
            Checks.Check($"the page has '{label}' with '{option}'", i >= 0, row == null ? "no such row" : $"option {i}");
            if (i >= 0) ((Action<int>)F(row, "_set"))(i);
        }
    }
}
