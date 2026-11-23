# 22 Shops: sellers and scenes

The sellers a slot can belong to, with the ids your shop file uses (`scene` and `seller`). Read from the game
(build 26668562) by a dump of each scene; a game update can add or move sellers.

"Left" and "right" are as a customer facing the seller sees them; the slot goes one step past the slot named in the
column. **Seen in the game** means a slot was placed there and looked at; the rest is worked out from the dump by
the same code the library runs, not looked at yet. Past the end of a table there may be no table: look, and correct
the place with `pos` and `rot` ([20](20-shops-your-first-slot.md)).

## The hub (`PitOfGoblin_Lobby`)

| Seller | `seller` id | Goods | Left / right end |
|---|---|---|---|
| Starter shop (the short shopkeeper) | `vanilla:NPC_ShopKeeperShort_Lobby_StarterShop` | small items in a row on a rug, a big item beside | torch / rock, both **seen in the game** (on the ground beside the rug) |
| Curiosa | `vanilla:NPC_ShopKeeper_Lobby_Hub_Curiosa` | small items in a row on a table; a cog above, armour on a dummy, a treasure display | dice / orb; right **seen in the game**: past the table's edge, among the decoration, so shift it |

The hub's gambling seller (`vanilla:NPC_ShopKeeper_Lobby_Hub_Gambling`) has no shop slots.

## InBetween (`PitOfGoblin_InBetween`)

| Seller | `seller` id | Goods | Left / right end |
|---|---|---|---|
| Pitaxas, the pawn shop | `vanilla:NPC_ShopKeeper_InBetween_Pitaxas` | potions, a rack of slings and pouches, armour on a dummy, a backpack | water potion / pouch |
| Big Brunna, the bucket vendor | `vanilla:NPC_ShopKeeper_InBetween_BigBrunna` | bucket upgrades (big items) | brazier / water bucket, about 1 m apart |
| Mold Muncher, the mystic tent | `vanilla:NPC_ShopKeeper_InBetween_MoldMuncher` | speaking stones, a tome, a mushroom, potions, a scroll | tome / mushroom, slots only 0.22 m apart |
| Exile Bango | `vanilla:NPC_ShopKeeper_InBetween_ExileBango` | slingshot, key, rocks, compass, torch | torch / slingshot. **Drawn at random**: on some visits another seller stands there, and your slot is not placed (the log says so) |

## DarkBurg (`PitOfGoblin_DarkBurg`)

| Seller | `seller` id | Goods | Left / right end |
|---|---|---|---|
| Moggoma the Inventor, the gadget shop | `vanilla:NPC_ShopKeeper_Darkburg_MoggomaTheInventor` | a cog, a crossbow, bolts, tool belts | crossbow / tool belt |
| The Maiden, the smith | `vanilla:NPC_ShopKeeper_Darkburg_Maiden` | dagger, axe and pickaxe on a table; an item rack, an anvil, armour on a dummy | pickaxe / axe |
| The pig butcher, the food hall | `vanilla:NPC_ShopKeeper_Darkburg_PigButcher` | four mugs in a row | mug 04 / mug 01 |
| The Tinkerer | `vanilla:NPC_Shopkeeper_Dungeon_Tinkerer` | a cog, dynamite, a slingshot | cog / dynamite. A merchant of the dungeon entrances; seen in DarkBurg in one dump, so probably drawn at random |

## A seller without a row

When the goods do not stand in a row, the log says so and the slot is not placed: place it by coordinates instead,
`"anchor"` and `"pos"` ([21 Shops: the shop file](21-shops-the-shop-file.md)).
