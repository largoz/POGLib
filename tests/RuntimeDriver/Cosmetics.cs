// Content.Cosmetics in the real game (driver.txt: cosmetics=1, cosmdir=<folder of make_parts.py>): one part per slot, worn in the
// game's own wardrobe, by an NPC and by the player. Asserting (PASS / FAIL), plus shots to look at.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using Il2CppEntities;
using Il2CppGame.Menu;
using Il2CppGame.Players;
using POGLib.Cosmetics;
using POGLib.Items;
using UnityEngine;

namespace POGLib.RuntimeDriver
{
    internal static class CosmeticsRun
    {
        const string Mod = "CosmTest";
        internal static bool Enabled, MenuDone, HubDone;
        static string _dir;
        static Action<string> _log;
        static int _m, _h, _cat;
        static uint _hairBefore;
        static NetworkMenuCharacterAppearance _ui;
        static readonly Dictionary<string, int> _arrays = new Dictionary<string, int>();

        static readonly (string id, CosmeticSlot slot, string file, bool locked)[] Parts =
        {
            ("Horns", CosmeticSlot.Horns, "horns", false), ("Cap", CosmeticSlot.Hair, "cap", false), ("Hood", CosmeticSlot.Hair, "hood", true),
            ("Mane", CosmeticSlot.Hair, "mane", false), ("Vest", CosmeticSlot.Chest, "vest", false), ("Trousers", CosmeticSlot.Legs, "trousers", false),
            ("Boots", CosmeticSlot.Feet, "boots", false), ("Belt", CosmeticSlot.Accessory, "belt", false), ("Board", CosmeticSlot.Accessory, "board", false),
        };

        static readonly (CosmeticSlot slot, CustomGoblinService.Category cat)[] Cats =
        {
            (CosmeticSlot.Hair, CustomGoblinService.Category.Hair), (CosmeticSlot.Horns, CustomGoblinService.Category.Horns), (CosmeticSlot.Chest, CustomGoblinService.Category.Chest),
            (CosmeticSlot.Legs, CustomGoblinService.Category.Legs), (CosmeticSlot.Feet, CustomGoblinService.Category.Feet), (CosmeticSlot.Accessory, CustomGoblinService.Category.Accessories),
        };

        static string Full(string id) => Mod + ":" + id;
        static uint Id(string id) => Content.Cosmetics.IdOf(Full(id));
        static void Check(string name, bool ok, string detail) => Checks.Check("cosm " + name, ok, detail);

