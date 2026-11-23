using Il2Cpp;
using POGLib.Core;
using UnityEngine;

namespace POGLib.Items
{
    /// <summary>Item visual kind.</summary>
    internal enum ItemVisualKind
    {
        None,
        GameMesh,     // clone a mesh child from an existing game prefab (reskin, no bundle)
        ObjFile,      // your own model, built at runtime from an OBJ (no Unity editor needed)
        Png,          // loose PNG icon → Sprite
        BundlePrefab, // prefab/mesh from an AssetBundle
    }

    /// <summary>
    /// The item's look (optional): made only by its factory methods. In 0.1 a pack has <see cref="Obj"/> (shown in the
    /// game) and <see cref="GameMesh"/>; an AssetBundle model waits for the game's Unity editor (ROADMAP, "Bundle
    /// models"), and an icon is <see cref="ModItemDefinition.IconFile"/>.
    /// </summary>
    public sealed class ItemVisual
    {
        internal ItemVisual() { }

        internal ItemVisualKind Kind = ItemVisualKind.None;
        internal string Path;                 // path to a PNG or a .bundle
        internal string AssetName;            // asset name inside the bundle (for BundlePrefab)
        internal string SourcePrefabName;     // donor game prefab (for GameMesh)
        internal string ChildName;            // mesh child name to clone (for GameMesh)
        internal bool StripSpeakingStone = true; // when donor is SpeakingStone, strip voice/VFX

        // — Placement of the custom visual inside the item (BundlePrefab) —
        internal Vector3 LocalOffset = Vector3.zero;
        internal Vector3 LocalEuler = Vector3.zero;
        internal Vector3 LocalScale = Vector3.one;
        /// <summary>Re-point materials that lost their shader in the bundle onto the game's HDRP/Lit.</summary>
        internal bool RepairShaders = true;

        /// <summary>Reskin by cloning a mesh child from an existing game prefab (no AssetBundle).</summary>
        public static ItemVisual GameMesh(string sourcePrefabName, string childName)
            => new ItemVisual { Kind = ItemVisualKind.GameMesh, SourcePrefabName = sourcePrefabName, ChildName = childName };

        /// <summary>
        /// Your own model from an OBJ file, built into a Unity mesh at runtime — **no AssetBundle and
        /// no Unity editor**. Materials come from the accompanying .mtl, mapped onto the game's own
        /// HDRP/Lit (base colour, metallic, smoothness, emission).
        ///
        /// This is the cheapest way to ship a genuinely custom shape: any 3D tool exports OBJ, so a
        /// modder does not need the game's exact Unity version. What it cannot carry is anything the
        /// Unity editor authors — rigs, animation clips, inspector-configured particle systems, custom
        /// shaders; those still need <see cref="Bundle"/>. Effects can be added on top through
        /// capabilities (GlowCapability, ParticleEffect), which is usually enough.
        /// </summary>
        public static ItemVisual Obj(string objPath)
            => new ItemVisual { Kind = ItemVisualKind.ObjFile, Path = objPath };

        internal static ItemVisual Png(string path) => new ItemVisual { Kind = ItemVisualKind.Png, Path = path };

        /// <summary>
        /// Custom mesh/prefab from an AssetBundle — your own model WITH its own visual effects
        /// (particles, lights, trails, animation all ride along inside the prefab). Custom C#
        /// MonoBehaviour scripts do NOT survive under IL2CPP; use capabilities/use handlers for logic.
        /// Build the bundle with the game's Unity version (6000.3.24f1 on build 26668562). Not shown in the game yet.
        /// </summary>
        internal static ItemVisual Bundle(string bundlePath, string assetName)
            => new ItemVisual { Kind = ItemVisualKind.BundlePrefab, Path = bundlePath, AssetName = assetName };

        /// <summary>Position/rotate/scale the custom visual inside the item (fluent).</summary>
        internal ItemVisual At(Vector3 offset, Vector3 euler = default, float scale = 1f)
        {
            LocalOffset = offset;
            LocalEuler = euler;
            LocalScale = new Vector3(scale, scale, scale);
            return this;
        }
    }

    /// <summary>Loot table category (an array in Il2Cpp.LootTable). Internal in 0.1, as is <see cref="LootPlacement"/>.</summary>
    internal enum LootCategory { Treasure, Food, Useful, Big, Shop }

