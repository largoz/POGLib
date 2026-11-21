using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using Il2CppGame.Gameplay;
using Il2CppGame.Menu;
using Il2CppGame.Networking;
using Il2CppGame.Spawning;
using MelonLoader;
using POGLib.Core;
using POGLib.Items;
using POGLib.Shops.Model;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace POGLib.Shops.Dev
{
    /// <summary>
    /// In-game shop tools (docs/design/shops.md §9). Turned on by the preference
    /// [POGLib_ShopTools] Enabled = true; then, while you host a game alone:
    ///   F7  aim mode: a HUD shows the slot/seller you look at; F8 places a new slot of your mod there,
    ///       G moves the selected new slot to where you look, arrows / PageUp / PageDown nudge it (Shift: fine),
    ///       [ and ] turn it, T switches nudging between the slot and its price tag.
    ///   F9  panel (opens the game's pause for a free cursor): slots in this scene with their chances and
    ///       "who changed what", your pool entries, items to add, scene jumps, round-timer freeze, Save.
    /// Save writes UserData/POGLib/Shops/&lt;mod&gt;.pogshop.json — copy it into your package as
    /// pogshop.json. Changes to vanilla slots apply on the next visit of the scene; new slots from the next
    /// hosted session (their network prefabs must exist before a session starts).
    /// </summary>
    internal static class ShopDevTools
    {
        private enum Mode { Off, Aim, Panel }

        private static MelonPreferences_Category _cat;
        private static MelonPreferences_Entry<bool> _enabled;
        private static MelonPreferences_Entry<string> _modPref;

        private static Mode _mode;
        private static ShopFile _draft;
        private static int _draftVersion;
        private static string _selected;           // full slot id
        private static bool _nudgeTag;
        private static string _status = "";
        private static bool _freeze;
        private static string _filter = "";
        private static List<string> _itemIds;
        private static string _pickItem;
        private static double _pickWeight = 10;
        private static int _listPage;
        private static bool _pausedByUs;
        private static readonly DevGui G = new DevGui();
        private static readonly Dictionary<string, GameObject> _previews = new Dictionary<string, GameObject>();

        // aim
        private static bool _aimHit;
        private static Vector3 _aimPoint;
        private static string _aimSlot, _aimSeller;

        private static ShopCatalog _previewCatalog;
        private static int _previewCatalogVersion = -1;

        public static void Init()
        {
            _cat = MelonPreferences.CreateCategory("POGLib_ShopTools", "POGLib shop tools");
            _enabled = _cat.CreateEntry("Enabled", false, "Shop tools (dev mode)",
                "F7 opens the in-game shop tools while you host a game alone. For mod authors.");
            _modPref = _cat.CreateEntry("Mod", "", "Mod being edited", "Mod id whose shop file the tools edit.");
        }

        private static bool CanEdit(out string why)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !nm.IsHost) { why = "host a game first (the tools change what the host spawns)"; return false; }
            if (ClientCount(nm) > 1) { why = "only while you play alone"; return false; }
            why = null; return true;
        }

        // The interop exposes ConnectedClientsIds as an interface without Count; the object behind it is a List.
        private static int ClientCount(NetworkManager nm)
        {
            var list = nm.ConnectedClientsIds?.TryCast<Il2CppSystem.Collections.Generic.List<ulong>>();
            return list != null ? list.Count : 1;
        }

        // ---------------------------------------------------------------- update

        public static void OnUpdate()
        {
            if (_enabled == null || !_enabled.Value) return;
            try
            {
                if (Input.GetKeyDown(KeyCode.F7)) SetMode(_mode == Mode.Off ? Mode.Aim : Mode.Off);
                if (_mode == Mode.Off) return;
                if (Input.GetKeyDown(KeyCode.F9)) SetMode(_mode == Mode.Panel ? Mode.Aim : Mode.Panel);
                if (_freeze) FreezeRoundTimer();
                if (_mode == Mode.Panel && _focus != null) TypeIntoFocus();
                if (_mode == Mode.Aim) { UpdateAim(); AimKeys(); }
                UpdatePreviews();
            }
            catch (Exception ex) { _status = $"error: {ex.GetType().Name}: {ex.Message}"; MelonLogger.Warning($"[POGLib] [ShopTools] {ex}"); }
        }

        private static void SetMode(Mode m)
        {
            if (m == _mode) return;
            if (_mode == Mode.Panel) ClosePause();
            if (m != Mode.Off && _draft == null) LoadDraft(string.IsNullOrEmpty(_modPref.Value) ? DefaultMod() : _modPref.Value);
            if (m == Mode.Panel) { OpenPause(); _itemIds = null; }
            if (m == Mode.Off) ClearPreviews();
            _mode = m;
        }

        private static string DefaultMod() =>
            CoreServices.Content?.AllDefinitions.Select(d => d.ModId).FirstOrDefault() ?? "MyMod";

        // The game's pause menu frees the cursor and stops the player from moving; we hide its canvas and
        // draw over it.
        private static void OpenPause()
        {
            var menu = NetworkMenu.Instance;
            if (menu == null) return;
            if (!menu.IsOpened) { menu.TogglePauseMenu(); _pausedByUs = true; }
            if (menu.m_canvas != null) menu.m_canvas.enabled = false;
        }

        private static void ClosePause()
        {
            var menu = NetworkMenu.Instance;
            if (menu == null) return;
            if (menu.m_canvas != null) menu.m_canvas.enabled = true;
            if (_pausedByUs && menu.IsOpened) menu.TogglePauseMenu();
            _pausedByUs = false;
        }

        private static void TypeIntoFocus()
        {
            string typed = Input.inputString;
            if (string.IsNullOrEmpty(typed)) return;
            bool done;
            if (_focus == "mod") { _modPrefEdit = DevGui.Type(_modPrefEdit, typed, out done); if (done) { _focus = null; LoadDraft(_modPrefEdit); } }
            else if (_focus == "filter") { _filter = DevGui.Type(_filter, typed, out done); if (done) _focus = null; }
        }

        private static void FreezeRoundTimer()
        {
            var round = GameMode_Round.Instance;
            if (round != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && round.m_gameStarted)
                round.m_startTime += Time.deltaTime;
        }

        // ---------------------------------------------------------------- aim

        private static void UpdateAim()
        {
            var cam = Camera.main;
            _aimHit = false; _aimSlot = _aimSeller = null;
            if (cam == null) return;
            if (Physics.Raycast(new Ray(cam.transform.position, cam.transform.forward), out RaycastHit hit, 8f))
            {
                _aimHit = true;
                _aimPoint = hit.point;
                var slot = ShopScene.SlotsInLoadedScenes().OrderBy(s => Vector3.Distance(s.transform.position, hit.point)).FirstOrDefault();
                if (slot != null && Vector3.Distance(slot.transform.position, hit.point) < 0.6f) _aimSlot = ShopScene.SlotId(slot);
                var seller = NearestSeller(hit.point);
                if (seller != null) _aimSeller = ShopScene.SellerId(seller);
            }
        }

        private static NpcSpawner NearestSeller(Vector3 p) =>
            ShopScene.SellersInLoadedScenes()
                .Select(s => (s, d: s.m_itemsForSale.Where(x => x != null).Select(x => Vector3.Distance(x.transform.position, p)).DefaultIfEmpty(999f).Min()))
                .Where(t => t.d < 8f).OrderBy(t => t.d).Select(t => t.s).FirstOrDefault();

        private static void AimKeys()
        {
            if (Input.GetKeyDown(KeyCode.F8)) PlaceNewSlot();
            if (Input.GetKeyDown(KeyCode.T)) _nudgeTag = !_nudgeTag;
            var slot = SelectedDraftSlot();
            if (slot == null) { if (_aimSlot != null && Input.GetKeyDown(KeyCode.Return)) _selected = _aimSlot; return; }
            if (Input.GetKeyDown(KeyCode.G)) MoveSlotTo(slot, _aimPoint);
            bool fine = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float step = fine ? 0.005f : 0.02f;
            var cam = Camera.main;
            Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 d = Vector3.zero;
            if (Input.GetKeyDown(KeyCode.UpArrow)) d += fwd * step;
            if (Input.GetKeyDown(KeyCode.DownArrow)) d -= fwd * step;
            if (Input.GetKeyDown(KeyCode.RightArrow)) d += right * step;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) d -= right * step;
            if (Input.GetKeyDown(KeyCode.PageUp)) d += Vector3.up * step;
            if (Input.GetKeyDown(KeyCode.PageDown)) d -= Vector3.up * step;
            float turn = (Input.GetKeyDown(KeyCode.RightBracket) ? 1 : 0) - (Input.GetKeyDown(KeyCode.LeftBracket) ? 1 : 0);
            if (d != Vector3.zero || turn != 0) Nudge(slot, d, turn * (fine ? 1f : 5f));
        }

        private static void PlaceNewSlot()
        {
            if (!CanEdit(out var why)) { _status = why; return; }
            if (!_aimHit) { _status = "look at a counter first"; return; }
            var seller = NearestSeller(_aimPoint);
            Transform anchor; string anchorId;
            if (seller != null)
            {
                // Anchor to a slot the game placed: a mod slot may not exist yet when this one is spawned.
                var near = seller.m_itemsForSale.Where(x => x != null && ShopIds.IsVanilla(ShopScene.SlotId(x)))
                                 .OrderBy(x => Vector3.Distance(x.transform.position, _aimPoint)).FirstOrDefault()
                           ?? seller.m_itemsForSale.Where(x => x != null).OrderBy(x => Vector3.Distance(x.transform.position, _aimPoint)).First();
                anchor = near.transform; anchorId = ShopScene.SlotId(near);
            }
            else
            {
                var slot = ShopScene.SlotsInLoadedScenes().Where(s => ShopIds.IsVanilla(ShopScene.SlotId(s)))
                                    .OrderBy(s => Vector3.Distance(s.transform.position, _aimPoint)).FirstOrDefault();
                if (slot == null) { _status = "no shop in this scene to place next to"; return; }
                anchor = slot.transform; anchorId = ShopScene.SlotId(slot);
            }
            var cam = Camera.main;
            Vector3 face = cam != null ? Vector3.ProjectOnPlane(cam.transform.position - _aimPoint, Vector3.up) : Vector3.forward;
            var world = Quaternion.LookRotation(face.sqrMagnitude > 0.001f ? face.normalized : Vector3.forward);
            var local = anchor.InverseTransformPoint(_aimPoint);
            var localRot = (Quaternion.Inverse(anchor.rotation) * world).eulerAngles;
            int n = 1; while (_draft.Slots.Any(s => s.Id == "slot_" + n)) n++;
            // Customers stand in front of the seller; the new slot faces the camera (a customer).
            var tag = TagLikeNeighbour(anchor.GetComponent<ShopLocation>(), seller != null ? seller.transform.forward : -anchor.forward);
            string item = _pickItem ?? CoreServices.Content.AllDefinitions.Where(x => x.ModId == _draft.Mod).Select(x => x.ModId + ":" + x.ContentId).FirstOrDefault() ?? ShopIds.Nothing;
            var def = new SlotDef
            {
                Id = "slot_" + n,
                Scene = SceneManager.GetActiveScene().name,
                Seller = seller != null ? ShopScene.SellerId(seller) : null,
                Anchor = anchorId,
                Pos = V(local), Rot = V(localRot),
                PriceTag = tag,
                Pool = { new PoolEntry { Item = Short(item), Weight = 10 } },
            };
            _draft.Slots.Add(def);
            _selected = _draft.Mod + ":" + def.Id;
            Changed($"new slot {_selected} next to {anchor.name}" + (seller != null ? $", sold by {ShopScene.SellerId(seller)}" : " (no seller nearby: set a faction in the file)"));
        }

        private static void MoveSlotTo(SlotDef s, Vector3 world)
        {
            var anchor = ShopScene.FindAnchor(string.IsNullOrEmpty(s.Anchor) ? s.Seller : s.Anchor);
            if (anchor == null) { _status = "anchor not in this scene"; return; }
            s.Pos = V(anchor.InverseTransformPoint(world));
            Changed($"moved {s.Id}");
        }

        private static void Nudge(SlotDef s, Vector3 worldDelta, float yawDeg)
        {
            var anchor = ShopScene.FindAnchor(string.IsNullOrEmpty(s.Anchor) ? s.Seller : s.Anchor);
            if (anchor == null) return;
            if (_nudgeTag)
            {
                var slotRot = anchor.rotation * Quaternion.Euler(Vec(s.Rot));
                s.PriceTag ??= new TagDef();
                var p = s.PriceTag.Pos != null ? Vec(s.PriceTag.Pos) : DefaultTagPos();
                p += Quaternion.Inverse(slotRot) * worldDelta;
                s.PriceTag.Pos = V(p);
                if (yawDeg != 0) s.PriceTag.Rot = V(Vec(s.PriceTag.Rot) + new Vector3(0, yawDeg, 0));
            }
            else
            {
                var world = anchor.TransformPoint(Vec(s.Pos)) + worldDelta;
                s.Pos = V(anchor.InverseTransformPoint(world));
                if (yawDeg != 0) s.Rot = V(Vec(s.Rot) + new Vector3(0, yawDeg, 0));
            }
            _draftVersion++;
        }

        /// <summary>
        /// The price tag placed like the neighbouring vanilla slot's, which sits on the counter (the template's own
        /// default can end up under it: 8 cm below, 2026-11-12). The neighbour's tag offset from its item holder is
        /// measured in the customer's frame (level, facing the seller's forward) and rebuilt in the new slot's frame
        /// (level, facing the camera), in the new slot root's local units. Measuring in the holders' own tilted
        /// frames put the tag 59 cm under the counter.
        /// </summary>
        internal static TagDef TagLikeNeighbour(ShopLocation neighbour, Vector3 customerForward)
        {
            if (neighbour == null || neighbour.m_priceTagText == null) return null;
            var nh = neighbour.m_itemHolder != null ? neighbour.m_itemHolder : neighbour.transform;
            var tagRoot = SlotTemplates.TopChildOf(neighbour.m_priceTagText.transform, neighbour.transform);
            var flat = Vector3.ProjectOnPlane(customerForward, Vector3.up);
            var yawN = Quaternion.LookRotation(flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward);
            Vector3 d = Quaternion.Inverse(yawN) * (tagRoot.position - nh.position);   // metres, customer frame
            Quaternion r = Quaternion.Inverse(yawN) * tagRoot.rotation;

            // The new slot root: rotation = level "facing the camera" frame; scale = the template's.
            var donor = SlotTemplates.Donor;
            Vector3 holderLocal = donor != null && donor.m_itemHolder != null ? donor.transform.InverseTransformPoint(donor.m_itemHolder.position) : Vector3.zero;
            Vector3 scale = donor != null ? donor.transform.localScale : Vector3.one;
            Vector3 local = holderLocal + new Vector3(d.x / NonZero(scale.x), d.y / NonZero(scale.y), d.z / NonZero(scale.z));
            return new TagDef { Pos = V(local), Rot = V(r.eulerAngles) };
        }

        private static float NonZero(float f) => Mathf.Abs(f) < 1e-5f ? 1f : f;

        private static Vector3 DefaultTagPos()
        {
            var donor = Resources.FindObjectsOfTypeAll<ShopLocation>().FirstOrDefault(x => x != null && x.m_priceTagText != null && !x.gameObject.scene.IsValid());
            if (donor == null) return new Vector3(0, 0.25f, 0.2f);
            return donor.transform.InverseTransformPoint(SlotTemplates.TopChildOf(donor.m_priceTagText.transform, donor.transform).position);
        }

        // ---------------------------------------------------------------- previews

        private static void UpdatePreviews()
        {
            string scene = SceneManager.GetActiveScene().name;
            var want = new HashSet<string>(_draft?.Slots.Where(s => s.Scene == scene).Select(s => s.Id) ?? Enumerable.Empty<string>());
            foreach (var gone in _previews.Keys.Where(k => !want.Contains(k)).ToList()) { if (_previews[gone] != null) UnityEngine.Object.Destroy(_previews[gone]); _previews.Remove(gone); }
            if (_draft == null) return;
            // A slot placed "after" another has no coordinates of its own: the game shows where it went.
            foreach (var s in _draft.Slots.Where(s => s.Scene == scene && string.IsNullOrEmpty(s.After)))
            {
                var anchor = ShopScene.FindAnchor(string.IsNullOrEmpty(s.Anchor) ? s.Seller : s.Anchor);
                if (anchor == null) continue;
                string topItem = s.Pool.OrderByDescending(p => p.Weight).Select(p => ShopIds.Qualify(p.Item, _draft.Mod)).FirstOrDefault();
                string key = s.Id;
                string tag = topItem + "|" + _draftVersion;
                if (!_previews.TryGetValue(key, out var go) || go == null || go.name != "ShopToolsPreview|" + tag)
                {
                    if (go != null) UnityEngine.Object.Destroy(go);
                    go = BuildPreview(topItem);
                    go.name = "ShopToolsPreview|" + tag;
                    _previews[key] = go;
                }
                go.transform.SetPositionAndRotation(anchor.TransformPoint(Vec(s.Pos)), anchor.rotation * Quaternion.Euler(Vec(s.Rot)));
                var tagMarker = go.transform.Find("Tag");
                if (tagMarker != null) tagMarker.localPosition = s.PriceTag?.Pos != null ? Vec(s.PriceTag.Pos) : DefaultTagPos();
            }
        }

        private static GameObject BuildPreview(string itemId)
        {
            var root = new GameObject("ShopToolsPreview");
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(root.transform, false);
            marker.transform.localScale = new Vector3(0.14f, 0.004f, 0.14f);
            var tag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(tag.GetComponent<Collider>());
            tag.name = "Tag";
            tag.transform.SetParent(root.transform, false);
            tag.transform.localScale = new Vector3(0.06f, 0.035f, 0.005f);
            var prefab = ShopScene.ResolveItemPrefab(itemId)?.GetComponent<InventoryItem>();
            if (prefab != null)
            {
                var ghost = ItemGhost.Create(prefab, "Item");
                ghost.transform.SetParent(root.transform, false);
                // As the game does it: the item's standing point lands on the holder (slot root here).
                var st = prefab.m_standingTransform;
                if (st != null)
                {
                    var inv = Matrix4x4.TRS(st.localPosition, st.localRotation, Vector3.one).inverse;
                    ghost.transform.localPosition = inv.GetColumn(3);
                    ghost.transform.localRotation = inv.rotation;
                }
            }
            return root;
        }

        private static void ClearPreviews()
        {
            foreach (var go in _previews.Values) if (go != null) UnityEngine.Object.Destroy(go);
            _previews.Clear();
        }

        // ---------------------------------------------------------------- draft

        private static void LoadDraft(string mod)
        {
            if (!ShopIds.IsValidModId(mod)) { _status = $"'{mod}' is not a valid mod id"; return; }
            _modPref.Value = mod;
            var existing = ShopsPlugin.Instance?.Files.FirstOrDefault(f => f.Mod == mod);
            if (existing != null)
            {
                var d = new List<Diagnostic>();
                _draft = ShopFileIO.Parse(ShopFileIO.Write(existing), existing.SourcePath, d) ?? NewFile(mod);
                _status = $"editing {mod} (from {existing.SourcePath})";
            }
            else { _draft = NewFile(mod); _status = $"editing {mod} (new file)"; }
            _draftVersion++;
            ClearPreviews();
        }

        private static ShopFile NewFile(string mod) => new ShopFile { Format = ShopFile.CurrentFormat, Mod = mod };

        private static void Changed(string what) { _draftVersion++; _status = what; }

        private static SlotDef SelectedDraftSlot()
        {
            if (_draft == null || _selected == null || !ShopIds.TrySplit(_selected, out var owner, out var local) || owner != _draft.Mod) return null;
            return _draft.Slots.FirstOrDefault(s => s.Id == local);
        }

        private static string Save()
        {
            var d = new List<Diagnostic>();
            if (!ShopFileIO.Validate(_draft, d)) return "not saved: " + string.Join("; ", d.Where(x => x.Severity == Severity.Error).Select(x => x.Message));
            Directory.CreateDirectory(ShopsPlugin.UserShopsDirectory);
            string path = Path.Combine(ShopsPlugin.UserShopsDirectory, _draft.Mod + ".pogshop.json");
            File.WriteAllText(path, ShopFileIO.Write(_draft));
            ShopsPlugin.Instance?.Reload();
            return $"saved {path} — ship it in your package as pogshop.json. Vanilla slots: next scene visit; new slots: next hosted session.";
        }

        /// <summary>The catalog as it would be with the draft in place of its mod's loaded files.</summary>
        private static ShopCatalog PreviewCatalog()
        {
            if (_previewCatalog != null && _previewCatalogVersion == _draftVersion) return _previewCatalog;
            var files = (ShopsPlugin.Instance?.Files ?? new List<ShopFile>()).Where(f => f.Mod != _draft.Mod).ToList();
            files.Add(_draft);
            _previewCatalog = ShopCatalog.Build(files, ShopsPlugin.Instance?.Options());
            _previewCatalogVersion = _draftVersion;
            return _previewCatalog;
        }

        private static ResolvedSlot ResolveSelected()
        {
            if (_selected == null) return null;
            var rec = ShopRuntime.Records.TryGetValue(_selected, out var r) ? r : null;
            IEnumerable<VanillaEntry> vanilla = rec?.Vanilla;
            if (vanilla == null) { var live = ShopScene.FindSlot(_selected); if (live != null) vanilla = ShopScene.VanillaEntriesOf(live); }
            return PreviewCatalog().Resolve(_selected, vanilla ?? Enumerable.Empty<VanillaEntry>());
        }

        private static void AddToSelected(string item, double weight)
        {
            var own = SelectedDraftSlot();
            if (own != null) own.Pool.Add(new PoolEntry { Item = Short(item), Weight = weight });
            else _draft.Add.Add(new AddOp { Slot = _selected, Item = Short(item), Weight = weight });
            Changed($"added {item} x{weight:0.##} to {_selected}");
        }

        private static void SetWeight(SlotEntry e, double w)
        {
            var own = SelectedDraftSlot();
            if (e.AddedBy == _draft.Mod)
            {
                if (own != null) foreach (var p in own.Pool.Where(p => ShopIds.Qualify(p.Item, _draft.Mod) == e.Item)) p.Weight = w;
                foreach (var a in _draft.Add.Where(a => ShopIds.Qualify(a.Slot, _draft.Mod) == _selected && ShopIds.Qualify(a.Item, _draft.Mod) == e.Item)) a.Weight = w;
                Changed($"{e.Item} weight {w:0.##}");
                return;
            }
            Patch(new PatchOp { Slot = _selected, Item = e.Item, Weight = w }, e);
        }

        private static void Remove(SlotEntry e)
        {
            var own = SelectedDraftSlot();
            if (e.AddedBy == _draft.Mod)
            {
                if (own != null) own.Pool.RemoveAll(p => ShopIds.Qualify(p.Item, _draft.Mod) == e.Item);
                _draft.Add.RemoveAll(a => ShopIds.Qualify(a.Slot, _draft.Mod) == _selected && ShopIds.Qualify(a.Item, _draft.Mod) == e.Item);
                Changed($"removed your {e.Item}");
                return;
            }
            Patch(new PatchOp { Slot = _selected, Item = e.Item, Remove = true }, e);
        }

        // A change to someone else's entry is a patch; declare the owners it touches in "requires".
        private static void Patch(PatchOp p, SlotEntry e)
        {
            _draft.Patch.RemoveAll(x => x.Slot == p.Slot && x.Item == p.Item);
            _draft.Patch.Add(p);
            foreach (var owner in new[] { ShopIds.OwnerOf(p.Slot), ShopIds.OwnerOf(p.Item) })
                if (owner != null && owner != ShopIds.Vanilla && owner != _draft.Mod && !_draft.Requires.Contains(owner)) _draft.Requires.Add(owner);
            Changed($"patch on {e.Item} (from {e.AddedBy})");
        }

        // ---------------------------------------------------------------- GUI

        public static void OnGUI()
        {
            if (_enabled == null || !_enabled.Value || _mode == Mode.Off) return;
            try
            {
                if (_mode == Mode.Aim) DrawAimHud(); else DrawPanel();
            }
            catch (Exception ex)
            {
                _status = $"gui error: {ex.Message}";
                if (_guiErrors.Add(ex.GetType().Name + ex.Message)) MelonLogger.Warning($"[POGLib] [ShopTools] drawing failed: {ex}");
            }
        }

        private static void DrawAimHud()
        {
            var r = new Rect(10, 10, 520, 178);
            G.Box(r, "Shop tools — aim (F7 close, F9 panel)");
            G.Begin(r.x + 8, r.y + 24, r.width - 16);
            G.Label($"Mod: {_draft?.Mod}   scene: {SceneManager.GetActiveScene().name}{(_freeze ? "   timer frozen" : "")}");
            G.Label(_aimHit ? $"Looking at {_aimPoint:F2}" + (_aimSlot != null ? $"  slot: {Tail(_aimSlot)} (Enter selects)" : "") : "Looking at nothing within 8 m");
            G.Label($"Seller nearby: {_aimSeller ?? "-"}");
            var sel = SelectedDraftSlot();
            G.Label(sel != null ? $"Selected: {sel.Id}  pos {Vec(sel.Pos):F3}  nudging the {(_nudgeTag ? "price tag" : "slot")} (T)" : $"Selected: {Tail(_selected) ?? "-"}");
            G.Label("F8 new slot here · G move selected here · arrows/PgUp/PgDn nudge · [ ] turn · Shift fine");
            G.Label(_status);
        }

        private static void DrawPanel()
        {
            float w = 940, h = 600;
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            G.Box(r, "Shop tools (F9 back to aiming, F7 close)");

            // left column
            G.Begin(r.x + 10, r.y + 26, 300);
            _modPrefEdit ??= _draft?.Mod;
            G.TextBox("mod", "Mod id", _modPrefEdit, ref _focus);
            if (G.Button("Load / start this mod's file")) LoadDraft(_modPrefEdit);
            G.Space();
            G.Label("Scene: " + SceneManager.GetActiveScene().name);
            int jump = G.Buttons("Lobby", "InBetween", "DarkBurg");
            if (jump >= 0) JumpTo(new[] { "PitOfGoblin_Lobby", "PitOfGoblin_InBetween", "PitOfGoblin_DarkBurg" }[jump]);
            if (G.Button(_freeze ? "Round timer: FROZEN (click to run)" : "Round timer: running (click to freeze)")) _freeze = !_freeze;
            G.Space();
            var slots = SlotsHere();
            int pages = Math.Max(1, (slots.Count + 13) / 14);
            _listPage = Math.Min(_listPage, pages - 1);
            int nav = G.LabelWithButtons($"Slots here: {slots.Count} (page {_listPage + 1}/{pages})", "<", ">");
            if (nav == 0) _listPage = Math.Max(0, _listPage - 1);
            if (nav == 1) _listPage = Math.Min(pages - 1, _listPage + 1);
            foreach (var (id, label) in slots.Skip(_listPage * 14).Take(14))
                if (G.Button((id == _selected ? "> " : "") + label)) _selected = id;

            // right column
            G.Begin(r.x + 330, r.y + 26, w - 340);
            if (_selected == null) { G.Label("Select a slot on the left, or aim at one (Enter) / place one (F8)."); }
            else DrawSlot();

            G.Begin(r.x + 330, r.y + h - 50, w - 340);
            if (G.Buttons("Save", "Reload all shop files", "Close") is int b && b >= 0)
            {
                if (b == 0) _status = Save();
                if (b == 1) { ShopsPlugin.Instance?.Reload(); _draftVersion++; _status = "reloaded"; }
                if (b == 2) SetMode(Mode.Off);
            }
            G.Label(_status);
        }

        private static string _modPrefEdit;
        private static string _focus;   // id of the text box being typed into
        private static readonly HashSet<string> _guiErrors = new HashSet<string>();

        private static void DrawSlot()
        {
            if (G.LabelWithButtons(Tail(_selected), "Copy") == 0)
            {
                MelonLogger.Msg($"[POGLib] [ShopTools] slot id: {_selected}");
                try { GUIUtility.systemCopyBuffer = _selected; _status = "slot id copied (also in the log)"; }
                catch { _status = "slot id written to the log"; }
            }
            var live = ShopScene.FindSlot(_selected);
            var rec = ShopRuntime.Records.TryGetValue(_selected, out var rr) ? rr : null;
            G.Label($"seller {(live?.m_shopKeeper != null ? ShopScene.SellerId(live.m_shopKeeper) : "-")}  faction {live?.m_shopFaction?.Value}  on sale now: {rec?.Picked ?? "(game's own choice)"}");
            if (rec != null) foreach (var n in rec.Notes.Take(3)) G.Label("  ! " + n);
            var res = ResolveSelected();
            G.Label($"Chances with your draft (total weight {res.TotalWeight:0.##}):");
            foreach (var e in res.Entries.Take(9))
            {
                int b = G.LabelWithButtons($"{100 * res.ChanceOf(e),5:0.0}%  {e.Item}  x{e.Weight:0.##}  ({e.AddedBy})", "-", "+", "x");
                if (b == 0) SetWeight(e, Math.Max(0, e.Weight - (e.Weight > 10 ? 5 : 1)));
                if (b == 1) SetWeight(e, e.Weight + (e.Weight >= 10 ? 5 : 1));
                if (b == 2) Remove(e);
            }
            G.Label("Who changed what:");
            foreach (var h in res.History.Reverse().Take(4)) G.Label("  " + h);
            G.Space();
            G.TextBox("filter", "Find item", _filter, ref _focus);
            var found = Items().Where(i => _filter.Length > 0 && i.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0).Take(4).ToList();
            foreach (var i in found) if (G.Button((i == _pickItem ? "> " : "") + i)) _pickItem = i;
            int a = G.LabelWithButtons($"Add {_pickItem ?? "(pick an item)"} weight {_pickWeight:0.##}", "-", "+", "Add");
            if (a == 0) _pickWeight = Math.Max(0.5, _pickWeight - (_pickWeight > 10 ? 5 : 1));
            if (a == 1) _pickWeight += _pickWeight >= 10 ? 5 : 1;
            if (a == 2 && _pickItem != null) AddToSelected(_pickItem, _pickWeight);
            if (SelectedDraftSlot() is SlotDef own && G.Button($"Delete new slot {own.Id}")) { _draft.Slots.Remove(own); _selected = null; Changed($"deleted {own.Id}"); }
        }

        private static List<(string id, string label)> SlotsHere()
        {
            var list = new List<(string, string)>();
            string scene = SceneManager.GetActiveScene().name;
            if (_draft != null)
                foreach (var s in _draft.Slots.Where(s => s.Scene == scene)) list.Add((_draft.Mod + ":" + s.Id, "* " + s.Id + " (new, yours)"));
            foreach (var s in ShopScene.SlotsInLoadedScenes())
            {
                string id = ShopScene.SlotId(s);
                if (list.Any(x => x.Item1 == id)) continue;
                list.Add((id, Tail(id)));
            }
            return list;
        }

        private static IEnumerable<string> Items()
        {
            if (_itemIds != null) return _itemIds;
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var d in CoreServices.Content.AllDefinitions) ids.Add(d.ModId + ":" + d.ContentId);
            foreach (var it in Resources.FindObjectsOfTypeAll<InventoryItem>())
                if (it != null && !it.gameObject.scene.IsValid() && it.GetComponent<NetworkObject>() != null) ids.Add(ShopIds.VanillaItem(it.gameObject.name));
            _itemIds = ids.ToList();
            return _itemIds;
        }

        private static void JumpTo(string scene)
        {
            if (!CanEdit(out var why)) { _status = why; return; }
            var handler = NetworkHandler.Singleton;
            if (handler == null) { _status = "no NetworkHandler"; return; }
            handler.LoadScene(scene, true, true, true, null);
            _status = $"loading {scene}…";
        }

        // ---------------------------------------------------------------- helpers

        private static string Short(string itemId) =>
            ShopIds.TrySplit(itemId, out var owner, out var local) && owner == _draft.Mod ? local : itemId;

        private static string Tail(string id)
        {
            if (id == null) return null;
            int slash = id.LastIndexOf('/');
            return slash >= 0 ? id.Substring(slash + 1).Replace("Game.Spawning.ShopLocation ", "") + " @" + id.Substring(0, slash).Split('/').Last() : id;
        }

        private static float[] V(Vector3 v) => new[] { (float)Math.Round(v.x, 4), (float)Math.Round(v.y, 4), (float)Math.Round(v.z, 4) };
        private static Vector3 Vec(float[] a) => a != null && a.Length == 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;
    }
}
