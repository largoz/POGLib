// LAN probe of a dungeon merchant's stall. The same DLL runs on the host and on the client and reads only the game's own
// objects, so the two logs can be compared line by line ("[PROBE] <role> ..."). In the dungeon:
//   client: 3 shots of the merchant; tries a thing that costs more than the purse; buys the cheapest; watches; after the
//           merchant is dead takes a thing for nothing.
//   both:   in the hub, 20 s after a client is in it: every place with nothing on it, and whether its price tag shows; places
//           with a thing on them whose tag does not.
//   host:   gives the party Purse gold; when a thing is sold, buys one too; kills the merchant; reputation before and after.
using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppEntities;
using Il2CppGame.Gameplay;
using Il2CppGame.Spawning;
using MelonLoader;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: MelonInfo(typeof(POGLib.ClientProbe.Probe), "POGLibClientProbe", "0.1.0", "test")]
[assembly: MelonGame(null, null)]

namespace POGLib.ClientProbe
{
    public class Probe : MelonMod
    {
        const int Purse = 18, Purse2 = 60;
        float _next, _start;
        int _step;
        bool _done, _keeperSeen, _asked;
        string _last = "";
        InventoryItem _item; int _gold0, _price0, _slot0;
        NpcSpawner _stall; NPCEntity _npc;

        static string Role()
        {
            var nm = NetworkManager.Singleton;
            return nm == null || !nm.IsListening ? null : nm.IsHost ? "host" : "client";
        }
        void Log(string m) => LoggerInstance.Msg($"[PROBE] {Role() ?? "-"} {m}");
        void Shot(string n) => LoggerInstance.Msg("[LAN-SHOT] want noirclient_" + n);   // the laptop's watcher takes it
        static int Gold() => ResourceManager.Instance != null ? ResourceManager.Instance.PlayerGold : -1;
        static EntityPlayer Me() { foreach (var p in UnityEngine.Object.FindObjectsOfType<EntityPlayer>()) if (p != null && p.IsOwner) return p; return null; }
        static string N(Component c) => c == null ? "-" : c.name.Replace("(Clone)", "").Replace("Game.Spawning.ShopLocation ", "").Trim();

        bool Find()
        {
            if (_stall == null)
                foreach (var x in UnityEngine.Object.FindObjectsOfType<NpcSpawner>())
                    for (var t = x.transform; t != null; t = t.parent)
                        if (t.name.StartsWith("EntranceCave_Shopkeeper_")) _stall = x;
            if (_stall != null && _npc == null && !_keeperSeen)
                _npc = UnityEngine.Object.FindObjectsOfType<NPCEntity>().Where(n => Vector3.Distance(n.transform.position, _stall.transform.position) < 6f).OrderBy(n => Vector3.Distance(n.transform.position, _stall.transform.position)).FirstOrDefault();
            return _stall != null && _npc != null;
        }

        List<ShopLocation> Slots() => _stall?.m_itemsForSale == null ? new List<ShopLocation>() : _stall.m_itemsForSale.Where(s => s != null).ToList();

        static string Line(ShopLocation s, int i)
        {
            var it = s.CurrentShopItem;
            string tag = s.m_priceTagText == null ? "none" : $"'{s.m_priceTagText.text}' {(Shown(s) ? "shown" : "hidden")}";
            return $"#{i} {N(s)}: {N(it)} price {s.CurrentPrice} tag {tag} faction {s.m_shopFaction?.Value}" + (it == null ? "" : $" shopItem {it.IsShopItem} stealable {it.IsStealable}");
        }

        string Snapshot() => $"gold {Gold()} | " + string.Join(" | ", Slots().Select(Line));
        void Watch(string why = null) { string s = Snapshot(); if (why != null || s != _last) Log($"{why ?? "change"}: {s}"); _last = s; }

        string Npc() => _npc == null ? "gone" : $"{N(_npc)} tag='{_npc.m_npcName}' chest {_npc.CosmeticsData.ChestId} hair {_npc.CosmeticsData.HairId} worn {(_npc.Appearance?.m_currentPartsTP != null ? _npc.Appearance.m_currentPartsTP.Count : -1)} health {(_npc.Health != null ? _npc.Health.Health : -1):0.#}; draws {string.Join(", ", _npc.GetComponentsInChildren<Renderer>(false).Select(r => r.name))}";
        bool Dead() => _keeperSeen && (_npc == null || (_npc.Health != null && _npc.Health.Health <= 0));

        static string Reputation()
        {
            var g = GameMode_Goblin.Instance;
            if (g == null) return "none";
            var l = new List<string>();
            foreach (Faction f in Enum.GetValues(typeof(Faction))) { if (f == Faction.None) continue; try { l.Add($"{f} {g.GetReputation(f)}"); } catch { l.Add($"{f} -"); } }
            return string.Join(", ", l);
        }

        string InBag() { var me = Me(); try { return _item == null ? "gone" : $"{N(_item)} inInventory {_item.IsInInventory} mySlot {(me?.Inventory != null ? me.Inventory.FindItemSlotIndex(_item) : -2)} owner {_item.OwnerId}"; } catch (Exception e) { return "EXC " + e.Message; } }

