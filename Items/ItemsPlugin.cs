using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppEntities;
using MelonLoader;
using POGLib.Core;
using Unity.Netcode;
using UnityEngine;

namespace POGLib.Items
{
    /// <summary>Use context. owner runs host-authoritative.</summary>
    public sealed class UseContext
    {
        internal UseContext() { }

        public bool IsHost { get; internal set; }
        public HostBridge Bridge { get; internal set; }
    }

    /// <summary>Use handler. Return false if the effect did not apply (the use is not consumed).</summary>
    public delegate bool ModItemUseHandler(InventoryItem item, Entity owner, UseContext ctx);

    /// <summary>
    /// Items domain plugin. Registers definitions/handlers, builds templates on scene load, and
    /// runs the host-side use flow. Network prefab registration and loot injection are done by
    /// the core (NetworkRegistrar / LootInjector) at the correct lifecycle moments.
    /// </summary>
    internal sealed class ItemsPlugin : IPOGPlugin
    {
        public string Name => "Items";
        public string Version => "0.1.0";

        internal static ItemsPlugin Instance { get; private set; }

        private readonly Dictionary<string, ModItemUseHandler> _handlers = new();
        private readonly List<ModItemDefinition> _defs = new();
        private readonly HashSet<string> _built = new();

        /// <summary>Whether a mod item "Mod:Item" was registered with Content.Items (built or not).</summary>
        public bool IsRegistered(string fullId)
        {
            foreach (var d in _defs)
                if (ContentRegistry.FullId(d.ModId, d.ContentId) == fullId) return true;
            return false;
        }

        /// <summary>Whether its template has been built (and can be registered and sold).</summary>
        public bool IsBuilt(string fullId) => _built.Contains(fullId);

        /// <summary>The item's name term, which is also its discovery id (m_itemTooltipID).</summary>
        internal static string NameKeyOf(ModItemDefinition def)
            => !string.IsNullOrEmpty(def.TooltipKey) ? def.TooltipKey : $"MOD/{def.ModId}/{def.ContentId}";

        internal static string DescriptionKeyOf(ModItemDefinition def)
            => !string.IsNullOrEmpty(def.TooltipDescriptionKey) ? def.TooltipDescriptionKey : $"MOD/{def.ModId}/{def.ContentId}_Desc";

        internal static Il2CppGame.Journal.ItemGroup JournalGroupOf(ModItemDefinition def)
        {
            if (def.JournalGroup.HasValue) return def.JournalGroup.Value;
            if ((def.ItemType & ItemType.Food) != 0) return Il2CppGame.Journal.ItemGroup.Food;
            if ((def.ItemType & ItemType.Treasure) != 0) return Il2CppGame.Journal.ItemGroup.Treasure;
            return Il2CppGame.Journal.ItemGroup.Other;
        }

        /// <summary>Registered definitions, for the journal (which can be built before any template).</summary>
        internal IReadOnlyList<ModItemDefinition> Definitions => _defs;

        // One sprite per item, shared by the item and its journal entry. Not cached while unresolved: a game
        // sprite named by IconSpriteName may only be loaded later.
        private readonly Dictionary<string, Sprite> _icons = new();

        internal Sprite IconFor(ModItemDefinition def)
        {
            string full = ContentRegistry.FullId(def.ModId, def.ContentId);
            if (_icons.TryGetValue(full, out var cached) && cached != null) return cached;
            Sprite icon = null;
            if (def.Icon != null) icon = def.Icon;
            else if (!string.IsNullOrEmpty(def.IconSpriteName)) icon = GameAssets.FindSprite(def.IconSpriteName);
            else if (!string.IsNullOrEmpty(def.IconFile))
            {
                try { icon = AssetLoader.LoadPngAsSprite(def.IconFile); }
                catch (Exception ex) { MelonLogger.Warning($"[POGLib.Items] {def.ContentId}: icon file '{def.IconFile}': {ex.Message}"); }
                if (icon != null)
                    MelonLogger.Msg($"[POGLib.Items] {def.ContentId}: icon from '{System.IO.Path.GetFileName(def.IconFile)}' ({icon.texture.width}x{icon.texture.height}).");
            }
            if (icon != null) _icons[full] = icon;
            return icon;
        }

