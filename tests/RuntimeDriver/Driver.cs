// POGLib runtime driver. Plays the game from inside, without the mouse, and writes one
// "[DRIVER] ..." log line per step so run.sh can screenshot and grep. Never ship it.
//
// Config: <profile>/UserData/POGLib/driver.txt, key=value lines (all optional):
//   slot=2            save slot to start (the last one by default, so slot 0 stays the player's)
//   stay=20           seconds to stay in the hub after the checks
//   dump=1            dump every NpcSpawner and ShopLocation in the hub (vanilla slot table)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Il2Cpp;
using Il2CppGame.Menu;
using Il2CppGame.Spawning;
using MelonLoader;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: MelonInfo(typeof(POGLib.RuntimeDriver.Driver), "POGLibRuntimeDriver", "0.1.0", "test")]
[assembly: MelonGame(null, null)]

namespace POGLib.RuntimeDriver
{
    public class Driver : MelonMod
    {
        const string HubScene = "PitOfGoblin_Lobby";

        enum Step { CosmeticsMenu, CosmeticsHub, Wardrobe, Client, WaitMenu, OpenSaves, StartSlot, WaitHub, Settle, Checks, Look, Outfit, SaveTest, DevTools, Jumps, Dungeon, Stay, Done }

        Step _step = Step.WaitMenu;
        float _next;
        float _deadline;
        Dictionary<string, string> _cfg = new Dictionary<string, string>();
        NetworkMenuGameSaveState _saves;

        int Cfg(string key, int fallback) =>
            _cfg.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : fallback;

        void Log(string msg) => LoggerInstance.Msg("[DRIVER] " + msg);

        public override void OnInitializeMelon()
        {
            try
            {
                var path = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "POGLib", "driver.txt");
                if (File.Exists(path))
                    foreach (var line in File.ReadAllLines(path))
                    {
                        var i = line.IndexOf('=');
                        if (i > 0) _cfg[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                    }
                Log("config: " + string.Join(", ", _cfg.Select(kv => kv.Key + "=" + kv.Value)));
            }
            catch (Exception ex) { Log("config read FAILED " + ex.Message); }
            Checks.Enabled = Cfg("checks", 0) == 1;
            if (Checks.Enabled) Checks.RegisterTestItems();
            Explore.Enabled = Cfg("explore", 0) == 1;
            Noir.Init(_cfg, Log);
            if (Cfg("merchants", 0) == 1) Merchants.Register();
            if (Cfg("cosmetics", 0) == 1) CosmeticsRun.Init(_cfg, Log);
            if (_cfg.TryGetValue("runshow", out var clips)) { RunShow.Init(clips, Log); RunShow.PoseDump = _cfg.TryGetValue("posedump", out var pd) ? pd : null; }
            if (Cfg("noir", 0) == 1) try { Noir.Enable(HarmonyInstance, _cfg, Log); } catch (Exception ex) { Log($"noir: patch FAILED {ex.GetType().Name}: {ex.Message}"); }
        }

        // Every way out ends with the player's setting back at its default and the check count.
        void Finish(string how)
        {
            if (CosmeticsRun.Enabled && !Checks.Enabled) Log("checks: " + Checks.Summary);
            if (Checks.Enabled)
            {
                Checks.SetVanillaWeight(10);
                Log("checks: " + Checks.Summary);
            }
            _step = Step.Done;
            Log(how);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Log($"scene loaded: {sceneName} (build {buildIndex}); {Role()}");
            if (_cfg.ContainsKey("entrance") && sceneName == "GameScene") Noir.OnScene();
        }

        static string Role()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return "no NetworkManager";
            if (!nm.IsListening) return "not listening";
            return nm.IsHost ? "host" : nm.IsServer ? "server" : "client";
        }

        bool _deleting;   // savetest=delete: at the main menu, then done

