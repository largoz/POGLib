using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using Il2CppGame.Players;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using POGLib.Core;
using POGLib.Cosmetics.Model;
using POGLib.Items;
using UnityEngine;

namespace POGLib.Cosmetics
{
    /// <summary>
    /// Cosmetics domain plugin. A part goes where the game looks parts up by number (its part cache) and, when the wardrobe asks
    /// for its lists, into the answer. The game's own arrays are never changed: it draws random goblins from them, and those wear
    /// nothing of a mod. A part's lock type is always Unlocked: the game then keeps it out of its list of unlocked rewards, which is
    /// in the player's Steam Cloud (SaveLoad.ConfirmUnlockedCosmetics); what a player may wear is <see cref="UnlockList"/>.
    /// </summary>
    internal sealed class CosmeticsPlugin : IPOGPlugin
    {
        private sealed class Part
        {
            public CosmeticDefinition Def;
            public uint Id;
            public GlbModel Model;      // until it is built
            public CosmeticItem Item;
            public Mesh FirstPerson;    // when something is hidden and something is left
            public bool Failed;
        }

        public string Name => "Cosmetics";
        public string Version => "0.1.0";

        internal static CosmeticsPlugin Instance { get; private set; }

        private readonly List<Part> _parts = new List<Part>();
        private readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);
        private UnlockList _unlocks;
        private GameObject _holder;
        private CustomGoblinService _service;

