// SaveTest: ROADMAP 2.2, a save with a mod item loaded with the mod and without it. How the game saves:
// _research/SAVES_NATIVE.md (read from its code). The save file itself (Steam Cloud, plain JSON) is the oracle; these
// steps only make the game write it and report what the game restored.
//
//   savetest=buy     in the hub: buy ShopDemo's berry from its stand as the host, keep it, save the game the way the
//                    bucket does (GameMode_Goblin.SaveCurrentGame) and log the result
//   savetest=load    in the hub, once the storages have loaded: list every mod item in the scene and where it is
//   savetest=delete  in the main menu: delete the save slot (slot=N, default 2) through the game, then done
using System;
using System.Linq;
using Il2Cpp;
using Il2CppGame.Gameplay;
using MelonLoader;
using UnityEngine;

namespace POGLib.RuntimeDriver
{
    internal static class SaveTest
    {
        static int _step;
        static float _at;
        static string _result;

        static void Log(string m) => MelonLogger.Msg("[DRIVER] save: " + m);

        static Il2CppEntities.EntityPlayer LocalPlayer()
        {
            foreach (var p in UnityEngine.Object.FindObjectsOfType<Il2CppEntities.EntityPlayer>())
                if (p != null && p.IsOwner) return p;
            return null;
        }

        static int Gold() { try { return Il2Cpp.ResourceManager.Instance != null ? Il2Cpp.ResourceManager.Instance.PlayerGold : -1; } catch { return -1; } }

        static string Where(InventoryItem it)
        {
            var p = it.transform.position;
            string state;
            try { state = $"inInventory={it.IsInInventory} inBucket={it.IsInBucket} inShop={(it.ShopLocation != null)}"; }
            catch (Exception e) { state = "state? " + e.GetType().Name; }
            return $"'{it.m_itemID}' {state} at ({p.x:F1},{p.y:F1},{p.z:F1})";
        }

        static InventoryItem[] ModItems() =>
            UnityEngine.Object.FindObjectsOfType<InventoryItem>()
                .Where(i => i != null && !POGLib.Shops.ShopScene.IsTemplate(i.transform) && (i.m_itemID ?? "").Contains(':'))
                .ToArray();

        /// <summary>One step in the hub; true when the mode is done.</summary>
        internal static bool Tick(string mode)
        {
            float now = Time.unscaledTime;
            if (now < _at) return false;
            if (mode == "buy") return Buy(now);
            if (mode == "load") return Load(now);
            Log($"unknown mode '{mode}'");
            return true;
        }

        static bool Buy(float now)
        {
            switch (_step)
            {
                case 0:
                {
                    var me = LocalPlayer();
                    var slot = POGLib.Shops.ShopScene.FindSlot("ShopDemo:berry_stand");
                    var item = slot != null ? slot.CurrentShopItem : null;
                    Log($"slot {Il2Cpp.SaveLoad.CurrentSaveSlot}; gold {Gold()}; the stand holds {(item != null ? Where(item) : "nothing")}");
                    if (me == null || item == null) { Log("FAIL: no local player or nothing to buy"); return true; }
                    item.Interact(me);
                    _step = 1; _at = now + 5f; return false;
                }
                case 1:
                {
                    var mine = ModItems().Where(i => { try { return i.IsInInventory; } catch { return false; } }).ToArray();
                    Log($"after buying: gold {Gold()}; mod items in an inventory: {mine.Length}" + string.Concat(mine.Select(i => "\n    " + Where(i))));
                    Action<Il2CppGame.Platform.FileResult> done = r => _result = r.ToString();
                    GameMode_Goblin.Instance.SaveCurrentGame(done);
                    Log("SaveCurrentGame requested");
                    _step = 2; _at = now + 1f; _deadline = now + 20f; return false;
                }
                default:
                    if (_result != null) { Log($"saved: {_result}"); return true; }
                    if (now > _deadline) { Log("FAIL: no save result in 20 s"); return true; }
                    _at = now + 1f; return false;
            }
        }

        static float _deadline;

        // The same question asked of the game's own items, so a missing mod item can be told from a storage the game
        // did not restore at all (2026-11-23: the bucket's storage is disabled in the hub's lobby state).
        static readonly string[] Controls = { "Item_Torch_Unlit", "Item_Weapon_Spear" };

        static bool Load(float now)
        {
            if (_step == 0) { Log($"slot {Il2Cpp.SaveLoad.CurrentSaveSlot}; looking at 20 s and at 60 s"); _step = 1; _at = now + 20f; return false; }
            var items = ModItems();
            Log($"[{_step}] mod items in the scene: {items.Length}" + string.Concat(items.Select(i => "\n    " + Where(i))));
            var all = UnityEngine.Object.FindObjectsOfType<InventoryItem>().Where(i => i != null && !POGLib.Shops.ShopScene.IsTemplate(i.transform)).ToArray();
            var inBucket = all.Where(i => { try { return i.IsInBucket; } catch { return false; } }).ToArray();
            Log($"[{_step}] items in the bucket: {inBucket.Length}" + string.Concat(inBucket.Select(i => $"\n    {i.name} " + Where(i))));
            foreach (var c in Controls)
            {
                var found = all.Where(i => i.name.StartsWith(c)).ToArray();
                Log($"[{_step}] control {c}: {found.Length}" + string.Concat(found.Select(i => $"\n    {i.name} " + Where(i))));
            }
            foreach (var v in UnityEngine.Object.FindObjectsOfType<Il2Cpp.StorageVessel>(true))
                Log($"[{_step}] storage vessel {v.name} active={v.isActiveAndEnabled}");
            if (_step == 1) { _step = 2; _at = now + 40f; return false; }
            return true;
        }

        /// <summary>At the main menu: delete the slot through the game (Steam Cloud follows); true when done.</summary>
        internal static bool Delete(int slot)
        {
            float now = Time.unscaledTime;
            if (now < _at) return false;
            if (_step == 0)
            {
                Action<Il2Cpp.SaveLoad.FileValidation> done = v => _result = v.ToString();
                Il2Cpp.SaveLoad.DeleteSaveSlot(slot, done);
                Log($"DeleteSaveSlot({slot}) requested");
                _step = 1; _at = now + 1f; _deadline = now + 20f; return false;
            }
            if (_result != null) { Log($"deleted slot {slot}: {_result}"); return true; }
            if (now > _deadline) { Log($"FAIL: no answer to deleting slot {slot} in 20 s"); return true; }
            _at = now + 1f; return false;
        }
    }
}
