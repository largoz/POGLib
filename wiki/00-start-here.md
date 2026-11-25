# 00 Start here

POGLib lets your mod add **items** to Pit of Goblin that work in co-op: every player with your mod sees them, can
buy them, carry them, pass them on. [POGShops](https://thunderstore.io/c/pit-of-goblin/p/Largo/POGShops/), built on
it, lets your mod **sell** them in the game's shops. You write the item in C# (about fifteen lines) and its shop slot
in a small JSON file; the library does the rest: the network identity, the shell from the game, the name, the
journal entry.

Game build 26791993, MelonLoader 0.7.

## What you can add today

| You want | Page | State |
|---|---|---|
| An item: name, description, type, map icon, value, price | [02 Your first item](02-your-first-item.md) | shown in the game |
| Your own 3D model (OBJ) and icon (PNG) | [11 Items: models and icons](11-items-models-and-icons.md) | shown in the game |
| Food that heals or gives stamina | [12 Items: capabilities](12-items-capabilities.md) | shown in the game |
| A big item, carried in both hands | [10 Items: all fields](10-items-all-fields.md) | shown in the game |
| Glow, particles, a melee weapon, a throwable | [12 Items: capabilities](12-items-capabilities.md) | experimental |
| A use of your own when the item is used | 13 (later) | experimental, host only |
| A slot at a seller that sells your items | [20 Shops: your first slot](20-shops-your-first-slot.md) | shown in the game |
| A dungeon merchant with your goods and a look | [23 Shops: dungeon merchants](23-shops-dungeon-merchants.md) | shown in the game |
| Clothes, hair, horns, hats of your own (GLB) | [50 Cosmetics: your first part](50-cosmetics-your-first-part.md) | shown in the game |

**Shown in the game** means seen working on a host and a client over LAN. **Experimental** means the code is there
but nobody has seen it work in the game yet: it may change. **Not in 0.1:** natural loot drops, models from Unity
AssetBundles, mobs, levels.

## Pages

**Getting started**
- [01 Set up a mod project](01-set-up-a-mod-project.md)
- [02 Your first item](02-your-first-item.md)
- [03 Publish your mod](03-publish-your-mod.md)

**Items**
- [10 Items: all fields](10-items-all-fields.md)
- [11 Items: models and icons](11-items-models-and-icons.md)
- [12 Items: capabilities](12-items-capabilities.md)

**Shops** (POGShops)
- [20 Shops: your first slot](20-shops-your-first-slot.md)
- [21 Shops: the shop file](21-shops-the-shop-file.md)
- [22 Shops: sellers and scenes](22-shops-sellers-and-scenes.md)
- [23 Shops: dungeon merchants](23-shops-dungeon-merchants.md)

**Cosmetics** (POGLib 0.1.3)
- [50 Cosmetics: your first part](50-cosmetics-your-first-part.md)
- [51 Cosmetics: the model](51-cosmetics-the-model.md)
- [52 Cosmetics: the game's parts and colours](52-cosmetics-the-games-parts-and-colours.md)

**Everything else**
- [80 Multiplayer and saves](80-multiplayer-and-saves.md)
- [90 Troubleshooting](90-troubleshooting.md)

## Two rules that save you trouble later

- **Never rename your mod's id or an item's id after you release.** An item's identity is a hash of
  `ModId:ContentId`, the same on every player's machine; a new id is a new item.
- **Every player needs the same content mods** (cosmetics are the exception: a player without them sees the goblin without the part). Players with
  [POG Mod Check](https://thunderstore.io/c/pit-of-goblin/p/Largo/POG_Mod_Check/) are told what differs before
  they load into a game.

Source, the full list of what you can call (`api/POGLib.txt`) and issues:
[github.com/largoz/POGLib](https://github.com/largoz/POGLib).
