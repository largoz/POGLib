# POGShops

Lets mods sell their items in Pit of Goblin's shops. A mod places **its own slots** next to a seller's goods with a
small data file, `pogshop.json`; no shop code. Built on [POGLib](https://thunderstore.io/c/pit-of-goblin/p/Largo/POGLib/).

**Players:** install it when a mod lists it as a dependency. On its own it places no slots.

## Multiplayer

- The host chooses what every slot sells; the other players see it through the game. **Only the host needs the
  mods' shop files.**
- A new slot is a copy of the game's own shop slot, so every player sees it. A player with POGShops gets its price
  tag where the host has it; a player without it may see the tag in the wrong place.
- With [POG Mod Check](https://thunderstore.io/c/pit-of-goblin/p/Largo/POG_Mod_Check/), players who both have
  POGShops are asked to have the same version.

## For mod developers

Give your item a `ShopCost` (POGLib), then ship `pogshop.json` in your package:

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
      "pool": [ { "item": "GlowBerry", "weight": 10 }, { "item": "nothing", "weight": 2 } ]
    }
  ]
}
```

- `"after": "left"` or `"right"` puts the slot at that end of the seller's row of goods, as a customer facing the
  seller sees it, on the ground like the seller's own goods. No coordinates; it names no slot of the game, so a game
  update that renames slots does not break it. `"pos"` and `"rot"` shift and tilt it from there; `"anchor"` +
  `"pos"` place a slot anywhere instead.
- The slot belongs to the seller: the seller's faction sets the price, it can be stolen from, and it is free when
  the seller dies. The price is the item's `ShopCost`.
- `pool`: your items with weights; `"nothing"` sometimes leaves the slot empty.

**In 0.1 a mod's file places only its own slots** (and says how its items stand on a counter). It cannot put items
into the game's slots or another mod's, change what others sell, or add dungeon merchants: such a file is refused,
with each reason in the log. A host can do those in a file of its own in `UserData/POGLib/Shops`.

Full guide, ids and the host's own files: [SHOPS.md](https://github.com/largoz/POGLib/blob/main/SHOPS.md).

## Status of 0.1.1

Shown in the game (build 26668562), on a host and a client over LAN: a mod's new slot at the end of the hub's
starter shop's row, on the floor; the client sees it where the host does, buys from it and passes the item on. A
mod file with `add` is refused.
Both ends of the row are found for every seller of the hub, InBetween and DarkBurg (from a dump of each scene); in
the game, slots were looked at in the hub. The automatic place is a start: correct it with `"pos"` and `"rot"`.
Not checked yet: slots in InBetween and DarkBurg looked at in the game, dungeon merchants.

Source and issues: [github.com/largoz/POGLib](https://github.com/largoz/POGLib)