        internal static void Init(IDictionary<string, string> cfg, Action<string> log)
        {
            _log = log; _dir = cfg["cosmdir"]; Enabled = true;
            bool Reg(string id, CosmeticSlot slot, string model, string thumb, bool locked = false) =>
                Content.Cosmetics.Register(new CosmeticDefinition { ModId = Mod, ContentId = id, Slot = slot, ModelFile = Path.Combine(_dir, model), ThumbnailFile = Path.Combine(_dir, thumb), Locked = locked });
            foreach (var (id, slot, file, locked) in Parts) Check($"registered {id}", Reg(id, slot, file + ".glb", file + ".png", locked), slot.ToString());
            // Each of these must be refused.
            Check("refuses a second one of the same id", !Reg("Cap", CosmeticSlot.Hair, "cap.glb", "cap.png"), "Cap again");
            Check("refuses a chest without weights", !Reg("BadChest", CosmeticSlot.Chest, "cap.glb", "cap.png"), "cap.glb as Chest");
            Check("refuses a bone the game does not find", !Reg("BadBone", CosmeticSlot.Accessory, "mouth.glb", "mouth.png"), "mouth.glb");
            Check("refuses a model that is not there", !Reg("NoModel", CosmeticSlot.Hair, "nothing.glb", "cap.png"), "nothing.glb");
            Check("refuses a thumbnail that is not there", !Reg("NoThumb", CosmeticSlot.Hair, "cap.glb", "nothing.png"), "nothing.png");
            Check("refuses a thumbnail that is not a PNG", !Reg("NotPng", CosmeticSlot.Hair, "cap.glb", "cap.glb"), "cap.glb as a thumbnail");
            Check("refuses to hide an object that is not there", !Content.Cosmetics.Register(new CosmeticDefinition { ModId = Mod, ContentId = "NoObject", Slot = CosmeticSlot.Chest, ModelFile = Path.Combine(_dir, "vest.glb"), ThumbnailFile = Path.Combine(_dir, "vest.png"), HiddenInFirstPerson = { "collar" } }), "vest.glb has no 'collar'");
            Check("refuses a PNG as a model", !Reg("NotGlb", CosmeticSlot.Hair, "cap.png", "cap.png"), "cap.png");
            Check("refuses an id with a colon", !Reg("A:B", CosmeticSlot.Hair, "cap.glb", "cap.png"), "A:B");
            Check("number of a part that is not there", Id("BadChest") == 0 && Content.Cosmetics.IdOf("nobody:nothing") == 0, "0");
            Check("numbers differ", Parts.Select(p => Id(p.id)).Distinct().Count() == Parts.Length, string.Join(" ", Parts.Select(p => p.id + "=" + Id(p.id))));
            Check("locked", !Content.Cosmetics.IsUnlocked(Full("Hood")) && Content.Cosmetics.IsUnlocked(Full("Cap")), "Hood locked, Cap not");
            // A secret: locked and out of the wardrobe until it is unlocked.
            Check("registered Secret", Content.Cosmetics.Register(new CosmeticDefinition { ModId = Mod, ContentId = "Secret", Slot = CosmeticSlot.Hair, ModelFile = Path.Combine(_dir, "cap.glb"),
                ThumbnailFile = Path.Combine(_dir, "cap.png"), Locked = true, HiddenWhileLocked = true }), "Hair");
        }

        static void Arrays(CustomGoblinService s, string when)
        {
            var now = new Dictionary<string, int>
            {
                ["horns"] = s.m_horns.Length, ["hairs"] = s.m_hairs.Length, ["chests"] = s.m_chests.Length, ["legs"] = s.m_legs.Length, ["feet"] = s.m_feet.Length, ["accessories"] = s.m_accessories.Length,
            };
            if (_arrays.Count == 0) foreach (var kv in now) _arrays[kv.Key] = kv.Value;
            Check($"the game's arrays are as they were ({when})", now.All(kv => _arrays[kv.Key] == kv.Value), string.Join(" ", now.Select(kv => kv.Key + "=" + kv.Value)));
        }

        static void Random(CustomGoblinService s)
        {
            var ours = new HashSet<uint>(Parts.Select(p => Id(p.id)));
            int hits = 0, looks = 0;
            foreach (var filter in new[] { 1, 5 })      // as NPCs are drawn, as players are
                for (int i = 0; i < 1500; i++)
                {
                    var d = s.GetRandom((CustomGoblinService.RandomFilter)filter);
                    looks++;
                    var ids = new List<uint> { d.HornId, d.HairId, d.ChestId, d.LegsId, d.FeetId };
                    for (int k = 0; d.Accessories != null && k < d.Accessories.Count; k++) ids.Add(d.Accessories[k]);
                    if (ids.Any(ours.Contains)) hits++;
                }
            Check("no random goblin wears a mod's part", hits == 0, $"{hits} of {looks}");
        }