        public override void OnUpdate()
        {
            if (_deleting)
            {
                try { if (SaveTest.Delete(Cfg("slot", 2))) { _deleting = false; Finish("done"); } }
                catch (Exception ex) { Log($"save: delete FAILED {ex.GetType().Name}: {ex.Message}"); _deleting = false; Finish("done"); }
                return;
            }
            if (_step == Step.Done || Time.unscaledTime < _next) return;
            try { Tick(); }
            catch (Exception ex)
            {
                Log($"step {_step} FAILED {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                if (Checks.Enabled) Checks.Check("driver steps", false, $"step {_step} threw");
                Finish("done (failed)");
            }
        }

        void Tick()
        {
            switch (_step)
            {
                case Step.WaitMenu:
                    // The main menu is up when NetworkMenu exists and its current state is the main state.
                    // configpage=1: the open panel is a menu state of its own, so its steps do not wait for the main menu.
                    if (_pageOpen && !_pageDone) { float w = Merchants.PageStep(); if (w > 0) { _next = Time.unscaledTime + w; return; } _pageDone = true; Log("page: done"); _next = Time.unscaledTime + 2f; return; }
                    // start=0: the mod that hosts may be through the main menu between two of our looks (2026-11-25: 0.1 s, the
                    // driver then waited for the menu for ever; likely the one earlier run of seven that hung in the hub).
                    if (Cfg("start", 1) == 0 && Role() == "host") { Log("hosting already (the main menu was passed between two looks)"); _step = Step.WaitHub; _deadline = Time.unscaledTime + 240f; _next = Time.unscaledTime + 1f; return; }
                    var menu = NetworkMenu.Instance;
                    if (menu == null || menu.m_currentState == null) { _next = Time.unscaledTime + 1f; return; }
                    if (menu.m_currentState.TryCast<NetworkMenuMainState>() == null) { _next = Time.unscaledTime + 1f; return; }
                    Log($"main menu up; login {menu.m_currentLoginState}; {Role()}");
                    if (Checks.Enabled) Checks.SetVanillaWeight(0);   // must reach the hub's picks: the next scene load
                    if (Explore.Enabled) Explore.AtMenu();
                    if (_cfg.TryGetValue("savetest", out var st) && st == "delete") { _deleting = true; return; }
                    // start=0: another mod hosts (POGLanTest on a machine without Steam, where only its LAN hosting
                    // works); the driver only waits for the hub as host.
                    if (CosmeticsRun.Enabled && !CosmeticsRun.MenuDone) { _step = Step.CosmeticsMenu; _next = Time.unscaledTime + 4f; return; }
                    if (Cfg("wardrobe", 0) == 1 && _wd == 0) { _step = Step.Wardrobe; _next = Time.unscaledTime + 6f; return; }
                    if (Cfg("configpage", 0) == 1 && !_pageDone) { _pageOpen = true; _next = Time.unscaledTime + 0.5f; return; }
                    if (Cfg("client", 0) == 1) { Log("client=1: waiting for the dungeon"); _step = Step.Client; _deadline = Time.unscaledTime + 900f; _next = Time.unscaledTime + 1f; return; }
                    if (Cfg("start", 1) == 0) { Log("start=0: waiting for someone else to host"); _step = Step.WaitHub; _deadline = Time.unscaledTime + 240f; _next = Time.unscaledTime + 1f; return; }
                    _step = Step.OpenSaves; _next = Time.unscaledTime + 3f;
                    return;

                case Step.CosmeticsMenu:
                {
                    float w = CosmeticsRun.MenuTick();
                    if (w < 0) { _step = Step.WaitMenu; w = 1f; }
                    _next = Time.unscaledTime + w;
                    return;
                }

                case Step.CosmeticsHub:
                {
                    if (RunShow.Wanted && !RunShow.Done)
                    {
                        float r = RunShow.Tick((pos, rot) => { HookCamera(); _ovPos = pos; _ovRot = rot; _override = true; }, () => _override = false);
                        _next = Time.unscaledTime + (r < 0 ? 0.5f : r);
                        return;
                    }
                    if (!CosmeticsRun.Enabled) { _step = Cfg("dungeon", 0) == 1 ? Step.Dungeon : Step.Stay; _next = Time.unscaledTime + 1f; return; }
                    float w = CosmeticsRun.HubTick((pos, rot) => { HookCamera(); _ovPos = pos; _ovRot = rot; _override = true; }, () => _override = false);
                    if (w < 0) { _step = Cfg("dungeon", 0) == 1 ? Step.Dungeon : Step.Stay; w = _step == Step.Stay ? Cfg("stay", 20) : 1f; }
                    _next = Time.unscaledTime + w;
                    return;
                }

                // wardrobe=1: open the game's own wardrobe in the main menu, show accessories and chests, put on what is named
                // (wardrobechest=, wardrobeacc= ids; wardrobecolor= clothes colour), then leave it.
                case Step.Wardrobe:
                {
                    var ui = Resources.FindObjectsOfTypeAll<Il2CppGame.Menu.NetworkMenuCharacterAppearance>().FirstOrDefault(u => u.gameObject.scene.IsValid());
                    if (ui == null) { Log("wardrobe: no NetworkMenuCharacterAppearance in the scene"); _wd = 99; _step = Step.WaitMenu; _next = Time.unscaledTime + 1f; return; }
                    uint Id(string k) => _cfg.TryGetValue(k, out var v) && uint.TryParse(v, out var n) ? n : 0;
                    switch (_wd++)
                    {
                        case 0: NetworkMenu.Instance.SetState(ui); Log("wardrobe: opened"); _next = Time.unscaledTime + 8f; return;
                        case 1: ui.SetCategory(CustomGoblinService.Category.Hair, true); Log($"wardrobe: hairs {ui.m_customGoblin.m_hairs.Length}, chests {ui.m_customGoblin.m_chests.Length}"); _next = Time.unscaledTime + 8f; return;
                        case 2: Log("shot wardrobe_hair"); _next = Time.unscaledTime + 4f; return;
                        case 3:
                        {
                            // An own hair style of the game first (what "right" looks like), then the one asked for.
                            var e = ui.m_menuCharacter;
                            Log($"wardrobe: appearance {(e != null ? e.name : "null")} head {(e?.m_headAnchor_TP != null ? e.m_headAnchor_TP.name : "-")}");
                            if (e != null && e.m_headAnchor_TP == null) { e.RefreshAnchors(); Log($"wardrobe: after RefreshAnchors head {(e.m_headAnchor_TP != null ? e.m_headAnchor_TP.name : "-")}"); }
                            ui.OnHairSelection(1588855695); ui.OnHairColorSelection(4);
                            _next = Time.unscaledTime + 2f; return;
                        }
                        case 4: Log("shot wardrobe_hairvanilla"); _next = Time.unscaledTime + 4f; return;
                        case 5:
                            if (Id("wardrobehair") != 0) ui.OnHairSelection(Id("wardrobehair"));
                            if (_cfg.ContainsKey("wardrobehaircolor")) ui.OnHairColorSelection((ushort)Cfg("wardrobehaircolor", 0));
                            if (ui.m_scrollRect != null) ui.m_scrollRect.verticalNormalizedPosition = CfgF("wardrobescroll", 0.45f);
                            _next = Time.unscaledTime + 2f; return;
                        case 6: Log("shot wardrobe_hat"); _next = Time.unscaledTime + 4f; return;
                        case 7: ui.SetCategory(CustomGoblinService.Category.Chest, true); _next = Time.unscaledTime + 3f; return;
                        case 8: if (Id("wardrobechest") != 0) ui.OnChestSelection(Id("wardrobechest")); _next = Time.unscaledTime + 2f; return;
                        case 9: Log("shot wardrobe_chest"); _next = Time.unscaledTime + 4f; return;
                        case 10: if (_cfg.ContainsKey("wardrobecolor")) ui.OnChestColorSelection((ushort)Cfg("wardrobecolor", 0)); _next = Time.unscaledTime + 2f; return;
                        case 11: Log("shot wardrobe_color"); _next = Time.unscaledTime + 4f; return;
                        case 12:
                        {
                            var rs = Resources.FindObjectsOfTypeAll<Il2CppGame.Players.EntityCosmetics>().Where(e => e.gameObject.scene.IsValid() && e.m_currentPartsTP != null);
                            foreach (var e in rs)
                            {
                                for (int pt = 0; pt <= 18; pt++)
                                    foreach (var (which, dict) in new[] { ("TP", e.m_currentPartsTP), ("FP", e.m_currentPartsFP) })
                                        if (dict != null && dict.ContainsKey((CustomGoblinService.Part)pt)) { var go = dict[(CustomGoblinService.Part)pt]; Log($"wardrobe:   {which} {(CustomGoblinService.Part)pt} = {(go != null ? $"{go.name} layer {LayerMask.LayerToName(go.layer)} active {go.activeInHierarchy} at {go.transform.position:F2} scale {go.transform.lossyScale:F2} parent {go.transform.parent?.name}" : "null")}"); }
                                var bodyR = e.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name.StartsWith("CH_Goblin"));
                                Log($"wardrobe:   rig TP {(e.m_rigObjectTP != null ? e.m_rigObjectTP.name : "-")} FP {(e.m_rigObjectFP != null ? e.m_rigObjectFP.name : "-")} head {(e.m_headAnchor_TP != null ? e.m_headAnchor_TP.name : "-")} body {(bodyR != null ? bodyR.name + " layer " + LayerMask.LayerToName(bodyR.gameObject.layer) + " verts " + bodyR.sharedMesh.vertexCount : "-")}");
                            }
                            foreach (var e in rs) Log($"wardrobe: {e.name} wears chest {e.m_currentCustomizationData.ChestId} acc {e.m_currentCustomizationData.GetAccessory(0)} colour {e.m_currentCustomizationData.ChestColorIndex}; parts {e.m_currentPartsTP.Count}");
                            ui.OnBackButton(); Log("wardrobe: left"); _next = Time.unscaledTime + 4f; return;
                        }
                        default:
                            Log($"wardrobe: after it hairs {ui.m_customGoblin?.m_hairs.Length}, chests {ui.m_customGoblin?.m_chests.Length}; state {NetworkMenu.Instance.m_currentState?.GetIl2CppType().Name}");
                            if (Cfg("wardrobeonly", 0) == 1) { Finish("done"); return; }
                            _step = Step.WaitMenu; _next = Time.unscaledTime + 1f; return;
                    }
                }

                // A client that another mod joins (POGLanTest): in the dungeon, look at the merchant and ask for shots
                // ("[LAN-SHOT] want <name>", taken by the laptop's watcher).
                case Step.Client:
                {
                    if (Time.unscaledTime > _deadline) { Finish("done (client: no dungeon)"); return; }
                    if (SceneManager.GetActiveScene().name != "GameScene" || Role() != "client") { _next = Time.unscaledTime + 1f; return; }
                    if (_cl == 0) { Log("client: in the dungeon"); _cl = 1; _next = Time.unscaledTime + 20f; return; }
                    if (_cl == 1)
                    {
                        NpcSpawner sp = null;
                        foreach (var x in UnityEngine.Object.FindObjectsOfType<NpcSpawner>())
                            for (var t = x.transform; t != null; t = t.parent) if (t.name.StartsWith("EntranceCave_Shopkeeper")) sp = x;
                        _clNpc = sp == null ? null : UnityEngine.Object.FindObjectsOfType<Il2CppEntities.NPCEntity>().OrderBy(n => Vector3.Distance(n.transform.position, sp.transform.position)).FirstOrDefault();
                        if (_clNpc == null) { Log($"client: no merchant yet (spawner {(sp != null)})"); _next = Time.unscaledTime + 3f; return; }
                        var ap = _clNpc.Appearance;
                        Log($"client: merchant {_clNpc.name} at {_clNpc.transform.position:F2} tag='{_clNpc.m_npcName}' chest id {_clNpc.CosmeticsData.ChestId} worn {(ap?.m_currentPartsTP != null ? ap.m_currentPartsTP.Count : -1)}; draws {string.Join(", ", _clNpc.GetComponentsInChildren<Renderer>(false).Select(r => r.name))}");
                        _cl = 2; _next = Time.unscaledTime + 0.5f; return;
                    }
                    if (_cl <= 4 && _clNpc != null)
                    {
                        HookCamera();
                        var f = _clNpc.transform.position + Vector3.up * 0.7f;
                        float d = _cl == 3 ? 3.2f : 1.5f;
                        _ovPos = f + (_cl == 4 ? (_clNpc.transform.forward + _clNpc.transform.right).normalized : _clNpc.transform.forward) * d + Vector3.up * (_cl == 3 ? 0.9f : 0.2f);
                        _ovRot = Quaternion.LookRotation(f - _ovPos); _override = true;
                        if (!_clAsked) { _clAsked = true; _next = Time.unscaledTime + 1.5f; return; }
                        _clAsked = false;
                        LoggerInstance.Msg("[LAN-SHOT] want " + new[] { "noirclient_front", "noirclient_wide", "noirclient_side" }[_cl - 2]);
                        _cl++; _next = Time.unscaledTime + 6f; return;
                    }
                    _override = false;
                    Finish("done");
                    return;
                }

                case Step.OpenSaves:
                {
                    // Only the instance that lives in the menu scene: FindObjectsOfTypeAll also returns
                    // prefab/asset copies whose references are not wired (both earlier attempts NRE'd).
                    var all = Resources.FindObjectsOfTypeAll<NetworkMenuGameSaveState>();
                    Log($"save states in memory: {all.Length}: " + string.Join(", ", all.Select(s => $"{s.name}(scene '{s.gameObject.scene.name}', active {s.gameObject.activeInHierarchy})")));
                    _saves = all.FirstOrDefault(s => s.gameObject.scene.IsValid() && s.gameObject.scene.name == SceneManager.GetActiveScene().name)
                             ?? all.FirstOrDefault(s => s.gameObject.scene.IsValid());
                    if (_saves == null) throw new InvalidOperationException("no NetworkMenuGameSaveState in a loaded scene");
                    NetworkMenu.Instance.SetState(_saves);
                    Log($"save menu opened: {PathOf(_saves.transform)}; slots {_saves.m_saveSlots?.Count}");
                    _step = Step.StartSlot; _next = Time.unscaledTime + 3f;
                    return;
                }

                case Step.StartSlot:
                {
                    int count = _saves.m_saveSlots?.Count ?? 0;
                    int slot = Cfg("slot", Math.Max(0, count - 1));
                    for (int i = 0; i < count; i++)
                    {
                        var sm = _saves.m_saveSlots[i];
                        Log($"  slot {i}: {(sm == null ? "null" : PathOf(sm.transform) + $" active {sm.gameObject.activeInHierarchy}, menuButton {(sm.m_menuButton == null ? "null" : "ok")}, button {(sm.m_menuButton?.m_button == null ? "null" : "ok")}")}");
                    }
                    // Three ways in, most faithful first; log which one worked.
                    string how = null;
                    try
                    {
                        var b = _saves.m_saveSlots[slot]?.m_menuButton?.m_button;
                        if (b == null) throw new NullReferenceException("slot button missing");
                        b.onClick.Invoke(); how = "slot button click";
                    }
                    catch (Exception ex) { Log($"  click failed: {ex.GetType().Name}: {ex.Message}"); }
                    if (how == null)
                        try { _saves.StartSelectedGame(slot); how = "StartSelectedGame"; }
                        catch (Exception ex) { Log($"  StartSelectedGame failed: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
                    if (how == null)
                        try { _saves.SetSelectedSaveFile(slot); _saves.StartGame(); how = "SetSelectedSaveFile+StartGame"; }
                        catch (Exception ex) { Log($"  SetSelectedSaveFile+StartGame failed: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
                    if (how == null) throw new InvalidOperationException("could not start a save slot");
                    Log($"started slot {slot} of {count} via {how}");
                    _step = Step.WaitHub; _deadline = Time.unscaledTime + 180f; _next = Time.unscaledTime + 1f;
                    return;
                }

                case Step.WaitHub:
                    var active = SceneManager.GetActiveScene().name;
                    if (active == HubScene && Role() == "host")
                    {
                        Log($"in hub as host");
                        _step = Step.Settle; _next = Time.unscaledTime + 12f;   // let the spawners run
                        return;
                    }
                    NavigateMenus(active);
                    if (Time.unscaledTime > _deadline) throw new TimeoutException($"no hub after 180 s; active scene {active}; {Role()}");
                    _next = Time.unscaledTime + 1f;
                    return;

                case Step.Settle:
                    Log("shot hub");
                    _step = Step.Checks; _next = Time.unscaledTime + 3f;
                    return;

                case Step.Checks:
                    if (Explore.Enabled) Explore.InHub();
                    if (Cfg("dump", 1) == 1) DumpShops();
                    CheckShopRules();
                    if (Checks.Enabled) { Checks.DumpNetworkPrefabs(); Checks.HubChecks(); }
                    foreach (var o in (_cfg.TryGetValue("objcheck", out var oc) ? oc : "").Split(';', StringSplitOptions.RemoveEmptyEntries)) Checks.ObjFacesOutward(o.Trim());
                    LogStandingPoints();
                    _looks = (_cfg.TryGetValue("look", out var l) ? l : "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
                    _step = Step.Look; _next = Time.unscaledTime + 1f;
                    return;

                case Step.Look:
                    if (_lookIndex > 0) DestroyLookCamera();
                    if (_lookIndex < _looks.Count)
                    {
                        string target = _looks[_lookIndex];
                        Log(LookAt(target) ? $"shot look_{_lookIndex}" : $"look {_lookIndex}: '{target}' not found");
                        _lookIndex++;
                        _next = Time.unscaledTime + 5f;   // run.sh shoots ~2.2 s after the line
                        return;
                    }
                    _step = Step.Outfit;
                    _next = Time.unscaledTime + 1f;
                    return;

                case Step.Outfit:
                    if (OutfitStep()) _step = CosmeticsRun.Enabled || RunShow.Wanted ? Step.CosmeticsHub : _cfg.ContainsKey("savetest") ? Step.SaveTest : Cfg("devtools", 0) == 1 ? Step.DevTools : Step.Jumps;
                    return;

                case Step.SaveTest:
                    if (SaveTest.Tick(_cfg["savetest"])) { _step = Cfg("devtools", 0) == 1 ? Step.DevTools : Step.Jumps; }
                    _next = Time.unscaledTime + 0.5f;
                    return;

                case Step.DevTools:
                    if (DevToolsStep()) { _step = Step.Jumps; _next = Time.unscaledTime + 1f; }
                    return;

                case Step.Jumps:
                    if (JumpStep()) { _step = Cfg("dungeon", 0) == 1 ? Step.Dungeon : Step.Stay; _next = Time.unscaledTime + (_step == Step.Stay ? Cfg("stay", 20) : 1f); }
                    return;

                case Step.Dungeon:
                    if (DungeonStep()) { _step = Step.Stay; _next = Time.unscaledTime + Cfg("stay", 20); }
                    return;

                case Step.Stay:
                    Finish("done");
                    return;
            }
        }

        List<string> _looks = new List<string>();
        int _lookIndex;
        GameObject _lookCam;

        // What the shop rules decided per slot, and what actually lies on each slot's item holder: the
        // game's result, independent of the library's own bookkeeping.
        void CheckShopRules()
        {
            var records = POGLib.Shops.ShopRuntime.Records;
            Log($"shops: {records.Count} slot record(s)");
            var items = UnityEngine.Object.FindObjectsOfType<InventoryItem>(true).Where(i => !POGLib.Shops.ShopScene.IsTemplate(i.transform)).ToArray();
            foreach (var kv in records.OrderBy(k => k.Key))
            {
                var rec = kv.Value;
                if (rec.Resolved.Untouched) continue;
                string chances = string.Join(", ", rec.Resolved.Entries.Select(e => $"{e.Item}={100 * rec.Resolved.ChanceOf(e):0.#}%({e.AddedBy})"));
                Log($"  rule {kv.Key}: picked {rec.Picked}; {chances}{(rec.Notes.Count > 0 ? "; notes: " + string.Join(" | ", rec.Notes) : "")}");
                var slot = rec.Slot;
                if (slot == null) { Log("    slot object gone"); continue; }
                var holder = slot.m_itemHolder != null ? slot.m_itemHolder : slot.transform;
                // The game links a stocked item to its slot (InventoryItem.ShopLocation); fall back to distance.
                var mine = items.Where(i => i != null && i.ShopLocation != null && i.ShopLocation.Pointer == slot.Pointer).ToList();
                if (mine.Count == 0)
                    mine = items.Where(i => i != null && Vector3.Distance(i.transform.position, holder.position) < 0.6f)
                                .OrderBy(i => Vector3.Distance(i.transform.position, holder.position)).Take(2).ToList();
                if (mine.Count == 0) Log($"    item: none linked to the slot, nothing within 0.6 m of {holder.position:F2}");
                foreach (var it in mine)
                    Log($"    item: {it.name} id='{it.m_itemID}' linked={(it.ShopLocation != null && it.ShopLocation.Pointer == slot.Pointer)} active={it.gameObject.activeInHierarchy} shopItem={it.IsShopItem} shopCost={it.ShopCost} dist={Vector3.Distance(it.transform.position, holder.position):F2} offset={holder.InverseTransformPoint(it.transform.position).ToString("F3")}");
                Log($"    tag '{(slot.m_priceTagText != null ? slot.m_priceTagText.text : "-")}' at {(slot.m_priceTagText != null ? (slot.m_priceTagText.transform.position - holder.position).ToString("F2") : "-")} from holder, active={(slot.m_priceTagText != null && slot.m_priceTagText.gameObject.activeInHierarchy)} price={slot.CurrentPrice} faction={slot.m_shopFaction?.Value} keeper={(slot.m_shopKeeper != null ? slot.m_shopKeeper.m_npcFlag : "-")} spawned={slot.IsSpawned}");
            }
        }

        // Point the game's own camera at a slot for one shot. A second camera does not show (the game
        // composes its own final image, 2026-11-12), so the main camera's pose is overridden right before
        // each render, after the game's own camera code has run for the frame.
        static bool _override;
        static Vector3 _ovPos;
        static Quaternion _ovRot;
        static Il2CppSystem.Action<UnityEngine.Rendering.ScriptableRenderContext, Camera> _beginCam;
        static Il2CppSystem.Action<UnityEngine.Rendering.ScriptableRenderContext, Il2CppSystem.Collections.Generic.List<Camera>> _beginContext;

        // beginContextRendering fires before HDRP culls and builds its view matrices (beginCameraRendering
        // alone moved only the interaction outline, 2026-11-12); keep both.
        static void OnBeginCamera(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
        {
            if (_override && cam != null && cam == Camera.main) cam.transform.SetPositionAndRotation(_ovPos, _ovRot);
        }

        static void OnBeginContext(UnityEngine.Rendering.ScriptableRenderContext ctx, Il2CppSystem.Collections.Generic.List<Camera> cams)
        {
            if (_override && Camera.main != null) Camera.main.transform.SetPositionAndRotation(_ovPos, _ovRot);
        }

        public override void OnLateUpdate()
        { RunShow.LateUpdate(); 
            if (_override && Camera.main != null) Camera.main.transform.SetPositionAndRotation(_ovPos, _ovRot);
        }

        void HookCamera()
        {
            if (_beginCam == null)
            {
                _beginCam = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<UnityEngine.Rendering.ScriptableRenderContext, Camera>>(
                    new Action<UnityEngine.Rendering.ScriptableRenderContext, Camera>(OnBeginCamera));
                UnityEngine.Rendering.RenderPipelineManager.add_beginCameraRendering(_beginCam);
                _beginContext = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<UnityEngine.Rendering.ScriptableRenderContext, Il2CppSystem.Collections.Generic.List<Camera>>>(
                    new Action<UnityEngine.Rendering.ScriptableRenderContext, Il2CppSystem.Collections.Generic.List<Camera>>(OnBeginContext));
                UnityEngine.Rendering.RenderPipelineManager.add_beginContextRendering(_beginContext);
                Log($"main camera: {(Camera.main != null ? Camera.main.name : "none")}; cameras: {string.Join(", ", Camera.allCameras.Select(c => c.name + "@" + c.depth))}");
            }
        }

        bool LookAt(string id) => LookAtTransform(POGLib.Shops.ShopScene.FindAnchor(id));

        bool LookAtTransform(Transform target)
        {
            if (target == null) return false;
            HookCamera();
            var slot = target.GetComponent<ShopLocation>();
            var focus = slot != null && slot.m_itemHolder != null ? slot.m_itemHolder.position : target.position + Vector3.up;
            var keeper = slot != null && slot.m_shopKeeper != null ? slot.m_shopKeeper.transform : null;
            Vector3 dir = keeper != null ? keeper.forward : -target.forward;
            dir.y = 0; if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward; dir.Normalize();
            float dist = float.Parse(_cfg.TryGetValue("lookdist", out var d) ? d : "1.1", System.Globalization.CultureInfo.InvariantCulture);
            float height = float.Parse(_cfg.TryGetValue("lookheight", out var hh) ? hh : "0.45", System.Globalization.CultureInfo.InvariantCulture);
            _ovPos = focus + dir * dist + Vector3.up * height;
            _ovRot = Quaternion.LookRotation(focus - _ovPos);
            _override = true;
            return true;
        }

        void DestroyLookCamera() => _override = false;

        // Goblins in looks of our own (Outfit.cs): dump, spawn, then one shot from the front and one from the side each.
        int _of;
        bool OutfitStep()
        {
            if (_of == 0)
            {
                if (Cfg("outfits", 0) == 1) Outfit.Dump(Log);
                if (Cfg("npcs", 0) == 1) Noir.DumpNpcPrefabs(Log);
                if (Cfg("items", 0) == 1) Noir.DumpItems(Log);
                Outfit.Make(_cfg, Log);
                _of = 1; _next = Time.unscaledTime + (Outfit.Made.Count > 0 ? 5f : 0.5f);
                return false;
            }
            int shot = _of - 1, i = shot / 2;
            if (i >= Outfit.Made.Count) { DestroyLookCamera(); _next = Time.unscaledTime + 1f; return true; }
            var g = Outfit.Made[i];
            bool side = shot % 2 == 1;
            if (!side) Outfit.Report(g);
            if (shot == 0 && g.Npc != null && _cfg.TryGetValue("rigdump", out var rigPath))
                try { RigDump.Write(g.Npc, rigPath, Log); } catch (Exception ex) { Log($"rigdump FAILED {ex.GetType().Name}: {ex.Message}"); }
            _of++;
            if (g.Npc == null) { _next = Time.unscaledTime + 0.1f; return false; }
            var t = g.Npc.transform;
            HookCamera();
            var focus = t.position + Vector3.up * CfgF("goblinfocus", 0.75f);
            var dir = side ? (t.forward + t.right).normalized : t.forward;
            _ovPos = focus + dir * CfgF("goblindist", 1.9f) + Vector3.up * 0.25f;
            _ovRot = Quaternion.LookRotation(focus - _ovPos);
            _override = true;
            Log($"shot {g.Key}_{(side ? "side" : "front")}");
            _next = Time.unscaledTime + 5f;
            return false;
        }

        float CfgF(string key, float fallback) =>
            _cfg.TryGetValue(key, out var v) && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : fallback;

        // Where items stand: the shell-built mod item vs. a vanilla item sold in the same shop.
        void LogStandingPoints()
        {
            foreach (var name in new[] { "ModItem_ShopDemo_GlowBerry", "Item_Bread", "Item_Key", "Item_Diamond" })
            {
                var it = POGLib.Core.GameAssets.FindItemPrefab(name);
                if (it == null) { Log($"standing {name}: prefab not found"); continue; }
                var st = it.m_standingTransform;
                string grip = "-";
                foreach (var g in it.GetComponentsInChildren<Il2CppAnimations.IKGrip>(true))
                    if (g.m_slotType == Il2CppAnimations.GripSlot.ShopTable) { grip = g.name + " " + it.transform.InverseTransformPoint(g.transform.position).ToString("F3"); break; }
                Log($"standing {name}: standing={(st != null ? st.name + " " + st.localPosition.ToString("F3") + " parent " + (st.parent != null ? st.parent.name : "-") : "null")}; tableGrip={grip}; activeSelf={it.gameObject.activeSelf}");
            }
        }

        // Drives the shop tools the way a modder would, without keys: aim at the counter next to a slot,
        // place a new slot, show the HUD and the panel, save. Returns true when finished.
        int _dev;
        Type _tools;
        object Call(string m, params object[] a) => _tools.GetMethod(m, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, a);
        void SetField(string f, object v) => _tools.GetField(f, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, v);
        object GetField(string f) => _tools.GetField(f, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        object ModeValue(string name) => Enum.Parse(_tools.GetNestedType("Mode", BindingFlags.NonPublic), name);

        bool DevToolsStep()
        {
            _tools ??= typeof(POGLib.Shops.ShopsPlugin).Assembly.GetType("POGLib.Shops.Dev.ShopDevTools");
            var pref = MelonPreferences.GetEntry<bool>("POGLib_ShopTools", "Enabled");
            string target = _cfg.TryGetValue("devslot", out var t) ? t : null;
            switch (_dev++)
            {
                case 0:
                    pref.Value = true;
                    Call("SetMode", ModeValue("Aim"));
                    Log($"devtools: aim mode, draft mod {((POGLib.Shops.Model.ShopFile)GetField("_draft"))?.Mod}");
                    _next = Time.unscaledTime + 1f; return false;
                case 1:
                {
                    var slot = POGLib.Shops.ShopScene.FindSlot(target);
                    if (slot == null) { Log($"devtools: slot '{target}' not found"); _dev = 5; return false; }
                    var holder = slot.m_itemHolder != null ? slot.m_itemHolder : slot.transform;
                    var keeper = slot.m_shopKeeper != null ? slot.m_shopKeeper.transform : slot.transform;
                    Vector3 right = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(keeper.forward, Vector3.up).normalized);
                    // devoffset = "x,z" in the customer's frame (x right, z towards the customer), default "-0.3,0".
                    var off = (_cfg.TryGetValue("devoffset", out var o) ? o : "-0.3,0").Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    var toCustomer = Vector3.ProjectOnPlane(keeper.forward, Vector3.up).normalized;
                    var from = holder.position + right * off[0] + toCustomer * off[1] + Vector3.up * 0.4f;
                    if (!Physics.Raycast(new Ray(from, Vector3.down), out RaycastHit hit, 1.5f)) { Log("devtools: no counter under the probe ray"); _dev = 5; return false; }
                    SetField("_aimHit", true);
                    SetField("_aimPoint", hit.point);
                    Call("PlaceNewSlot");
                    Log($"devtools: placed at {hit.point:F3} (hit {hit.collider.name}); status '{GetField("_status")}'; selected {GetField("_selected")}");
                    CheckTagPlacement(slot, keeper);
                    // look at it
                    _ovPos = hit.point + Vector3.ProjectOnPlane(keeper.forward, Vector3.up).normalized * 0.9f + Vector3.up * 0.5f;
                    _ovRot = Quaternion.LookRotation(hit.point - _ovPos);
                    if (_beginCam == null) LookAt(target);   // hooks the camera override once
                    _ovPos = hit.point + Vector3.ProjectOnPlane(keeper.forward, Vector3.up).normalized * 0.9f + Vector3.up * 0.5f;
                    _ovRot = Quaternion.LookRotation(hit.point - _ovPos);
                    _override = true;
                    _next = Time.unscaledTime + 1.5f; return false;
                }
                case 2:
                    Log("shot devaim");
                    _next = Time.unscaledTime + 5f; return false;
                case 3:
                    Call("SetMode", ModeValue("Panel"));
                    Log("shot devpanel");
                    _next = Time.unscaledTime + 5f; return false;
                case 4:
                {
                    string status = (string)Call("Save");
                    Log($"devtools: Save -> {status}");
                    var draft = (POGLib.Shops.Model.ShopFile)GetField("_draft");
                    string path = System.IO.Path.Combine(POGLib.Shops.ShopsPlugin.UserShopsDirectory, draft.Mod + ".pogshop.json");
                    if (System.IO.File.Exists(path))
                    {
                        foreach (var line in System.IO.File.ReadAllLines(path)) Log("  saved| " + line);
                        System.IO.File.Delete(path);
                        POGLib.Shops.ShopsPlugin.Instance.Reload();
                        Log("devtools: test file removed");
                    }
                    _next = Time.unscaledTime + 1f; return false;
                }
                default:
                    _override = false;
                    try { Call("SetMode", ModeValue("Off")); } catch (Exception ex) { Log("devtools: close failed " + ex.Message); }
                    pref.Value = false;
                    MelonPreferences.Save();
                    Log("devtools: closed, preference back to false");
                    return true;
            }
        }

        // Down the bucket into a dungeon: find the mod merchant in the entrance cave, then check that the
        // shop tools' timer freeze holds the round clock.
        int _dg;
        float _dgDeadline, _t1;
        int _wd; bool _pageOpen, _pageDone;
        int _cl; bool _clAsked; Il2CppEntities.NPCEntity _clNpc; float _clientsSince;
        bool DungeonStep()
        {
            string scene = SceneManager.GetActiveScene().name;
            switch (_dg)
            {
                case 0:
                {
                    // waitclients=1: go down only with a client that has been in the hub for a while.
                    if (Cfg("waitclients", 0) > 0)
                    {
                        int n = UnityEngine.Object.FindObjectsOfType<Il2CppEntities.EntityPlayer>().Length - 1;   // players that stand in the hub, the host aside
                        if (_dgDeadline == 0f) { _dgDeadline = Time.unscaledTime + 420f; Log("dungeon: waiting for a client"); }
                        if (n < Cfg("waitclients", 0)) { _clientsSince = 0f; if (Time.unscaledTime > _dgDeadline) { Log("dungeon: no client came"); return true; } _next = Time.unscaledTime + 1f; return false; }
                        if (_clientsSince == 0f) { _clientsSince = Time.unscaledTime; Log($"dungeon: {n} client(s) connected"); }
                        if (Time.unscaledTime < _clientsSince + CfgF("clientsettle", 40f)) { _next = Time.unscaledTime + 1f; return false; }
                    }
                    var bucket = UnityEngine.Object.FindObjectsOfType<Il2CppGame.Gameplay.Bucket>().FirstOrDefault();
                    if (bucket == null) { Log("dungeon: no Bucket in the hub"); return true; }
                    Log($"dungeon: bucket at {bucket.m_location?.Value} -> {bucket.m_destination?.Value}; StartBucket()");
                    bucket.StartBucket();
                    _dg = 1; _dgDeadline = Time.unscaledTime + 240f; _next = Time.unscaledTime + 2f; return false;
                }
                case 1:
                    if (scene == "GameScene" && UnityEngine.Object.FindObjectsOfType<NpcSpawner>().Length > 0) { Log($"dungeon: in {scene}"); _dg = 2; _next = Time.unscaledTime + 15f; return false; }
                    if (Time.unscaledTime > _dgDeadline) { Log($"dungeon: no dungeon after 240 s (scene {scene})"); return true; }
                    _next = Time.unscaledTime + 2f; return false;
                case 2:
                {
                    foreach (var sp in POGLib.Shops.ShopScene.SellersInLoadedScenes())
                        Log($"dungeon: seller {POGLib.Shops.ShopScene.SellerId(sp)} root {sp.transform.root.name} slots {sp.m_itemsForSale.Length}");
                    if (Cfg("merchant", 0) == 1) Noir.FindMerchant();
                    if (_cfg.TryGetValue("expectmerchant", out var who)) { try { Merchants.CheckStall(who); } catch (Exception ex) { Checks.Check("stall checks", false, ex.ToString()); } Log("checks: " + Checks.Summary); }
                    if (Cfg("noir", 0) == 1 || Cfg("merchant", 0) == 1) { _dg = 10; _next = Time.unscaledTime + 1f; return false; }
                    CheckShopRules();
                    var mine = POGLib.Shops.ShopRuntime.Records.Values.Where(r => r.SlotId.Contains("#")).OrderBy(r => r.SlotId).ToList();
                    foreach (var r in mine) Log($"dungeon: mod merchant slot {r.SlotId} picked {r.Picked}");
                    if (Checks.Enabled) Checks.MerchantChecks(mine);
                    if (mine.Count > 0 && mine[0].Slot != null && LookAtTransform(mine[0].Slot.transform)) Log("shot merchant");
                    else if (mine.Count == 0) Log("dungeon: no mod merchant in this dungeon");
                    _dg = 3; _next = Time.unscaledTime + 5f; return false;
                }
                case 3:
                {
                    _override = false;
                    _tools ??= typeof(POGLib.Shops.ShopsPlugin).Assembly.GetType("POGLib.Shops.Dev.ShopDevTools");
                    MelonPreferences.GetEntry<bool>("POGLib_ShopTools", "Enabled").Value = true;
                    Call("SetMode", ModeValue("Aim"));
                    SetField("_freeze", true);
                    var round = Il2CppGame.Gameplay.GameMode_Round.Instance;
                    _t1 = round != null ? round.ElapsedGameTime : -1;
                    Log($"dungeon: round {(round == null ? "none" : $"started {round.m_gameStarted}, elapsed {_t1:F1} of {round.RoundTime:F0}")}; timer frozen");
                    _dg = 4; _next = Time.unscaledTime + 8f; return false;
                }
                case 4:
                {
                    var round = Il2CppGame.Gameplay.GameMode_Round.Instance;
                    float t2 = round != null ? round.ElapsedGameTime : -1;
                    Log($"dungeon: after 8 s frozen, elapsed {t2:F1} (was {_t1:F1}, change {t2 - _t1:F2})");
                    SetField("_freeze", false);
                    _t1 = t2;
                    _dg = 5; _next = Time.unscaledTime + 8f; return false;
                }
                // noir=1: the merchant from the front and with its stall; presets=1: what each preset holds.
                case 10:
                case 11:
                {
                    var n = Noir.Npc;
                    if (n == null) { Log("noir: no noir goblin in this dungeon"); _dg = 12; _next = Time.unscaledTime + 0.5f; return false; }
                    if (_dg == 10)
                    {
                        Log($"noir: {n.name} at {n.transform.position:F2} tag='{n.m_npcName}' spawned={n.IsSpawned} worn {(n.Appearance?.m_currentPartsTP != null ? n.Appearance.m_currentPartsTP.Count : -1)}; draws {string.Join(", ", n.GetComponentsInChildren<Renderer>(false).Select(r => r.name))}");
                        if (Noir.Spawner?.m_itemsForSale != null)
                            Log("noir: slots " + string.Join(", ", Noir.Spawner.m_itemsForSale.Where(x => x != null).Select(x => $"{x.name} holds {(x.CurrentShopItem != null ? x.CurrentShopItem.name : "-")} price {x.CurrentPrice} faction {x.m_shopFaction?.Value}")));
                    }
                    HookCamera();
                    var f = n.transform.position + Vector3.up * 0.7f;
                    float dist = _dg == 10 ? 1.5f : 3.4f;
                    _ovPos = f + n.transform.forward * dist + n.transform.right * (_dg == 10 ? 0.3f : 0.8f) + Vector3.up * (_dg == 10 ? 0.2f : 0.9f);
                    _ovRot = Quaternion.LookRotation(f - _ovPos); _override = true;
                    Log(_dg == 10 ? "shot noir_front" : "shot noir_wide");
                    _dg++; _next = Time.unscaledTime + 5f; return false;
                }
                case 12:
                {
                    if (Cfg("trade", 0) == 1 && Noir.Npc != null)
                    {
                        float wait;
                        try { wait = Merchants.TradeStep(); } catch (Exception ex) { Checks.Check("trade", false, ex.ToString()); wait = 0; }
                        if (wait > 0) { _next = Time.unscaledTime + wait; return false; }
                        Log("checks: " + Checks.Summary);
                    }
                    if (Cfg("presets", 0) != 1) { _override = false; return true; }
                    var list = Noir.MerchantList();
                    var all = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g != null && g.transform.parent == null && !g.scene.IsValid() && g.name.StartsWith("EntranceCave_Shopkeeper")).GroupBy(g => g.name).Select(g => g.First()).OrderBy(g => g.name).ToArray();
                    Log($"noir: {all.Length} presets in memory; the list has {(list != null ? list.PropList.Length : -1)}");
                    Noir.DumpPresets(all);
                    _override = false;
                    return true;
                }
                default:
                {
                    var round = Il2CppGame.Gameplay.GameMode_Round.Instance;
                    float t3 = round != null ? round.ElapsedGameTime : -1;
                    Log($"dungeon: after 8 s running, elapsed {t3:F1} (change {t3 - _t1:F2})");
                    try { Call("SetMode", ModeValue("Off")); } catch { }
                    MelonPreferences.GetEntry<bool>("POGLib_ShopTools", "Enabled").Value = false;
                    // Save writes every setting: put the test value back first, or it lands in the player's cfg.
                    if (Checks.Enabled) Checks.SetVanillaWeight(10);
                    MelonPreferences.Save();
                    return true;
                }
            }
        }

        // The shop tools' scene jump, scene by scene: jump, wait for the scene, dump its shops, shoot.
        List<string> _jumps;
        int _jumpIndex;
        float _jumpDeadline;
        bool _jumpWaiting;
        bool JumpStep()
        {
            _jumps ??= (_cfg.TryGetValue("jumps", out var j) ? j : "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
            if (_jumpIndex >= _jumps.Count) return true;
            string target = _jumps[_jumpIndex];
            string scene = SceneManager.GetActiveScene().name;
            if (!_jumpWaiting)
            {
                _tools ??= typeof(POGLib.Shops.ShopsPlugin).Assembly.GetType("POGLib.Shops.Dev.ShopDevTools");
                Call("JumpTo", target);
                Log($"jump: to {target}; status '{GetField("_status")}'");
                _jumpWaiting = true; _jumpDeadline = Time.unscaledTime + 90f; _next = Time.unscaledTime + 2f; return false;
            }
            if (scene == target && Role() == "host")
            {
                if (Time.unscaledTime < _jumpDeadline - 75f) { _next = Time.unscaledTime + 1f; return false; }   // ~15 s to spawn
                Log($"jump: in {scene}");
                DumpShops();
                CheckShopRules();
                Log($"shot jump_{_jumpIndex}");
                _jumpIndex++; _jumpWaiting = false; _next = Time.unscaledTime + 5f; return false;
            }
            if (Time.unscaledTime > _jumpDeadline) { Log($"jump: {target} not reached in 90 s (scene {scene}, {Role()})"); _jumpIndex++; _jumpWaiting = false; }
            _next = Time.unscaledTime + 1f; return false;
        }

        // Dry run of the new slot's price tag: an inactive copy of a slot template at the new slot's pose, its tag
        // set from the draft, compared with the neighbour's tag — both as offsets from the item holder in the
        // customer's level frame (forward = towards the customer).
        void CheckTagPlacement(ShopLocation neighbour, Transform keeper)
        {
            try
            {
                var draft = (POGLib.Shops.Model.ShopFile)GetField("_draft");
                var sel = (string)GetField("_selected");
                var def = draft.Slots.First(x => draft.Mod + ":" + x.Id == sel);
                var templates = typeof(POGLib.Shops.ShopsPlugin).Assembly.GetType("POGLib.Shops.SlotTemplates");
                var template = templates.GetMethod("Get", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { POGLib.Shops.ShopsPlugin.Instance.Catalog.ModSlots.Keys.First() }) as ShopLocation;
                if (template == null || def.PriceTag == null) { Log("tagcheck: no template or no priceTag"); return; }
                var parent = new GameObject("DriverTagCheck"); parent.SetActive(false);
                var copy = UnityEngine.Object.Instantiate(template.gameObject, parent.transform);
                var anchor = POGLib.Shops.ShopScene.FindAnchor(def.Anchor);
                var rootRot = anchor.rotation * Quaternion.Euler(def.Rot[0], def.Rot[1], def.Rot[2]);
                copy.transform.SetPositionAndRotation(anchor.TransformPoint(new Vector3(def.Pos[0], def.Pos[1], def.Pos[2])), rootRot);
                var cs = copy.GetComponent<ShopLocation>();
                var tagRoot = TopChild(cs.m_priceTagText.transform, copy.transform);
                tagRoot.localPosition = new Vector3(def.PriceTag.Pos[0], def.PriceTag.Pos[1], def.PriceTag.Pos[2]);
                if (def.PriceTag.Rot != null) tagRoot.localEulerAngles = new Vector3(def.PriceTag.Rot[0], def.PriceTag.Rot[1], def.PriceTag.Rot[2]);
                var newHolder = cs.m_itemHolder != null ? cs.m_itemHolder : copy.transform;
                Vector3 mine = Quaternion.Inverse(Level(rootRot * Vector3.forward)) * (tagRoot.position - newHolder.position);

                var nh = neighbour.m_itemHolder != null ? neighbour.m_itemHolder : neighbour.transform;
                var ntag = TopChild(neighbour.m_priceTagText.transform, neighbour.transform);
                Vector3 theirs = Quaternion.Inverse(Level(keeper.forward)) * (ntag.position - nh.position);
                Log($"tagcheck: new slot tag {mine:F3} vs neighbour {theirs:F3} (customer frame, from holder); world dy {tagRoot.position.y - newHolder.position.y:F3} vs {ntag.position.y - nh.position.y:F3}");
                UnityEngine.Object.Destroy(parent);
            }
            catch (Exception ex) { Log($"tagcheck failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        static Transform TopChild(Transform t, Transform root)
        {
            var x = t;
            while (x.parent != null && x.parent != root) x = x.parent;
            return x.parent == root ? x : t;
        }

        static Quaternion Level(Vector3 f)
        {
            var flat = Vector3.ProjectOnPlane(f, Vector3.up);
            return Quaternion.LookRotation(flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward);
        }

        string _lastMenuLine;
        bool _pressedReady;

        // Between the slot click and the hub: log each new menu state once, answer a popup with its
        // positive button, press the lobby's Ready button. Logs what it did, so a new menu flow shows up.
        void NavigateMenus(string activeScene)
        {
            var menu = NetworkMenu.Instance;
            string state = menu?.m_currentState != null ? menu.m_currentState.GetIl2CppType().Name : "none";
            var popup = menu?.m_popup;
            bool popupOpen = popup != null && popup.m_window != null && popup.m_window.activeInHierarchy;
            string line = $"menu {state}; scene {activeScene}; {Role()}; popup {(popupOpen ? "'" + popup.m_messageText?.text + "'" : "closed")}";
            if (line != _lastMenuLine) { Log(line); _lastMenuLine = line; }

            if (popupOpen)
            {
                foreach (var b in new[] { popup.m_yesButton, popup.m_okeyButton })
                    if (b != null && b.gameObject.activeInHierarchy && b.m_button != null && b.m_button.interactable)
                    {
                        Log($"popup: pressing {b.name}");
                        b.m_button.onClick.Invoke();
                        return;
                    }
            }

            var lobby = menu?.m_currentState?.TryCast<NetworkMenuLobbyState>();
            if (!_pressedReady && lobby != null && lobby.m_readyButton != null && lobby.m_readyButton.gameObject.activeInHierarchy && lobby.m_readyButton.interactable)
            {
                _pressedReady = true;   // once: Ready may toggle
                Log("lobby: pressing Ready");
                lobby.m_readyButton.onClick.Invoke();
            }
        }

        // One line per seller and per slot: the raw material for the vanilla slot-ID table.
        void DumpShops()
        {
            var scene = SceneManager.GetActiveScene().name;
            var sellers = UnityEngine.Object.FindObjectsOfType<NpcSpawner>(true);
            Log($"dump {scene}: {sellers.Length} NpcSpawner");
            foreach (var s in sellers)
            {
                var slots = s.m_itemsForSale;
                var sp = s.transform.position; var sf = s.transform.forward;
                Log($"  seller {PathOf(s.transform)} flag='{s.m_npcFlag}' npc={(s.m_npc != null ? s.m_npc.name : "-")} slots={(slots != null ? slots.Length : -1)} " +
                    $"at ({sp.x:F2},{sp.y:F2},{sp.z:F2}) forward ({sf.x:F2},{sf.z:F2})");
                // R5 (ids that survive a game update): the seller's own order, where each slot stands and how it is turned.
                if (slots != null)
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var sl = slots[i];
                        if (sl == null) { Log($"    #{i} null"); continue; }
                        var p = sl.transform.position;
                        Log($"    #{i} {PathOf(sl.transform)} at ({p.x:F2},{p.y:F2},{p.z:F2}) yaw {sl.transform.eulerAngles.y:F0}");
                    }
            }
            var shops = UnityEngine.Object.FindObjectsOfType<ShopLocation>(true);
            Log($"dump {scene}: {shops.Length} ShopLocation");
            foreach (var sh in shops)
            {
                var sb = new StringBuilder();
                sb.Append("  slot ").Append(PathOf(sh.transform));
                // 2026-11-14 build: address references; the names are only known once loaded.
                sb.Append(" fixedRef=").Append(sh.HasNetworkPrefab ? (sh.m_networkPrefabRef?.AssetGUID ?? "?") : "-");
                sb.Append(" loaded=").Append(sh.m_loadedPrefab != null ? sh.m_loadedPrefab.name : "-");
                sb.Append(" poolRef=").Append(sh.HasLootTable ? (sh.m_lootTableRef?.AssetGUID ?? "?") : "-");
                sb.Append(" keeper=").Append(sh.m_shopKeeper != null ? sh.m_shopKeeper.name : "-");
                sb.Append(" faction=").Append(sh.m_shopFaction != null ? sh.m_shopFaction.Value.ToString() : "?");
                sb.Append(" price=").Append(sh.CurrentPrice);
                sb.Append(" shopItem=").Append(sh.CurrentShopItem != null ? sh.CurrentShopItem.name : "-");
                sb.Append(" tag='").Append(sh.m_priceTagText != null ? sh.m_priceTagText.text : "-").Append('\'');
                sb.Append(" item=").Append(sh.m_networkInstance != null ? sh.m_networkInstance.name : "-");
                sb.Append(" noSpawnIfSave=").Append(sh.m_disableSpawnIfSaveFileExist);
                var h = sh.m_itemHolder;
                if (h != null) sb.Append(" holder=").Append(h == sh.transform ? "self" : h.name).Append(' ').Append(h.localPosition.ToString("F2"));
                Log(sb.ToString());
            }
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (var x = t; x != null; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