    /// <summary>
    /// Natural-drop placement. There is no rarity/weight field in the game — bias is emergent:
    /// weight = how many copies are appended (uniform selection turns that into weighting), and
    /// value fields (GoldValue/FoodValue/ShopCost) decide the value-budget match. See
    /// _research/ITEM_SYSTEM.md §5.
    ///
    /// Two table families (§5.6):
    ///  • Il2Cpp.LootTable — fixed arrays, 5 categories (biome loot, secret rooms, LootSpawner).
    ///    Target via Category (all tables) optionally restricted by name.
    ///  • Il2CppInventory.InventoryLootTable — growable List (ore veins, shop pools, gambling,
    ///    reward chests, dungeon-piece overrides). Target by explicit table name(s).
    /// </summary>
    /// <remarks>
    /// Not offered to packs in 0.1 (the user, 2026-11-21): loot puts copies of an item into the game's own tables, so a
    /// weight there means something against vanilla content, the same question that keeps vanilla shop slots out of the
    /// first release. The code stays; it opens with that answer and a run where a client finds the item (ROADMAP 2.7).
    /// </remarks>
    internal sealed class LootPlacement
    {
        /// <summary>LootTable array to append to (null = don't touch fixed-array tables).</summary>
        public LootCategory? Category;
        /// <summary>Restrict to these table names (both families). Null = all LootTables of Category.
        /// Required to inject into InventoryLootTable pools (ore/shop/gambling/reward).</summary>
        public string[] TableNames;
        /// <summary>Copies appended (uniform draw → this is the weight).</summary>
        public int Weight = 1;

        /// <summary>Append into the given category of ALL loaded Il2Cpp.LootTables (broad, e.g. all biomes).</summary>
        public static LootPlacement In(LootCategory category) => new LootPlacement { Category = category };

        /// <summary>Target named tables (e.g. "LootTable_Destructible_Ore_Diamond", "GamblingWheelSurprise",
        /// "DT_BonusChest_Tier1"). InventoryLootTable uses List.Add; a LootTable also needs a Category.</summary>
        public static LootPlacement InTables(params string[] names) => new LootPlacement { TableNames = names };

        /// <summary>Restrict a category placement to specific named LootTables (e.g. one biome).</summary>
        public LootPlacement RestrictTo(params string[] names) { TableNames = names; return this; }
        public LootPlacement WithWeight(int w) { Weight = w; return this; }
    }

    /// <summary>Mod item data contract (versioned — data outlives code).</summary>
    public sealed class ModItemDefinition : IContentDefinition
    {
        // — Identity —
        public string ModId { get; set; }
        public string ContentId { get; set; }

        // — Shell / visual —
        /// <summary>Vanilla donor shell (Diamond by default — safe).</summary>
        public ItemShellKind ShellKind { get; set; } = ItemShellKind.Diamond;
        public ItemVisual Visual { get; set; }
        /// <summary>Direct icon override (takes precedence over IconSpriteName).</summary>
        public Sprite Icon { get; set; }
        /// <summary>Name of an existing game Sprite to use as the icon (resolved at build).</summary>
        public string IconSpriteName { get; set; }
        /// <summary>Path to a PNG icon of the mod's own (square, transparent background; the sample's is 256x256),
        /// used when neither <see cref="Icon"/> nor <see cref="IconSpriteName"/> is set. An item with a model of its own
        /// (<c>ItemVisual.Obj</c>) and no icon shows the shell's icon.</summary>
        public string IconFile { get; set; }
        /// <summary>Optional jewel/mesh tint (HDRP _BaseColor).</summary>
        public Color? Tint { get; set; }

        // — Text / localization —
        public string DisplayName { get; set; }
        public string DisplayDescription { get; set; }
        /// <summary>I2 localization key for the name (falls back to DisplayName).</summary>
        public string TooltipKey { get; set; }
        /// <summary>I2 localization key for the description (falls back to DisplayDescription).</summary>
        public string TooltipDescriptionKey { get; set; }