        /// <summary>Resolve a definition by hash (used by ModItemHandle).</summary>
        internal static ModItemDefinition ResolveDefinition(uint hash)
            => CoreServices.Content.GetDefinitionByHash(hash) as ModItemDefinition;

        // ---- Public API (via Content.Items) ----

        public void RegisterUseHandler(string handlerId, ModItemUseHandler handler)
        {
            if (string.IsNullOrEmpty(handlerId) || handler == null) return;
            _handlers[handlerId] = handler;
            MelonLogger.Msg($"[POGLib.Items] Use handler: {handlerId}");
        }

        public void RegisterItem(ModItemDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.ModId) || string.IsNullOrEmpty(def.ContentId))
            {
                MelonLogger.Error("[POGLib.Items] Invalid ModItemDefinition.");
                return;
            }
            if (!CoreServices.Content.RegisterDefinition(def)) return;
            _defs.Add(def);
            // If a scene is already loaded (late registration), try to build immediately.
            TryBuild(def);
        }

        // ---- Lifecycle ----

        public void Build(PluginGraph graph)
        {
            Instance = this;
            Content.Items.Plugin = this;

            // On every scene load: build pending templates; host-side, inject loot into loaded tables.
            graph.AddSystem(PluginStage.SceneLoaded, Name, () =>
            {
                BuildPending();

                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsServer)
                    CoreServices.Loot.InjectIntoLoadedTables();
            });
            // 2026-11-14 build: the shells arrive with the game's prefab list, right before the session starts
            // (after the last menu scene load), so build there too or nothing is ready to register.
            graph.AddSystem(PluginStage.GamePrefabsLoaded, Name, BuildPending);
        }

        private void BuildPending()
        {
            foreach (var def in _defs) TryBuild(def);

            // Link "leaves behind" references once every template exists — the target may well be
            // registered after the item that points at it.
            LinkConsumeSpawns();

            // Retry any queued visual probes (prefabs load progressively across scenes).
            Content.Diagnostics.RetryPending();
        }

        private void TryBuild(ModItemDefinition def)
        {
            string full = ContentRegistry.FullId(def.ModId, def.ContentId);
            if (_built.Contains(full)) return;

            // A big item on the default shell would keep the diamond's one-hand grips and size (D7): it gets the
            // big-item shell unless the definition chose a shell itself.
            var shellKind = def.ShellKind;
            if (def.Big && shellKind == ItemShellKind.Diamond) shellKind = ItemShellKind.BigItem;
            var template = CoreServices.Shells.BuildItemTemplate(shellKind, def.ModId, def.ContentId);
            if (template == null) return; // shell not in memory yet — retry on the next scene load

            ApplyIdentity(template, def);
            ApplyVisual(template, def);
            ApplyCapabilities(template, def);
            StandingPoints.Ensure(template, def.StandingPoint, def.StandingRotation);
            template.gameObject.AddComponent<ModItemHandle>();

            // Request a natural drop (executed host-side by LootInjector).
            if (def.Loot != null)
                CoreServices.Loot.AddRequest(template, def.Loot.Category, def.Loot.TableNames, def.Loot.Weight);

            _built.Add(full);
        }

        private static void ApplyIdentity(InventoryItem item, ModItemDefinition def)
        {
            item.m_itemID = ContentRegistry.FullId(def.ModId, def.ContentId);

            // Classification — without these the item mis-routes to the wrong slot and has no
            // minimap marker (ITEM_SYSTEM.md §2). Fields are set directly (interop exposes them).
            item.m_itemType = def.ItemType;
            item.m_mapIconType = def.MapIcon;

            // Economy / satiety (§4). GoldValue = loot budget, ShopCost = price (decoupled).
            item.m_goldValue = def.GoldValue;
            item.m_foodValue = def.FoodValue;
            item.m_shopCost = def.ShopCost;

            item.m_doConsume = def.Consumable;
            item.m_isConsumed = false;
            item.m_spawnAmount = 0;
            item.m_spawnChance = 0f;

            // Handling / "weight" (ITEM_ANATOMY.md §1.4) — plain fields, statically sound.
            item.m_isBigItem = def.Big;
            item.m_staminaPenaltyOnPickup = def.PickupStaminaPenalty;
            item.m_movementFactorWhenHeld = def.MovementFactorWhenHeld;

            // Icon: direct override wins, then a game sprite by name, then a PNG file of the mod's own.
            // None of them: the shell's icon stays (D10: the glow berry showed the diamond in the client's toast).
            var icon = Instance?.IconFor(def);
            if (icon != null) item.m_itemIcon = icon;
            else if (def.Visual?.Kind == ItemVisualKind.ObjFile)
                MelonLogger.Warning($"[POGLib.Items] {def.ContentId}: a model of its own but no icon (Icon, IconSpriteName or IconFile): it shows the shell's icon.");

            // Journal: the tab and whether it is listed (the entry itself is added by JournalEntries).
            item.m_journalGroup = JournalGroupOf(def);
            item.m_showInJournal = def.ShowInJournal;

            // Tooltip/name. Registering the translation is only half of it — the item must also POINT
            // at our term, otherwise it keeps the shell's ids and shows "Diamond / Best rock in world".
            // The ids live on NetworkInteractableBase (InventoryItem's base): m_itemTooltipID and
            // m_itemTooltipDescriptionID, which UIItemTooltip reads via GetTooltipTitleId/DescriptionId.
            string nameKey = NameKeyOf(def);
            string descKey = DescriptionKeyOf(def);

            // Always our own keys, with or without the texts: the game uses m_itemTooltipID as the item's discovery id
            // too (InventoryItem.IsDiscovered, build 26668562), so a kept shell key made the item "Diamond" and
            // discovered together with the diamond. No DisplayName: the content id is the name; no description:
            // the game's own placeholder.
            ModLocalization.Register(nameKey, string.IsNullOrEmpty(def.DisplayName) ? def.ContentId : def.DisplayName);
            item.m_itemTooltipID = nameKey;
            if (!string.IsNullOrEmpty(def.DisplayDescription)) ModLocalization.Register(descKey, def.DisplayDescription);
            item.m_itemTooltipDescriptionID = descKey;
        }

        /// <summary>
        /// Attach declared capability components (EXPERIMENTAL, v0.2 — see ItemCapability). Each attach
        /// is guarded so one bad capability can't abort the whole item build. Runs on every peer at the
        /// same build point, so the resulting component set stays identical (ForceSamePrefabs).
        /// </summary>
        private static void ApplyCapabilities(InventoryItem item, ModItemDefinition def)
        {
            // A use handler needs something able to receive the game's primary action. The vanilla
            // UseItem input ends at ActiveItem_ReadyItem, and bare shells carry no ActiveItem_* at
            // all — so give the item the game's own usable base unless the pack added one itself.
            if (!string.IsNullOrEmpty(def.UseHandlerId) && !HasUsableComponent(def))
            {
                try
                {
                    new UsableCapability().Attach(item);
                    MelonLogger.Msg($"[POGLib.Items] {def.ContentId}: made usable (ActiveItem_ReadyItem added).");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[POGLib.Items] {def.ContentId}: could not make usable: {ex.Message}");
                }
            }

            if (def.Capabilities == null || def.Capabilities.Count == 0) return;
            foreach (var cap in def.Capabilities)
            {
                if (cap == null) continue;
                try
                {
                    cap.Attach(item);
                    MelonLogger.Msg($"[POGLib.Items] Capability '{cap.Name}' attached to {def.ContentId} (EXPERIMENTAL).");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[POGLib.Items] Capability '{cap.Name}' failed on {def.ContentId}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Wire every definition's <c>SpawnOnConsume</c> to the target template's NetworkObject, using
        /// the game's own <c>m_spawnOnDestroy</c> so the replacement is spawned and replicated by the
        /// game itself. Runs after the build pass because the target may be registered later.
        /// </summary>
        private void LinkConsumeSpawns()
        {
            foreach (var def in _defs)
            {
                if (string.IsNullOrEmpty(def.SpawnOnConsume)) continue;

                var source = ResolveTemplate(def.ModId, def.ContentId);
                if (source == null) continue;                       // not built yet — retry next scene
                if (source.m_spawnOnDestroy != null) continue;      // already linked

                // "contentId" means "in my own mod"; "modId:contentId" points anywhere.
                string targetMod = def.ModId, targetContent = def.SpawnOnConsume;
                int sep = def.SpawnOnConsume.IndexOf(':');
                if (sep > 0)
                {
                    targetMod = def.SpawnOnConsume.Substring(0, sep);
                    targetContent = def.SpawnOnConsume.Substring(sep + 1);
                }

                uint targetHash = ContentRegistry.ComputeHash(targetMod, targetContent);
                var targetObj = CoreServices.Content.GetPrefabByHash(targetHash);
                if (targetObj == null) continue;                    // target not built yet

                source.m_spawnOnDestroy = targetObj;
                // ShellFactory zeroes these to kill inherited spawn chains; a deliberate link needs
                // them back, or the game has nothing to spawn.
                source.m_spawnAmount = 1;
                source.m_spawnChance = 1f;
                MelonLogger.Msg($"[POGLib.Items] {def.ContentId} leaves behind {targetMod}:{targetContent} when used up.");
            }
        }

        /// <summary>The built InventoryItem template for a content id, or null if not built yet.</summary>
        private static InventoryItem ResolveTemplate(string modId, string contentId)
        {
            var netObj = CoreServices.Content.GetPrefabByHash(ContentRegistry.ComputeHash(modId, contentId));
            return netObj != null ? netObj.GetComponent<InventoryItem>() : null;
        }

        /// <summary>Whether the pack already declared a capability that makes the item usable.</summary>
        private static bool HasUsableComponent(ModItemDefinition def)
        {
            if (def.Capabilities == null) return false;
            foreach (var cap in def.Capabilities)
                if (cap is UsableCapability || cap is EatCapability
                    || cap is MeleeWeaponCapability || cap is ThrowableCapability) return true;
            return false;
        }

        private static void ApplyVisual(InventoryItem item, ModItemDefinition def)
        {
            var go = item.gameObject;
            try
            {
                if (def.Visual != null)
                {
                    switch (def.Visual.Kind)
                    {
                        case ItemVisualKind.GameMesh:
                            var source = GameAssets.FindItemPrefab(def.Visual.SourcePrefabName);
                            if (source != null)
                            {
                                // Swap the mesh on the shell's own renderers so the world view, the
                                // in-hand view and inventory hiding all keep working (see ReplaceMeshes).
                                ItemVisuals.ReplaceMeshes(go, source.gameObject, def.Visual.ChildName);
                                if (def.Visual.StripSpeakingStone)
                                {
                                    ItemVisuals.StripVoiceComponents(go);
                                    ItemVisuals.HideSpeakingStoneVfx(go);
                                }
                            }
                            else MelonLogger.Warning($"[POGLib.Items] Visual source not found: {def.Visual.SourcePrefabName}");
                            break;

                        case ItemVisualKind.ObjFile:
                            // A model of our own, built from plain geometry data at runtime. Same in-place swap as the
                            // reskin path: the mesh goes into the shell's own renderers, which sit under PickupItem /
                            // HeldItem, so the game keeps showing, hiding and layering it (docs/design/item-chain.md).
                            var model = ObjMesh.Load(def.Visual.Path);
                            if (model != null)
                            {
                                ItemVisuals.ReplaceMeshes(go, model.Mesh, model.Materials);
                                // The hitbox follows the model; otherwise the item keeps the shell's collider.
                                ItemVisuals.ApplyColliders(go, model.CollisionMeshes, model.Mesh);
                            }
                            else MelonLogger.Warning($"[POGLib.Items] {def.ContentId}: OBJ visual failed, keeping the shell mesh.");
                            break;

                        case ItemVisualKind.Png when def.Icon == null:
                            var icon = AssetLoader.LoadPngAsSprite(def.Visual.Path);
                            if (icon != null) item.m_itemIcon = icon;
                            break;

                        case ItemVisualKind.BundlePrefab:
                            ApplyBundleVisual(item, def);
                            break;
                    }
                }

                if (def.Tint.HasValue)
                    ItemVisuals.ApplyTint(go, def.Tint.Value, def.Visual?.ChildName);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[POGLib.Items] Visual failed for {def.ContentId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Load a custom mesh/prefab from an AssetBundle and use it as the item's visual. The prefab
        /// brings its own engine-side effects (particles, lights, trails, animation); custom C#
        /// scripts cannot survive IL2CPP and are dropped by Unity on load. Bundles must be built with
        /// the game's Unity/HDRP version — mismatched shaders are repaired onto HDRP/Lit.
        /// RUNTIME-TODO (Milestone 0.2): the bundle round-trip is not proven in-game yet.
        /// </summary>
        private static void ApplyBundleVisual(InventoryItem item, ModItemDefinition def)
        {
            var v = def.Visual;
            if (string.IsNullOrEmpty(v.Path) || string.IsNullOrEmpty(v.AssetName))
            {
                MelonLogger.Warning($"[POGLib.Items] Bundle visual for {def.ContentId} needs both a bundle path and an asset name.");
                return;
            }

            var bundle = CoreServices.Assets.LoadBundle(v.Path);
            if (bundle == null) return;

            var prefab = CoreServices.Assets.LoadAsset<GameObject>(bundle, v.AssetName);
            if (prefab == null)
            {
                MelonLogger.Error($"[POGLib.Items] Asset '{v.AssetName}' not found in bundle '{v.Path}'.");
                return;
            }

            var visual = ItemVisuals.AttachBundlePrefab(item.gameObject, prefab, v.LocalOffset, v.LocalEuler, v.LocalScale);
            if (visual == null) return;

            if (v.RepairShaders) ItemVisuals.RepairBundleShaders(visual);
            MelonLogger.Msg($"[POGLib.Items] Custom mesh '{v.AssetName}' applied to {def.ContentId} (EXPERIMENTAL).");
        }

        // ---- Use flow (host-authoritative) ----

        /// <summary>Try to use an item. Called from the Interact Harmony patch.</summary>
        public void TryUseItem(InventoryItem item, Entity owner)
        {
            if (item == null || owner == null) return;

            var netObj = item.GetComponent<NetworkObject>();
            if (netObj == null) return;
            var def = ResolveDefinition(netObj.GlobalObjectIdHash);
            if (def == null) return;

            // From here on the item IS ours, so every early exit is a reason the player's click did
            // nothing — say which one. A silent no-op here once hid a broken use flow completely.
            var handle = item.GetComponent<ModItemHandle>();
            if (handle != null && handle.IsConsumed)
            {
                MelonLogger.Msg($"[POGLib.Items] {def.ContentId}: already consumed.");
                return;
            }

            if (string.IsNullOrEmpty(def.UseHandlerId))
            {
                MelonLogger.Msg($"[POGLib.Items] {def.ContentId}: no UseHandlerId set — nothing to run.");
                return;
            }
            if (!_handlers.TryGetValue(def.UseHandlerId, out var handler))
            {
                MelonLogger.Warning($"[POGLib.Items] {def.ContentId}: use handler " +
                                    $"'{def.UseHandlerId}' is not registered.");
                return;
            }

            var nm = NetworkManager.Singleton;
            bool isHost = nm != null && nm.IsServer;

            // Host-authoritative: a client would route the request via HostBridge (v2). For now, host-only.
            if (!isHost)
            {
                MelonLogger.Msg("[POGLib.Items] Use on client deferred (HostBridge RPC is a v2 feature).");
                return;
            }

            var ctx = new UseContext { IsHost = true, Bridge = CoreServices.Bridge };
            if (!handler(item, owner, ctx))
            {
                // The handler declined (e.g. "the bucket is moving") — the use is NOT spent.
                MelonLogger.Msg($"[POGLib.Items] {def.ContentId}: handler declined; use not spent.");
                return;
            }

            if (handle != null)
            {
                handle.UsesRemaining--;
                MelonLogger.Msg($"[POGLib.Items] {def.ContentId}: used, {handle.UsesRemaining} use(s) left.");

                if (handle.UsesRemaining <= 0 && def.Consumable)
                {
                    handle.IsConsumed = true;
                    try { item.ConsumeItem_Owner(); }
                    catch (Exception ex)
                    { MelonLogger.Warning($"[POGLib.Items] Consume failed: {ex.Message}"); }
                }
                else
                {
                    // Multi-use items only fired once: the item stays flagged as "doing its primary
                    // action" until something ends it, and our added ReadyItem has no effect logic of
                    // its own to do so — so the next click was swallowed. Close the action ourselves.
                    try { item.EndPrimaryAction(); }
                    catch (Exception ex)
                    { MelonLogger.Warning($"[POGLib.Items] EndPrimaryAction failed: {ex.Message}"); }
                }
            }
        }

        public bool IsModItem(InventoryItem item)
        {
            if (item == null) return false;
            var netObj = item.GetComponent<NetworkObject>();
            return netObj != null && CoreServices.Content.GetDefinitionByHash(netObj.GlobalObjectIdHash) != null;
        }
    }
}
