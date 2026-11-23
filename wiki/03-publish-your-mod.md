# 03 Publish your mod

Thunderstore is where Pit of Goblin's mods live; r2modman and Gale install from it and bring every dependency with
them. A package is a zip.

## The package

At the root of the zip:

| File | What |
|---|---|
| `manifest.json` | name, version, description, dependencies (below) |
| `icon.png` | 256 by 256 |
| `README.md` | your mod's page on the site |
| `CHANGELOG.md` | optional: the Changelog tab |
| `BerryMod.dll` | your mod |
| `pogshop.json`, your `.obj`, `.mtl`, `.png` | your shop file and model files, if you have them |

The mod manager puts the package in `Mods/<Author>-<Name>/`; MelonLoader loads your DLL from there, and POGShops
finds your `pogshop.json` there.

## manifest.json

```json
{
  "name": "BerryMod",
  "version_number": "1.0.0",
  "website_url": "https://github.com/you/BerryMod",
  "description": "Glow berries in the hub's starter shop.",
  "dependencies": [
    "LavaGang-MelonLoader-0.7.2",
    "Largo-POGLib-0.1.0",
    "Largo-POGShops-0.1.0"
  ]
}
```

- `description`: plain text, one or two sentences (it is the card on the site).
- `dependencies`: MelonLoader and POGLib always; POGShops only if you ship a `pogshop.json`. A version here is the
  lowest one your mod needs.

## Before you upload

- **Your ids are final**: `ModId`, every `ContentId`, every slot `id`. Changing one later makes it a different
  item or slot for every player.
- Every player in a game needs your mod, the same version: POG Mod Check counts a mod that adds items through POGLib
  as required for everyone by itself, and tells a joining player what differs. You need no code for that.
- Try it with two players if you can: a host and a client, both with your mod.

Upload at [thunderstore.io](https://thunderstore.io/c/pit-of-goblin/) ("Upload"), community Pit of Goblin, category
Mods.
