# 20 Shops: your first slot

Sell your item at one of the game's sellers. You need [POGShops](https://thunderstore.io/c/pit-of-goblin/p/Largo/POGShops/)
(add `Largo-POGShops-0.1.0` to your manifest's dependencies) and an item with a `ShopCost`
([02 Your first item](02-your-first-item.md)). No code: a file.

## The file

Save this as `pogshop.json` next to your mod's DLL, in your package:

```json
{
  "format": 1,
  "mod": "BerryMod",
  "slots": [
    {
      "id": "berry_stand",
      "scene": "PitOfGoblin_Lobby",
      "seller": "vanilla:NPC_ShopKeeperShort_Lobby_StarterShop",
      "after": "left",
      "pool": [ { "item": "GlowBerry", "weight": 10 } ]
    }
  ]
}
```

| Key | Means |
|---|---|
| `format` | always `1` for now |
| `mod` | your mod's id, the same as `ModId` in your code; `GlowBerry` below means `BerryMod:GlowBerry` |
| `id` | your slot's own id; never change it after release |
| `scene` | where the seller stands; `PitOfGoblin_Lobby` is the hub. Sellers: [22 Shops: sellers and scenes](22-shops-sellers-and-scenes.md) |
| `seller` | who sells it: the seller's faction sets the price, the slot can be stolen from, and it is free when the seller dies |
| `after` | `"left"` or `"right"`: the slot goes at that end of the seller's row of goods, as a customer facing the seller sees it, on the ground like the seller's own |
| `pool` | your items and their weights; `{ "item": "nothing", "weight": 2 }` sometimes leaves the slot empty |

The price is the item's `ShopCost`. Two slots `"after": "left"` stand one after the other.

## Moving and tilting it

The automatic place is a start. Past the end of a table there may be no table, so look at it in the game and
correct it with `pos` (metres, in the slot's own axes: x sideways, y up, z forward) and `rot` (degrees: x tilts it
forward or back, y turns it, z tilts it sideways):

```json
"after": "left",
"pos": [0, 0.1, -0.2],
"rot": [0, 30, 0],
```

You see a change after the scene loads again.

## See it

Host a game: the slot appears when the hub loads. Other players see it too, and **only the host needs your shop
file**; a player with POGShops also sees its price tag where yours is.

In the log (`MelonLoader/Latest.log`):

```
[Shops] 1 shop file(s) from 1 mod(s): 1 new slot(s), 1 slot(s) with rules, 0 merchant(s); order BerryMod
[Shops] placed BerryMod:berry_stand at (8.77, 3.85, -11.30) in the row after Game.Spawning.ShopLocation Item_Torch_Unlit, on the floor (SM_Prop_Rug_02), sold by vanilla:NPC_ShopKeeperShort_Lobby_StarterShop (None)
[Shops] BerryMod:berry_stand: BerryMod:GlowBerry (100% of 1 candidate(s))
```

## What your file cannot do in 0.1

A mod's file places only **its own** slots. It cannot put items into the game's slots or another mod's (`add`),
change what others sell (`patch`), or add dungeon merchants (`sellers`): a file with any of those is not used at
all, and the log names each reason:

```
[Shops] ...pogshop.json: "add" puts items into slots that are not this mod's (1 rule(s)): a mod's shop file may only place its own slots ("slots") and say how its items stand ("items") in this version; the file is not used. Put the items in a slot of your own instead
```

A player hosting a game can do those in a file of their own, for their own games:
[21 Shops: the shop file](21-shops-the-shop-file.md).

## Common mistakes

| Log line | Fix |
|---|---|
| `item 'BerryMod:GlowBerry' ... is not loaded` | the item id is misspelt, or your mod did not register it |
| `... has ShopCost 0` | give the item a price |
| `no left end at ...` | the seller's slots stand in no row; use `"anchor"` and `"pos"` instead ([21](21-shops-the-shop-file.md)) |
| `... needs a "seller"` | `"after": "left"` / `"right"` need the seller |