        static void Meta()
        {
            try
            {
                var ours = new HashSet<uint>(Parts.Select(p => Id(p.id)));
                var m = SaveLoad.MetaData;
                if (m == null) { _log("cosm: no MetaData in this run"); return; }
                int before = m.UnlockedCosmetics.Count;
                try { SaveLoad.ConfirmUnlockedCosmetics(); } catch (Exception ex) { _log($"cosm: ConfirmUnlockedCosmetics threw {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
                int bad = 0;
                for (int i = 0; i < m.UnlockedCosmetics.Count; i++) if (ours.Contains(m.UnlockedCosmetics[i])) bad++;
                for (int i = 0; i < m.NewCosmetics.Count; i++) if (ours.Contains(m.NewCosmetics[i])) bad++;
                Check("nothing of ours in the game's list of unlocked rewards", bad == 0, $"{bad}; the list had {before}, has {m.UnlockedCosmetics.Count}");
            }
            catch (Exception ex) { Check("nothing of ours in the game's list of unlocked rewards", false, ex.GetType().Name + ": " + ex.Message); }
        }

        // Which of our parts the wardrobe shows now, told by the thumbnail: what is on the screen, not what a list says. "Hood!" = in the
        // game's locked section, under its padlock.
        static readonly string[] Extra = { "Secret" };
        static List<string> Shown()
        {
            var shown = new List<string>();
            foreach (var (content, inLocked) in new[] { (_ui.m_scrollViewContentUnlocked, false), (_ui.m_scrollViewContentLocked, true) })
                foreach (var item in content.GetComponentsInChildren<CustomizationItem>(false))
                    foreach (var id in Parts.Select(p => p.id).Concat(Extra))
                    {
                        var c = _ui.m_customGoblin.GetPartObject(Id(id));
                        if (c == null || c.m_thumbnail == null || item.m_thumbnail == null || item.m_thumbnail.sprite == null || item.m_thumbnail.sprite.Pointer != c.m_thumbnail.Pointer) continue;
                        bool padlock = item.m_isLocked && item.m_lockedItemObject != null && item.m_lockedItemObject.activeSelf;
                        shown.Add(id + (inLocked && padlock ? "!" : inLocked || padlock ? "?" : ""));
                    }
            return shown;
        }

        static string Worn(EntityCosmetics e, bool fp = false)
        {
            var d = fp ? e.m_currentPartsFP : e.m_currentPartsTP;
            var l = new List<string>();
            for (int pt = 0; pt <= 18 && d != null; pt++) if (d.ContainsKey((CustomGoblinService.Part)pt)) l.Add($"{(CustomGoblinService.Part)pt}={d[(CustomGoblinService.Part)pt]?.name}");
            return string.Join(", ", l);
        }

        /// <summary>One step in the main menu; seconds to wait, or -1 when it is over.</summary>
        internal static float MenuTick()
        {
            switch (_m)
            {
                case 0:
                    _ui = Resources.FindObjectsOfTypeAll<NetworkMenuCharacterAppearance>().FirstOrDefault(u => u.gameObject.scene.IsValid());
                    if (_ui == null) { Check("wardrobe", false, "no NetworkMenuCharacterAppearance"); MenuDone = true; return -1; }
                    NetworkMenu.Instance.SetState(_ui); _m = -1; return 4f;
                case -1:
                    // Without Steam the game does not get to this by itself (no saved look), and then the goblin has no head anchor.
                    try { _ui.InitialiseCharacter(); } catch (Exception ex) { _log($"cosm: InitialiseCharacter threw {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
                    _m = 1; return 3f;
                case 1:
                {
                    var s = _ui.m_customGoblin;
                    Arrays(s, "wardrobe open");
                    foreach (var p in Parts) { var c = s.GetPartObject(Id(p.id)); Check($"the game finds {p.id}", c != null && c.m_id == Id(p.id), c != null ? $"{c.name} {c.m_itemType} layer {LayerMask.LayerToName(c.gameObject.layer)} thumb {(c.m_thumbnail != null ? c.m_thumbnail.texture.width : 0)}" : "no"); }
                    Random(s);
                    _m++; return 0.5f;
                }
                case 2:
                    if (_cat >= Cats.Length) { _m = 4; return 0.1f; }
                    _ui.SetCategory(Cats[_cat].cat, true); _m++; return 3f;
                case 3:
                {
                    // A locked part stands in the game's locked section with the padlock; the secret is not there at all.
                    var want = Parts.Where(p => p.slot == Cats[_cat].slot).Select(p => p.id + (p.locked ? "!" : "")).OrderBy(x => x).ToList();
                    var got = Shown().OrderBy(x => x).ToList();
                    Check($"the wardrobe shows ours under {Cats[_cat].cat}", want.SequenceEqual(got), $"want {string.Join(",", want)}; on the screen {string.Join(",", got)}; items {_ui.m_scrollViewContentUnlocked.childCount}+{_ui.m_scrollViewContentLocked.childCount}");
                    _cat++; _m = 2; return 0.1f;
                }
                case 4: _ui.SetCategory(CustomGoblinService.Category.Hair, true); _m = 41; return 3f;
                case 41:
                {
                    // A click on the padlocked hood, as far as the game can tell: the toggle itself.
                    _hairBefore = _ui.m_customizationData.HairId;
                    var hood = _ui.m_customGoblin.GetPartObject(Id("Hood"));
                    var item = _ui.m_scrollViewContentLocked.GetComponentsInChildren<CustomizationItem>(false).FirstOrDefault(i => i.m_thumbnail.sprite != null && i.m_thumbnail.sprite.Pointer == hood.m_thumbnail.Pointer);
                    Check("the padlocked hood is there to click, and the game lets no click through", item != null && !item.m_toggle.interactable,
                          item != null ? $"locked {item.m_isLocked}, interactable {item.m_toggle.interactable}" : "not in the locked section");
                    // As a mouse does it: a click the toggle itself takes or refuses.
                    if (item != null) item.m_toggle.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left });
                    _log("shot cosm_padlock"); _m = 42; return 2f;
                }
                case 42:
                    Check("a click on a padlocked part puts nothing on", _ui.m_customizationData.HairId == _hairBefore && _ui.m_customizationData.HairId != Id("Hood"), $"hair {_ui.m_customizationData.HairId} (was {_hairBefore}), hood {Id("Hood")}");
                    _m = 43; return 0.5f;
                case 43:
                    Check("unlocking", Content.Cosmetics.Unlock(Full("Hood")) && !Content.Cosmetics.Unlock(Full("Hood")) && !Content.Cosmetics.Unlock(Full("Cap")) && Content.Cosmetics.IsUnlocked(Full("Hood")), "Hood once, Cap never was locked");
                    _ui.SetCategory(CustomGoblinService.Category.Hair, true); _m = 5; return 3f;
                case 5:
                    Check("the unlocked one is in the wardrobe, open", Shown().Contains("Hood") && !Shown().Any(s => s.StartsWith("Hood") && s != "Hood"), string.Join(",", Shown()));
                    Check("the game's divider stands over the locked section only when it has something", _ui.m_scrollViewDivider == null || _ui.m_scrollViewDivider.activeSelf == (_ui.m_scrollViewContentLocked.childCount > 0), $"divider {_ui.m_scrollViewDivider?.activeSelf}, locked {_ui.m_scrollViewContentLocked.childCount}");
                    _log("shot cosm_hair"); _m++; return 4f;
                case 6:
                {
                    _ui.SetCategory(CustomGoblinService.Category.Chest, true); _m++; return 3f;
                }
                case 7:
                {
                    // A click, as far as the game can tell: the toggle itself.
                    var vest = _ui.m_customGoblin.GetPartObject(Id("Vest"));
                    var item = _ui.m_scrollViewContentUnlocked.GetComponentsInChildren<CustomizationItem>(false).FirstOrDefault(i => i.m_thumbnail.sprite != null && i.m_thumbnail.sprite.Pointer == vest.m_thumbnail.Pointer);
                    if (item != null) item.m_toggle.isOn = true;
                    _m++; return 1.5f;
                }
                case 8:
                    Check("a click puts the vest on", _ui.m_customizationData.ChestId == Id("Vest"), $"chest {_ui.m_customizationData.ChestId}, vest {Id("Vest")}; wears {Worn(_ui.m_menuCharacter)}");
                    _ui.OnHornSelection(Id("Horns")); _ui.OnHairSelection(Id("Cap")); _ui.OnLegsSelection(Id("Trousers")); _ui.OnFeetSelection(Id("Boots"));
                    for (int i = 0; i < 6; i++) _ui.OnHeadAccessorySelection(i, i == 0 ? Id("Belt") : i == 1 ? Id("Board") : 0u);
                    _ui.OnChestColorSelection(5); _ui.OnHairColorSelection(3); _ui.OnLegsColorSelection(9); _ui.OnHornColorSelection(2);
                    _m++; return 2f;
                case 9:
                {
                    var w = Worn(_ui.m_menuCharacter);
                    Check("the menu goblin wears all seven", new[] { "Horn=", "Hair=", "Chest=", "Legs=", "Feet=", "Accessory1=", "Accessory2=" }.All(w.Contains) && w.Split(Mod + ":").Length - 1 == 7, w);
                    // The game's colour on the first material of hair, horns, chest and legs; none on feet and accessories.
                    foreach (var (part, coloured) in new[] { (CustomGoblinService.Part.Horn, true), (CustomGoblinService.Part.Hair, true), (CustomGoblinService.Part.Chest, true), (CustomGoblinService.Part.Legs, true), (CustomGoblinService.Part.Feet, false), (CustomGoblinService.Part.Accessory1, false) })
                    {
                        var d = _ui.m_menuCharacter.m_currentPartsTP;
                        var r = d.ContainsKey(part) ? d[part].GetComponent<Renderer>() : null;
                        Check($"colour of {part}", r != null && r.HasPropertyBlock() == coloured, r == null ? "no renderer" : $"the game's colour {(r.HasPropertyBlock() ? "on it" : "not on it")}");
                    }
                    _log("shot cosm_front"); _m++; return 5f;
                }
                case 10: if (_ui.m_cameraRotation != null) _ui.m_cameraRotation.Rotate(0f, 180f, 0f); _log("shot cosm_back"); _m++; return 5f;
                case 11: if (_ui.m_cameraRotation != null) _ui.m_cameraRotation.Rotate(0f, -90f, 0f); _ui.OnHairSelection(Id("Mane")); _m++; return 1.5f;
                case 12: Check("a hair style weighted to the body", Worn(_ui.m_menuCharacter).Contains("Hair=" + Full("Mane")), Worn(_ui.m_menuCharacter)); _log("shot cosm_side"); _m++; return 5f;
                case 13:
                    _ui.OnHairSelection(Id("Cap"));
                    Arrays(_ui.m_customGoblin, "after dressing");
                    Meta();
                    _ui.OnBackButton(); _m++; return 4f;
                case 14:
                {
                    // Back does not lead to the main menu in this run; the mod that hosts waits for it.
                    var main = Resources.FindObjectsOfTypeAll<NetworkMenuMainState>().FirstOrDefault(s => s.gameObject.scene.IsValid());
                    if (main != null) NetworkMenu.Instance.SetState(main);
                    _m++; return 2f;
                }
                default:
                    Check("the look is kept", SaveLoad.GetCustomizationData().Contains(Id("Vest").ToString()), SaveLoad.GetCustomizationData());
                    MenuDone = true; return -1;
            }
        }

        /// <summary>A goblin's look through the library's own call.</summary>
        internal static string Json(string look, out int expected, out string tag)
        {
            var l = new GoblinLook(); tag = null;
            foreach (var pair in look.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                int i = pair.IndexOf(':'); if (i <= 0) continue;
                string k = pair.Substring(0, i).Trim(), v = pair.Substring(i + 1).Trim();
                int N() => int.Parse(v);
                switch (k)
                {
                    case "name": tag = v; break;
                    case "horn": l.Horns = v; break;
                    case "hair": l.Hair = v; break;
                    case "chest": l.Chest = v; break;
                    case "legs": l.Legs = v; break;
                    case "feet": l.Feet = v; break;
                    case "acc": l.Accessories.AddRange(v.Split(',').Select(a => a.Trim())); break;
                    case "skin": l.Skin = N(); break;
                    case "eyes": l.EyeColor = N(); break;
                    case "chestcolor": l.ChestColor = N(); break;
                    case "haircolor": l.HairColor = N(); break;
                    case "legscolor": l.LegsColor = N(); break;
                    case "horncolor": l.HornColor = N(); break;
                }
            }
            expected = new[] { l.Horns, l.Hair, l.Chest, l.Legs, l.Feet }.Count(x => x != null) + l.Accessories.Count;
            return Content.Cosmetics.LookJson(l);
        }

        static EntityCosmetics _mine;

        /// <summary>One step in the hub, after the NPCs: the player. Seconds to wait, or -1 when it is over.</summary>
        internal static float HubTick(Action<Vector3, Quaternion> camera, Action free)
        {
            switch (_h++)
            {
                case 0:
                {
                    foreach (var g in Outfit.Made)
                    {
                        var e = g.Npc != null ? g.Npc.Appearance : null;
                        int n = e != null && e.m_currentPartsTP != null ? e.m_currentPartsTP.Count : -1;
                        Check($"NPC {g.Key} wears what its look names", n == g.Expected, $"{n} of {g.Expected}: {(e != null ? Worn(e) : "-")}");
                    }
                    EntityPlayer me = null;
                    foreach (var p in UnityEngine.Object.FindObjectsOfType<EntityPlayer>()) if (p != null && p.IsOwner) me = p;
                    _mine = me != null ? me.GetComponentInChildren<EntityCosmetics>(true) : null;
                    if (_mine == null) { Check("the player", false, "no EntityCosmetics"); HubDone = true; return -1; }
                    string tp = Worn(_mine), fp = Worn(_mine, true);
                    Check("the player came into the game in our clothes", tp.Contains("Chest=" + Full("Vest")) && tp.Contains("Hair=" + Full("Cap")), tp);
                    Check("first person has chest, legs and feet", fp.Contains("Chest=" + Full("Vest")) && fp.Contains("Legs=" + Full("Trousers")) && fp.Contains("Feet=" + Full("Boots")), fp);
                    Arrays(_mine.m_goblinCosmetics, "hub");
                    Random(_mine.m_goblinCosmetics);
                    free();
                    Stage.BringMe();
                    return 3f;
                }
                case 1: _log("shot cosm_fp"); return 4f;
                case 2:
                {
                    var eye = Camera.main.transform;
                    camera(eye.position, Quaternion.LookRotation((eye.forward * 0.45f + Vector3.down).normalized, eye.forward));
                    return 1.5f;
                }
                case 3: _log("shot cosm_fpdown"); return 4f;
                case 4:
                {
                    uint coat = Content.Cosmetics.IdOf("POGBlackMarket:Coat"), hat = Content.Cosmetics.IdOf("POGBlackMarket:Hat");
                    if (coat == 0) { _h = 100; return 0.1f; }
                    _mine.SetObject(CustomGoblinService.Part.Horn, 0); _mine.SetObject(CustomGoblinService.Part.Chest, coat); _mine.SetObject(CustomGoblinService.Part.Hair, hat);
                    for (int i = 13; i <= 18; i++) _mine.SetObject((CustomGoblinService.Part)i, 0);
                    free();
                    return 2f;
                }
                case 5: Check("the coat and the hat on the player", Worn(_mine).Contains("POGBlackMarket:Coat") && Worn(_mine, true).Contains("POGBlackMarket:Coat"), Worn(_mine) + " | FP " + Worn(_mine, true)); _log("shot cosm_fpcoat"); return 4f;
                case 6:
                {
                    var eye = Camera.main.transform;
                    camera(eye.position, Quaternion.LookRotation((eye.forward * 0.45f + Vector3.down).normalized, eye.forward));
                    return 1.5f;
                }
                case 7: _log("shot cosm_fpcoatdown"); return 4f;
                default: free(); HubDone = true; return -1;
            }
        }
    }
}
