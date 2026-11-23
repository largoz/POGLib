# 22 Shops: sellers and scenes

The sellers a slot can belong to, with the ids your shop file uses (`scene` and `seller`). Read from the game
(build 26668562) by a dump of each scene; a game update can add or move sellers.

## The hub (`PitOfGoblin_Lobby`)

| Seller | `seller` id | Its goods | `"after": "left"` / `"right"` |
|---|---|---|---|
| The starter shop, the short shopkeeper | `vanilla:NPC_ShopKeeperShort_Lobby_StarterShop` | 4 small items in a row on a rug, a big item on its own stand beside them | left: past the torch; right: past the rock. Both **shown in the game** |
| Curiosa | `vanilla:NPC_ShopKeeper_Lobby_Hub_Curiosa` | 4 small items in a row on a table; a cog above it, armour on a dummy, a treasure display | worked out from the dump only: left past the dice, right past the orb; **not seen on screen yet** |

The gambling seller in the hub (`vanilla:NPC_ShopKeeper_Lobby_Hub_Gambling`) has no shop slots.

"Left" and "right" are as a customer facing the seller sees them. A slot in a row stands on the ground found under
it, so past the end of a rug it may stand on the floor beside it.

## Other scenes

Not listed yet: InBetween, DarkBurg, and the merchants at a dungeon's entrance (those are drawn at random, so a
slot there is absent on some visits). This page grows as each scene is read.

## A seller without a row

For a seller whose goods do not stand in a row, place the slot by coordinates instead: `"anchor"` and `"pos"`
([21 Shops: the shop file](21-shops-the-shop-file.md)).