        // Take a thing as a player does who presses E at it.
        void Take(string what, Func<(ShopLocation s, int i), bool> which, bool cheapest)
        {
            var all = Slots().Select((s, i) => (s, i)).Where(x => x.s.CurrentShopItem != null && which(x)).OrderBy(x => cheapest ? x.s.CurrentPrice : -x.s.CurrentPrice).ToList();
            _item = all.Count > 0 ? all[0].s.CurrentShopItem : null;
            var me = Me();
            if (_item == null || me == null) { Log($"{what}: nothing to take (player {(me != null)})"); _item = null; return; }
            _gold0 = Gold(); _price0 = all[0].s.CurrentPrice; _slot0 = all[0].i;
            bool can = false; try { can = _item.CanInteract(me); } catch { }
            Log($"{what}: {N(_item)} of #{_slot0}, price {_price0}, gold {_gold0}, the game lets me: {can}");
            _item.Interact(me);
        }

        void Took(string what) { Log($"{what} after: gold {_gold0} -> {Gold()} (price {_price0}); {InBag()}"); Watch(what + " slots"); }

        float _hubAt; bool _hubDone;
        static bool Shown(ShopLocation s)
        {
            if (s.m_priceTagText == null) return false;
            return s.m_priceTagText.gameObject.activeInHierarchy && s.m_priceTagText.transform.lossyScale.magnitude > 0.01f;
        }

        void Hub(string r)
        {
            if (r == "host" && UnityEngine.Object.FindObjectsOfType<EntityPlayer>().Length < 2) return;
            if (r == "client" && Me() == null) return;
            if (_hubAt == 0f) { _hubAt = Time.unscaledTime; return; }
            if (Time.unscaledTime < _hubAt + 20f) return;
            _hubDone = true;
            var all = UnityEngine.Object.FindObjectsOfType<ShopLocation>().Where(s => s != null && s.m_priceTagText != null).OrderBy(s => s.name).ToList();
            foreach (var s in all.Where(s => s.CurrentShopItem == null)) Log($"hub empty place: {s.transform.parent?.name}/{N(s)} tag '{s.m_priceTagText.text}' {(Shown(s) ? "shown" : "hidden")}");
            foreach (var s in all.Where(s => s.CurrentShopItem != null && !Shown(s))) Log($"hub place with {N(s.CurrentShopItem)} without a tag: {s.transform.parent?.name}/{N(s)}");
            Log($"hub: {all.Count} places, {all.Count(s => s.CurrentShopItem != null)} with a thing, {all.Count(s => s.CurrentShopItem != null && Shown(s))} of them with a tag");
        }