        // — Classification (ITEM_SYSTEM.md §2). ItemType gates the inventory slot; MapIcon is the
        //   minimap marker — a SEPARATE field. Set these or the item mis-routes / has no map marker. —
        /// <summary>Inventory slot filter ([Flags]). Item=generic, Treasure=gems, Food, Weapon, Key.</summary>
        public ItemType ItemType { get; set; } = ItemType.Item;
        /// <summary>Minimap marker: Default, Player, Enemy, Food, Treasure, Crumb, Door, NPC (enum
        /// <c>Il2Cpp.MapIconType</c>; before the 2026-11-06 game update it was nested in PassiveItem_Map).</summary>
        public MapIconType MapIcon { get; set; } = MapIconType.Default;
        /// <summary>The journal tab the item is listed under (Food, Treasure, Tools, Equipment, Weapons, Relics,
        /// Other). Null: Food for a <c>Food</c> item, Treasure for a <c>Treasure</c> item, otherwise Other.</summary>
        public Il2CppGame.Journal.ItemGroup? JournalGroup { get; set; }
        /// <summary>List the item in the journal (it shows once the item has been discovered). Default true.</summary>
        public bool ShowInJournal { get; set; } = true;

        // — Numeric properties. Also the drop-weight knobs in the value-budget loot spawner (§5). —
        /// <summary>Loot budget / treasure value (matched against LootValueTable_Gold levels).</summary>
        public int GoldValue { get; set; } = 50;
        /// <summary>Satiety when eaten (0-5). Real eating needs an eat capability too (v2).</summary>
        public int FoodValue { get; set; } = 0;
        /// <summary>Shop purchase price (decoupled from GoldValue). 0 = not priced.</summary>
        public int ShopCost { get; set; } = 0;

        // — Handling / "weight" (ITEM_ANATOMY.md §1.4 — there is NO weight field; these are the
        //   real proxies, all plain fields on InventoryItem, so setting them is statically sound). —
        /// <summary>Big item: takes the hands/large slot instead of a normal inventory slot (m_isBigItem).</summary>
        public bool Big { get; set; } = false;
        /// <summary>Stamina drained when this item is picked up — the closest thing to weight (m_staminaPenaltyOnPickup).</summary>
        public float PickupStaminaPenalty { get; set; } = 0f;
        /// <summary>Movement multiplier while held (1 = no slow-down; &lt;1 = heavier feel) (m_movementFactorWhenHeld).</summary>
        public float MovementFactorWhenHeld { get; set; } = 1f;

        // — Usage —
        public bool Consumable { get; set; } = true;
        public int MaxUses { get; set; } = 1;
        /// <summary>Id of a registered use handler (RegisterUseHandler).</summary>
        public string UseHandlerId { get; set; }

        /// <summary>
        /// Another registered content item to leave behind when this one is used up — a spent husk, an
        /// empty bottle, a broken tool. Accepts "contentId" (same mod) or "modId:contentId".
        ///
        /// Uses the game's OWN mechanism (<c>InventoryItem.m_spawnOnDestroy</c>), so the replacement is
        /// spawned and replicated by the game rather than by us: it appears for every player, survives
        /// saving, and needs no RPC of our own.
        /// </summary>
        public string SpawnOnConsume { get; set; }

        // — Capabilities — declarative sibling components (eat/weapon/throw). Which are shown in the game and which are
        //   experimental is said in the store page and the wiki (00 Start here), not here: the log no longer labels them
        //   (it called eating EXPERIMENTAL after eating was shown, 2026-11-23). —
        //   See ItemCapability. Attached at template build; validate on-item in Milestone 0. —
        public System.Collections.Generic.List<ItemCapability> Capabilities { get; } =
            new System.Collections.Generic.List<ItemCapability>();

        // — Natural drops: not in 0.1 (see LootPlacement) —
        internal LootPlacement Loot { get; set; }

        // — How the item stands on a shop counter —
        /// <summary>
        /// The point of the item that a shop slot puts on its holder (the game's
        /// <c>InventoryItem.m_standingTransform</c>), in the item's local space. Null = keep the shell's own,
        /// or, when the shell has none, the bottom centre of the item's mesh, so it stands ON the counter.
        /// A shop file's "items" section can override it too (that is what the shop tools write).
        /// </summary>
        public UnityEngine.Vector3? StandingPoint { get; set; }
        /// <summary>Rotation of the standing point, in degrees (null = unchanged).</summary>
        public UnityEngine.Vector3? StandingRotation { get; set; }
    }
}
