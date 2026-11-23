# 21 Shops: the shop file

Every key of `pogshop.json`. Start with [20 Shops: your first slot](20-shops-your-first-slot.md) if you have not.

## Where it goes

POGShops reads, on the host:

- `Mods/**/pogshop.json` and `Mods/**/*.pogshop.json`: ship it at the root of your package; the mod manager puts it
  in `Mods/<Author>-<Name>/`;
- `UserData/POGLib/Shops/*.json`: a host's own files (below);
- a file a mod builds in code: `ShopsApi.AddFile(new ShopFile { ... })` (POGShops' DLL), with the same limits as a
  mod's file.

A file with an error is not used, and the log names the problem. A key it does not know is reported as a probable
typo.

## The file

| Key | Type | Means |
|---|---|---|
| `format` | number | `1` |
| `mod` | text | your mod's id; an id below without `Mod:` belongs to it |
| `slots` | list | your new slots (below) |
| `items` | list | how your own items stand on a counter (below) |

### A slot

| Key | Required | Means |
|---|---|---|
| `id` | yes | your slot's id, without `:`; never change it after release |
| `scene` | yes | the scene the seller is in ([22](22-shops-sellers-and-scenes.md)) |
| `seller` | for `after` | the game's seller: faction (price), theft, the seller's death |
| `after` | one of `after` / `pos` | `"left"` or `"right"`: that end of the seller's row of goods. Or a game slot's id at one end of the row |
| `anchor` | with `pos` | a seller's or a game slot's id the position is measured from (default: the seller) |
| `pos` | one of `after` / `pos` | `[x, y, z]` in metres, in the anchor's space |
| `rot` | no | `[x, y, z]` degrees; with `after` only the turn about the vertical (`y`) is used |
| `priceTag` | no | `{ "pos": [x, y, z], "rot": [x, y, z] }`, relative to the slot |
| `template` | no | the game's slot prefab to copy: `"ShopLocation"` (holds the item on a small table, 0.75 m up) or `"ShopLocationSlim"` (on the ground). Default: slim with `after`, the table one with `pos` |
| `pool` | yes | `[ { "item": "...", "weight": 10 }, ... ]`; `"nothing"` is an item too: an empty slot |

A slot placed with `after` stands on the ground found under its place, upright, turned like the slot next to it.
Weights are shares: `10` and `5` mean two thirds and one third.

### How your item stands

```json
"items": [ { "item": "GlowBerry", "standing": { "pos": [0, -0.02, 0], "rot": [0, 45, 0] } } ]
```

The point of the item that lands on the slot, in the item's own space, and its turn. Without it: the shell's own
point, or the bottom centre of your model. The same can be set in code (`StandingPoint`, `StandingRotation`). You see
a change after the scene loads again.

## Ids

| Kind | Form | Example |
|---|---|---|
| Your item | `<Mod>:<ContentId>` or just `<ContentId>` | `GlowBerry` |
| Game item | `vanilla:<prefab name>` | `vanilla:Item_Apple` |
| Game seller | `vanilla:<npc flag>` | `vanilla:NPC_ShopKeeperShort_Lobby_StarterShop` |
| Game slot | `vanilla:<scene>/<object path>` | `vanilla:PitOfGoblin_Lobby/StarterShop/Game.Spawning.ShopLocation Item_Bread` |
| Your slot | `<Mod>:<id>` | `BerryMod:berry_stand` |

A game slot's id contains its default item's name, so a game update may change it; `"after": "left"` / `"right"`
name no slot and do not break that way.

## A host's own file

A player hosting a game can keep files in `UserData/POGLib/Shops/*.json`, under a mod id no package uses (say
`"mod": "MyServer"`). They hold everything above and also `add` (items into the game's slots or a mod's), `patch`
(change or remove what others sell), `requires` (mods a `patch` changes) and `sellers` (dungeon merchants). They
apply to that host's games only. What a weight means next to the game's own items may change in a later version, so
these are a host's settings, not something to publish; a mod's file with any of them is not used.

Details of those sections: [SHOPS.md](https://github.com/largoz/POGLib/blob/main/SHOPS.md#the-hosts-own-files).
