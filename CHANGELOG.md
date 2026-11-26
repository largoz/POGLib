# Changelog

## 0.1.4 (2026-11-26)

- **A locked part stands in the wardrobe under the game's own padlock**, among the game's locked parts, so players know it is there to
  be found; a click does nothing until the mod unlocks it. `HiddenWhileLocked = true` keeps a part out of the wardrobe until then (a
  secret), as every locked part was in 0.1.3. Nothing of ours goes into the game's own list of unlocked parts.

## 0.1.3 (2026-11-26)

- **Cosmetics** (`Content.Cosmetics`): clothes, hair, horns and accessories of a mod's own, from a GLB file. A model weighted to the
  goblin's bones moves with the body, one without weights sits on the head. They stand in the game's wardrobe and take the
  player's colours; a part can be locked until the mod unlocks it,
  and objects of a model can be hidden from its wearer in first person (a collar). An NPC wears them through `LookJson`. A player without the
  mod sees the goblin without the part; random goblins never wear one; nothing enters the game's list of rewards.
- Nothing changes for mods that do not use it.

## 0.1.2 (2026-11-25)

- **Models from OBJ files are drawn the right way round.** In 0.1.0 and 0.1.1 an OBJ as Blender and other tools export
  it was drawn inside out: you saw its far inner side instead of the near outer one. If you turned your faces over to
  make up for it, you need not change your file: where a face has normals, they decide which side is the outside.

## 0.1.1 (2026-11-23)

- **Mod items come back from saves.** In 0.1.0 an item a player carried or left in the bucket was saved, but on
  loading the game could not find it and it vanished: the game looks a saved item up in its own prefab list, and
  POGLib now adds mod items to that list too. Seen in the game: a berry saved in the bucket comes back; without its
  mod the save loads with no error.
- The log no longer calls capabilities "EXPERIMENTAL" (it did so for food too, which is shown working).

## 0.1.0 (2026-11-22)

First release.

- Items from code: `Content.Items.Register(new ModItemDefinition { ... })`, with the item's identity hashed from
  `ModId:ContentId`, the same on every player's machine.
- Your own model from an OBJ file (`ItemVisual.Obj`) and an icon from a PNG (`IconFile`); food (`EatCapability`);
  big items; journal entries.
- The public surface is listed in `api/POGLib.txt`; everything else is internal. Experimental parts are named in the
  README.
- Shops are a mod of their own: POGShops.
