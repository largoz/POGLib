# Changelog

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