        public void Build(PluginGraph graph)
        {
            Instance = this;
            Content.Cosmetics.Plugin = this;
            _unlocks = new UnlockList(Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "POGLib", "Cosmetics.json"), Warn);
        }

        internal bool Any => _parts.Count > 0;

        private static void Warn(string text) => MelonLogger.Warning("[POGLib.Cosmetics] " + text);
        private void WarnOnce(string text) { if (_warned.Add(text)) Warn(text); }

        private Part Find(string fullId) => _parts.FirstOrDefault(p => p.Def.FullId == fullId);

        // ---- the public calls ----

        public bool Register(CosmeticDefinition given)
        {
            if (given == null) { MelonLogger.Error("[POGLib.Cosmetics] Register(null)."); return false; }
            // A copy: what the mod does to its object afterwards changes nothing here.
            var d = new CosmeticDefinition { ModId = given.ModId, ContentId = given.ContentId, Slot = given.Slot, ModelFile = given.ModelFile, ThumbnailFile = given.ThumbnailFile, Locked = given.Locked, HiddenWhileLocked = given.HiddenWhileLocked };
            d.HiddenInFirstPerson.AddRange(given.HiddenInFirstPerson.Where(h => h != null).Distinct());
            bool Refuse(string why) { MelonLogger.Error($"[POGLib.Cosmetics] {d.FullId} is refused: {why}."); return false; }
            var wrong = CosmeticRules.CheckIds(d.ModId, d.ContentId);
            if (wrong != null) return Refuse(wrong);
            if (!Enum.IsDefined(typeof(CosmeticSlot), d.Slot)) return Refuse($"there is no slot {(int)d.Slot}");
            if (Find(d.FullId) != null) return Refuse("it is registered already");
            uint id = ContentRegistry.ComputeHash(d.ModId, d.ContentId);
            var same = _parts.FirstOrDefault(p => p.Id == id);
            if (id == 0 || same != null) return Refuse($"its number {id} is taken{(same != null ? " by " + same.Def.FullId : "")}; give it another ContentId");
            if (string.IsNullOrEmpty(d.ThumbnailFile) || !File.Exists(d.ThumbnailFile)) return Refuse($"no thumbnail file '{d.ThumbnailFile}'");
            try { if (!CosmeticRules.IsPng(d.ThumbnailFile)) return Refuse($"{Path.GetFileName(d.ThumbnailFile)} is not a PNG picture"); }
            catch (IOException ex) { return Refuse($"{Path.GetFileName(d.ThumbnailFile)}: {ex.Message}"); }
            if (string.IsNullOrEmpty(d.ModelFile) || !File.Exists(d.ModelFile)) return Refuse($"no model file '{d.ModelFile}'");
            GlbModel model;
            try { model = Glb.ReadFile(d.ModelFile); }
            catch (GlbException ex) { return Refuse($"{Path.GetFileName(d.ModelFile)}: {ex.Message}"); }
            catch (IOException ex) { return Refuse($"{Path.GetFileName(d.ModelFile)}: {ex.Message}"); }
            wrong = CosmeticRules.CheckModel(d.Slot, model) ?? CosmeticRules.CheckHidden(d.HiddenInFirstPerson, model);
            if (wrong != null) return Refuse($"{Path.GetFileName(d.ModelFile)}: {wrong}");
            if (model.Notes.Count > 0) Warn($"{d.FullId}: not read from {Path.GetFileName(d.ModelFile)}: {string.Join("; ", model.Notes)}.");
            _parts.Add(new Part { Def = d, Id = id, Model = model });
            MelonLogger.Msg($"[POGLib.Cosmetics] {d.FullId} ({d.Slot}, {(CosmeticRules.OnBody(model) ? "on the body" : "on the head")}): {model.VertexCount} vertices, " +
                            $"{model.Parts.Sum(p => p.Triangles.Count) / 3} triangles, materials {string.Join(", ", model.Parts.Select(p => p.MaterialName))}; number {id}{(d.Locked ? "; locked" : "")}.");
            return true;
        }

        public uint IdOf(string fullId) => Find(fullId)?.Id ?? 0u;

        public bool IsUnlocked(string fullId)
        {
            var p = Find(fullId);
            return p != null && (!p.Def.Locked || _unlocks.Contains(fullId));
        }

        public bool Unlock(string fullId)
        {
            var p = Find(fullId);
            if (p == null) { WarnOnce($"Unlock('{fullId}'): no such part."); return false; }
            if (!p.Def.Locked || !_unlocks.Add(fullId)) return false;
            MelonLogger.Msg($"[POGLib.Cosmetics] {fullId} is unlocked.");
            return true;
        }

        public string LookJson(GoblinLook look)
        {
            if (look == null) return null;
            var s = Service();
            if (s == null) return null;
            return CosmeticRules.LookJson(look, (slot, name) =>
            {
                if (name.Contains(':'))
                {
                    var p = Find(name);
                    if (p == null || p.Def.Slot != slot) { WarnOnce($"a look names {name} as {slot}: {(p == null ? "no such part is registered" : $"it is {p.Def.Slot}")}."); return 0; }
                    return p.Id;
                }
                var list = List(s, slot);
                for (int i = 0; list != null && i < list.Length; i++)
                    if (list[i] != null && list[i].name == name)
                    {
                        var c = list[i].GetComponent<CosmeticItem>();
                        if (c != null) return c.m_id;
                    }
                WarnOnce($"a look names {name} as {slot}: the game has no such part.");
                return 0;
            });
        }

        // ---- the game's side ----

        private static Il2CppReferenceArray<GameObject> List(CustomGoblinService s, CosmeticSlot slot) => slot switch
        {
            CosmeticSlot.Horns => s.m_horns,
            CosmeticSlot.Hair => s.m_hairs,
            CosmeticSlot.Chest => s.m_chests,
            CosmeticSlot.Legs => s.m_legs,
            CosmeticSlot.Feet => s.m_feet,
            CosmeticSlot.Accessory => s.m_accessories,
            _ => null,
        };

        private static CustomGoblinService.Category CategoryOf(CosmeticSlot slot) => slot switch
        {
            CosmeticSlot.Horns => CustomGoblinService.Category.Horns,
            CosmeticSlot.Hair => CustomGoblinService.Category.Hair,
            CosmeticSlot.Chest => CustomGoblinService.Category.Chest,
            CosmeticSlot.Legs => CustomGoblinService.Category.Legs,
            CosmeticSlot.Feet => CustomGoblinService.Category.Feet,
            CosmeticSlot.Accessory => CustomGoblinService.Category.Accessories,
            _ => CustomGoblinService.Category.None,
        };

        private CustomGoblinService Service()
        {
            if (_service == null)
                _service = Resources.FindObjectsOfTypeAll<CustomGoblinService>().FirstOrDefault(s => s != null && s.m_chests != null && s.m_chests.Length > 0);
            return _service;
        }

        /// <summary>A goblin has woken up and is about to be dressed: make what is not made yet, and let the game know every part.</summary>
        internal void GoblinWoke(EntityCosmetics app)
        {
            var s = app.m_goblinCosmetics;
            if (s == null || s.m_partCache == null) return;
            _service = s;
            GoblinRig rig = null;
            bool looked = false;
            foreach (var p in _parts)
            {
                if (p.Failed || (p.Item == null && p.Model == null)) continue;
                if (p.Item == null)
                {
                    if (!looked) { looked = true; rig = GoblinRig.From(app); }
                    if (rig == null || !Make(p, s, rig)) continue;   // no rig: not a goblin of the kind the clothes are made for; the next one may be
                }
                Put(s, p);
            }
        }

        /// <summary>The game has put a part on somebody: what the wearer is not to see of it goes from the first-person copy.</summary>
        internal void PutOn(EntityCosmetics app, CustomGoblinService.Part place, uint id)
        {
            var p = _parts.FirstOrDefault(x => x.Id == id);
            if (p == null || p.Def.HiddenInFirstPerson.Count == 0) return;
            var copies = app.m_currentPartsFP;
            if (copies == null || !copies.ContainsKey(place)) return;
            var go = copies[place];
            if (go == null) return;
            var skinned = go.GetComponent<SkinnedMeshRenderer>();
            if (p.FirstPerson != null && skinned != null) { skinned.sharedMesh = p.FirstPerson; return; }
            var filter = go.GetComponent<MeshFilter>();
            if (p.FirstPerson != null && filter != null) { filter.sharedMesh = p.FirstPerson; return; }
            var r = go.GetComponent<Renderer>();
            if (r != null) r.enabled = false;   // not SetActive: the game switches that on and off by itself
        }

        /// <summary>The game has filled its part cache anew: what is made goes back in.</summary>
        internal void CacheFilled(CustomGoblinService s)
        {
            if (s.m_partCache == null) return;
            foreach (var p in _parts) if (!p.Failed && p.Item != null) Put(s, p);
        }

        /// <summary>The wardrobe's list of a group, with the parts this player may wear.</summary>
        internal Il2CppReferenceArray<GameObject> Wardrobe(CustomGoblinService.Category category, Il2CppReferenceArray<GameObject> list)
        {
            if (list == null) return null;
            var ours = _parts.Where(p => !p.Failed && p.Item != null && CategoryOf(p.Def.Slot) == category
                                         && CosmeticRules.InWardrobe(p.Def.Locked, p.Def.HiddenWhileLocked, IsUnlocked(p.Def.FullId)).listed).ToList();
            if (ours.Count == 0) return list;
            var longer = new Il2CppReferenceArray<GameObject>(list.Length + ours.Count);
            for (int i = 0; i < list.Length; i++) longer[i] = list[i];
            for (int i = 0; i < ours.Count; i++) longer[list.Length + i] = ours[i].Item.gameObject;
            return longer;
        }

        /// <summary>
        /// Whether a wardrobe button with this picture is a part of ours the player has not unlocked: the game puts its own padlock on it.
        /// (The game thinks every part of ours is open; which part a button is, only its picture says.)
        /// </summary>
        internal bool Padlocked(Sprite picture)
        {
            if (picture == null) return false;
            var p = _parts.FirstOrDefault(x => x.Item != null && x.Item.m_thumbnail != null && x.Item.m_thumbnail.Pointer == picture.Pointer);
            return p != null && CosmeticRules.InWardrobe(p.Def.Locked, p.Def.HiddenWhileLocked, IsUnlocked(p.Def.FullId)).padlock;
        }

        private bool Make(Part p, CustomGoblinService s, GoblinRig rig)
        {
            var id = p.Def.FullId;
            try
            {
                var missing = CosmeticRules.MissingBones(p.Model, rig.Index.Keys);
                if (missing.Count > 0) { p.Failed = true; MelonLogger.Error($"[POGLib.Cosmetics] {id}: the goblin has no bone {string.Join(", ", missing)}."); return false; }
                if (_holder == null)
                {
                    _holder = new GameObject("POGLib_Cosmetics");
                    _holder.SetActive(false);   // what is in it is not drawn; a copy the game makes is
                    UnityEngine.Object.DontDestroyOnLoad(_holder);
                }
                Renderer like = null;
                var list = List(s, p.Def.Slot);
                for (int i = 0; list != null && i < list.Length && like == null; i++) if (list[i] != null) like = list[i].GetComponentInChildren<Renderer>(true);
                var go = CosmeticBuilder.Build(id, p.Model, rig, _holder.transform, like, p.Def.HiddenInFirstPerson, out p.FirstPerson);
                if (go == null) return false;
                var c = go.AddComponent<CosmeticItem>();
                c.m_id = p.Id;
                c.m_itemType = CosmeticRules.OnBody(p.Model) ? CustomGoblinService.ItemType.Stitched : CustomGoblinService.ItemType.Static;
                c.m_lockType = CustomGoblinService.LockType.Unlocked;
                c.m_thumbnail = AssetLoader.LoadPngAsSprite(p.Def.ThumbnailFile);
                p.Item = c;
                p.Model = null;
                MelonLogger.Msg($"[POGLib.Cosmetics] {id} is made ({c.m_itemType}, layer {LayerMask.LayerToName(go.layer)}).");
                return true;
            }
            catch (Exception ex)
            {
                p.Failed = true;
                MelonLogger.Error($"[POGLib.Cosmetics] {id} could not be made: {ex}");
                return false;
            }
        }

        private void Put(CustomGoblinService s, Part p)
        {
            if (s.m_partCache.TryGetValue(p.Id, out var there) && there != null)
            {
                if (there.Pointer == p.Item.Pointer) return;
                p.Failed = true;
                MelonLogger.Error($"[POGLib.Cosmetics] {p.Def.FullId}: its number {p.Id} is the game's own {there.name}; give it another ContentId.");
                return;
            }
            s.m_partCache[p.Id] = p.Item;
        }
    }
}