        public override void OnUpdate()
        {
            if (_done || Time.unscaledTime < _next) return;
            try
            {
                string r = Role();
                if (!_hubDone && r != null && SceneManager.GetActiveScene().name == "PitOfGoblin_Lobby") { Hub(r); _next = Time.unscaledTime + 1f; return; }
                if (r == null || SceneManager.GetActiveScene().name != "GameScene") { if (_start > 0) { Log("left the dungeon"); _done = true; } _next = Time.unscaledTime + 1f; return; }
                if (_start == 0f) { _start = Time.unscaledTime; Log("in the dungeon"); _next = Time.unscaledTime + (r == "host" ? 8f : 20f); return; }
                if (!Find() && !_keeperSeen) { if (Time.unscaledTime > _start + 90f) { Log($"no merchant (stall {(_stall != null)})"); _done = true; } _next = Time.unscaledTime + 2f; return; }
                _keeperSeen = true;
                if (Time.unscaledTime > _start + 170f) { Watch("end (time)"); _done = true; return; }
                if (r == "host") Host(); else Client();
            }
            catch (Exception ex) { Log($"step {_step} FAILED {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); _done = true; }
        }

        void Host()
        {
            switch (_step)
            {
                case 0: ResourceManager.Instance.SetGold(Purse); Log("npc " + Npc()); Log("reputation " + Reputation()); _step = 1; _next = Time.unscaledTime + 2f; return;
                case 1: Watch("slots"); Near(-0.7f); _gold0 = Gold(); _step = 2; _next = Time.unscaledTime + 0.5f; return;
                case 2:   // until the client bought something
                    Watch();
                    if (Gold() < Purse) { Log("the client bought something"); _step = 3; _next = Time.unscaledTime + 12f; return; }
                    if (Time.unscaledTime > _start + 100f) { Log("the client bought nothing in 100 s"); _step = 3; }
                    _next = Time.unscaledTime + 0.5f; return;
                case 3: Watch("before my purchase"); ResourceManager.Instance.SetGold(Purse2); _step = 4; _next = Time.unscaledTime + 2f; return;
                case 4: Take("buy", x => x.s.CurrentPrice > 0 && x.s.CurrentPrice <= Gold(), true); _step = 5; _next = Time.unscaledTime + 5f; return;
                case 5: Took("buy"); Log("reputation " + Reputation()); Log("killing the merchant"); _npc.Kill(); _step = 6; _next = Time.unscaledTime + 5f; return;
                case 6: Log("npc " + Npc()); Log("reputation after the death " + Reputation()); Watch("after the death"); _step = 7; _next = Time.unscaledTime + 1f; return;
                case 7: Watch(); _next = Time.unscaledTime + 0.5f; return;
            }
        }

        void Client()
        {
            switch (_step)
            {
                case 0: Log("npc " + Npc()); Log("reputation " + Reputation()); Watch("slots"); _step = 1; _next = Time.unscaledTime + 0.5f; return;
                case 1: case 2: case 3:
                    if (_npc != null)
                    {
                        HookCamera();
                        var f = _npc.transform.position + Vector3.up * 0.7f;
                        float d = _step == 2 ? 3.2f : 1.5f;
                        _ovPos = f + (_step == 3 ? (_npc.transform.forward + _npc.transform.right).normalized : _npc.transform.forward) * d + Vector3.up * (_step == 2 ? 0.9f : 0.2f);
                        _ovRot = Quaternion.LookRotation(f - _ovPos); _override = true;
                        if (!_asked) { _asked = true; _next = Time.unscaledTime + 1.5f; return; }
                        _asked = false;
                        Shot(new[] { "front", "wide", "side" }[_step - 1]);
                    }
                    _step++; _next = Time.unscaledTime + 5f; return;
                case 4: Near(0.7f); Watch("before"); Take("too dear", x => x.s.CurrentPrice > Gold(), false); _step = 5; _next = Time.unscaledTime + 4f; return;
                case 5: if (_item != null) Took("too dear"); _step = 6; _next = Time.unscaledTime + 1f; return;
                case 6: Take("buy", x => x.s.CurrentPrice > 0 && x.s.CurrentPrice <= Gold(), true); _step = 7; _next = Time.unscaledTime + 5f; return;
                case 7: Took("buy"); if (_npc != null) SetWide(); Shot("bought"); _step = 8; _next = Time.unscaledTime + 3f; return;
                case 8: Watch(); if (Dead()) { _step = 9; _next = Time.unscaledTime + 9f; return; } _next = Time.unscaledTime + 0.5f; return;
                case 9: Log("npc " + Npc()); Log("reputation after the death " + Reputation()); Watch("after the death"); Shot("dead"); _step = 10; _next = Time.unscaledTime + 4f; return;
                case 10: Take("steal", x => x.s.CurrentShopItem.IsStealable, false); _step = 11; _next = Time.unscaledTime + 5f; return;
                case 11: if (_item != null) Took("steal"); _override = false; Log("done"); _done = true; return;
            }
        }

        // To the customer's side of the counter: some mods (and the game's own tags) look at how near the player is.
        void Near(float side)
        {
            var me = Me(); if (me == null || _npc == null) { Log("near: no player"); return; }
            var to = _npc.transform.position + _npc.transform.forward * 3f + _npc.transform.right * side;
            me.Teleport(to + Vector3.up * 0.3f, Quaternion.LookRotation(-_npc.transform.forward));
            Log($"near: at {me.transform.position:F1}, {Vector3.Distance(me.transform.position, _npc.transform.position):0.0} m from the merchant");
        }

        void SetWide() { var f = _npc.transform.position + Vector3.up * 0.7f; _ovPos = f + _npc.transform.forward * 3.2f + Vector3.up * 0.9f; _ovRot = Quaternion.LookRotation(f - _ovPos); _override = true; }

        // The game's own camera, moved right before each render (RuntimeDriver has the same; a second camera does not show).
        static bool _override; static Vector3 _ovPos; static Quaternion _ovRot;
        static Il2CppSystem.Action<UnityEngine.Rendering.ScriptableRenderContext, Camera> _beginCam;
        static Il2CppSystem.Action<UnityEngine.Rendering.ScriptableRenderContext, Il2CppSystem.Collections.Generic.List<Camera>> _beginContext;
        static void Apply() { if (_override && Camera.main != null) Camera.main.transform.SetPositionAndRotation(_ovPos, _ovRot); }
        public override void OnLateUpdate() => Apply();
        static void HookCamera()
        {
            if (_beginCam != null) return;
            _beginCam = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<UnityEngine.Rendering.ScriptableRenderContext, Camera>>(new Action<UnityEngine.Rendering.ScriptableRenderContext, Camera>((c, cam) => Apply()));
            UnityEngine.Rendering.RenderPipelineManager.add_beginCameraRendering(_beginCam);
            _beginContext = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<UnityEngine.Rendering.ScriptableRenderContext, Il2CppSystem.Collections.Generic.List<Camera>>>(new Action<UnityEngine.Rendering.ScriptableRenderContext, Il2CppSystem.Collections.Generic.List<Camera>>((c, l) => Apply()));
            UnityEngine.Rendering.RenderPipelineManager.add_beginContextRendering(_beginContext);
        }
    }
}
