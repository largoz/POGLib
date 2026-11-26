# POGLib — modular content framework for Pit of Goblin

> **Version:** 0.1.4 · **Game:** Pit of Goblin · **Mod loader:** MelonLoader 0.7.2

One library with a modular core for adding **items, mobs, levels and effects** to the game via
community content packs. Targets **Unity IL2CPP + Unity Netcode for GameObjects** (host-authoritative).

## Architecture

Two mods, released together (since 2026-11-21; ROADMAP 1.6):

```
POGLib.dll                    ← the core: what a content pack calls
├── Core/                     ← internal: plugin graph, content registry (deterministic GlobalObjectIdHash),
│                               shells, network registration, loot, save guard, HostBridge (public: messages
│                               between a client and the host)
├── Items/                    ← Content.Items: ModItemDefinition, capabilities, use handlers
└── Cosmetics/                ← Content.Cosmetics: clothes from GLB files (docs/design/cosmetics.md)

POGShops.dll                  ← Shops/: shop files, weights, new slots, merchants, in-game tools.
                                A mod of its own on POGLib; only mods that sell items need it.
```

Mobs and levels come after the first release (ROADMAP, "After the first release").

### What a pack can call

Everything a pack can see is listed, line by line, in [`api/POGLib.txt`](api/POGLib.txt): `Content.Items`,
`ModItemDefinition` and the types it uses, the capabilities, use handlers, `HostBridge`, and
`Content.ContentAssemblies` (read by POG Mod Check). The rest of the library is `internal`. A build whose public
surface differs from that file fails `tools/apisurface` (`check`), so the list cannot drift from the code; a
change to it is a decision, written into the file on purpose (`write`). Within 0.x, what is in the file stays.

### Lifecycle stages

Stages are bound to real MelonLoader + NGO events (not a one-shot init) and may run more than
once, so systems are idempotent:

1. **Boot** (`OnInitializeMelon`) — register services, use-handlers, data contracts.
2. **SceneLoaded** (`OnSceneWasLoaded`) — build item templates once shell prefabs are in memory.
3. **BeforeSessionStart** (Harmony prefix on `StartHost`/`StartClient`/`StartServer`) — register
   network prefabs (the only legal window under `ForceSamePrefabs=true`) and inject loot host-side.
4. **SessionStop** (Harmony postfix on `Shutdown`) — reset session-scoped state so a lobby rejoin
   re-registers content.

## Usage (for content-pack authors)

```csharp
using POGLib.Items;  // Content, ModItemDefinition, ItemShellKind

public override void OnInitializeMelon()
{
    Content.Items.RegisterUseHandler("heal", (item, owner, ctx) =>
    {
        // ctx.IsHost is guaranteed true (host-authoritative use flow)
        // apply the effect here
        return true; // return false if the effect did not apply (use is not consumed)
    });

    Content.Items.Register(new ModItemDefinition
    {
        ModId = "MyMod",
        ContentId = "HealingBerry",
        ShellKind = ItemShellKind.Diamond,              // safe default shell
        ItemType = Il2Cpp.ItemType.Food,                // inventory slot routing (§2)
        MapIcon  = Il2Cpp.MapIconType.Food,             // minimap marker (separate field)
        GoldValue = 40,   // loot budget
        FoodValue = 3,    // satiety
        ShopCost  = 8,    // shop price (decoupled from gold)
        Consumable = true,
        MaxUses = 1,
        UseHandlerId = "heal",
    });
}
```

> Content mods load **after** POGLib (it uses `[MelonPriority(-1000)]`), so calling
> `Content.Items.*` from your `OnInitializeMelon` is safe. Templates build on scene load;
> spawning/consume happen host-side.

### Natural loot placement (not in 0.1)

**Not offered in 0.1** (`LootPlacement` is internal): loot puts copies of an item into the game's own tables, so a
weight there means something against vanilla content, and that is decided together with vanilla shop slots after the
first release. What follows is how the code works today, for that version.

Bias is emergent — there is **no rarity field** (see
the item-system research notes §5). Weight = copies appended;
value fields decide the value-budget match. Two table families are both supported:

```csharp
// Broad: appears in every biome's Treasure loot (fixed Il2Cpp.LootTable, array-realloc)
Loot = LootPlacement.In(LootCategory.Treasure)
// Restricted to specific biomes
Loot = LootPlacement.In(LootCategory.Treasure).RestrictTo("LootTable_Crypt", "LootTable_Mines")
// Named InventoryLootTable pools — ore veins, shops, gambling, reward chests (List.Add)
Loot = LootPlacement.InTables("LootTable_Destructible_Ore_Diamond")   // gem ore drop
Loot = LootPlacement.InTables("GamblingWheelSurprise")                 // gambling prize
```

Injection is host-side at scene load, before dungeon generation. **Do not** target reward-chest
tables (`DT_BonusChest_*`) casually — a removed mod then NREs on save restore; the framework's
`SaveGuard` catches it, but keeping mod items out of persisted reward chests is safer.

### Selling items in shops

Install POGShops too (a mod of its own on POGLib). Give the item a `ShopCost`, then place **your own slots** at
the game's sellers in a `pogshop.json` next to your mod — no shop code. `"after"` puts a slot in the row of a
seller's goods, so you write no coordinates. In 0.1 a mod's file cannot put items into the game's slots or another
mod's, nor add dungeon merchants (those come from code: `ShopsApi.AddMerchants`); a host's own file can. Full guide: [`SHOPS.md`](SHOPS.md); example:
`samples/ShopDemo/`.

## Key technical decisions

- **Identity = GlobalObjectIdHash** (`MD5(modId:contentId)`), not a string — one deterministic
  value gives stable save + network identity, identical on every peer.
- **Host-authoritative** execution (a client→host bridge is planned for v2).
- **No new RPCs / NetworkVariables** — impossible under IL2CPP; sync goes through existing
  vanilla RPCs and `CustomMessagingManager` named messages.
- **Natural loot** by appending prefab refs into vanilla ScriptableObject loot arrays
  (fixed `Il2CppReferenceArray`, so array-realloc — no consumer patching).
- **Versioned data contracts** per domain, so Items can ship while Mobs/Levels are still in research.

## Multiplayer note

On game build 26668562 (checked 2026-11-14 and over LAN on 2026-11-19) the game runs with
`NetworkConfig.ForceSamePrefabs` **false**: a player without a content pack can join a modded host, and the
objects of a pack it lacks simply do not appear for it (NGO logs "Failed to create object locally"). New shop
slots are copies of the game's own slot, so every player sees those. [POG Mod Check](https://github.com/largoz/POGModCheck)
shows a joining player what differs before anything loads. (Earlier builds ran with `true`; the sections below
that assume it are history.)

## Open questions (Milestone 0) — require launching the game

1. ✅ `NetworkConfig.ForceSamePrefabs` — already resolved statically to **TRUE** → the
   "mods-on-all-peers + join gate" architecture (only a runtime log-confirmation remains).
2. ⚠️ AssetBundle round-trip smoke test (an `HDRP/Lit` cube built in Unity 2022.3.62f2) —
   gates the full visual bundle pipeline.
3. ⚠️ Two-player test: a deterministic-hash prefab registers on both peers and the client sees
   the real mod item (not the shell).

Details and the full plan are in [ROADMAP.md](ROADMAP.md).

## Dependencies

- MelonLoader 0.7.2+ (net6)
- Il2CppInterop
- Harmony
- Unity Netcode for GameObjects (NGO)
- Assembly-CSharp (the game)

## License

MIT — POG community
