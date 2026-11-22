# Changelog

## 0.1.0 (2026-11-22)

First release.

- Items from code: `Content.Items.Register(new ModItemDefinition { ... })`, with the item's identity hashed from
  `ModId:ContentId`, the same on every player's machine.
- Your own model from an OBJ file (`ItemVisual.Obj`) and an icon from a PNG (`IconFile`); food (`EatCapability`);
  big items; journal entries.
- The public surface is listed in `api/POGLib.txt`; everything else is internal. Experimental parts are named in the
  README.
- Shops are a mod of their own: POGShops.
