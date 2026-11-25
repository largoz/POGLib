# POGShops

Lets mods sell their items in Pit of Goblin's shops. A mod places **its own slots** next to a seller's goods with a
small data file, `pogshop.json`, and may have **dungeon merchants** of its own. Built on [POGLib](https://thunderstore.io/c/pit-of-goblin/p/Largo/POGLib/).

**Players:** install it when a mod lists it as a dependency. On its own it places no slots and brings no merchants.

## Multiplayer

- The host chooses what every slot sells; the other players see it through the game. **Only the host needs the
  mods' shop files.**
- A new slot is a copy of the game's own shop slot, so every player sees it. A player with POGShops gets its price
  tag where the host has it; a player without it may see the tag in the wrong place.
- A mod's dungeon merchant is the host's choice too; what the other players see of them comes through the game.
- With [POG Mod Check](https://thunderstore.io/c/pit-of-goblin/p/Largo/POG_Mod_Check/), players who both have
  POGShops are asked to have the same version.

## Dungeon merchants: the host's settings

A mod's merchant can keep a dungeon merchant's stall instead of the game's own. **All mods together take at most 75% of
the stalls, so the game's own merchants keep at least a quarter**; a mod asks for at most 25%. With
[POG Config](https://thunderstore.io/c/pit-of-goblin/p/Largo/POG_Config/) (optional) the host changes both on the page
"POG Shops": the limit for all mods and a line per mod (as the mod asks, off, or a percentage). Without it the same numbers
are in `UserData/MelonPreferences.cfg`, under `[POGLib_Shops]` and `[POGLib_Shops_Merchants]`.

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

**Dungeon merchants** are added from code. One mod is one class: the host draws the class first, then a merchant in it, so a mod
with ten merchants comes up as often as a mod with one.

```csharp
ShopsApi.AddMerchants(new MerchantClass
{
    ModId = "BerryMod", Title = "Berry Traders", Chance = 0.1,        // of the stalls below; at most 0.25
    Merchants = { new MerchantDef
    {
        Id = "trader", Weight = 3,                                    // against this class's other merchants only
        Stalls = { "Curiosa", "Antique" },
        Goods = { new PoolEntry { Item = "GlowBerry", Weight = 2 } },
    } },
});
```

Full guide, ids and the host's own files: [SHOPS.md](https://github.com/largoz/POGLib/blob/main/SHOPS.md).

## Status of 0.1.2

Shown in the game (the host on build 26791993), on a host and a client over LAN: a mod's new slot at the end of the hub's
starter shop's row, on the floor; the client sees it where the host does, buys from it and passes the item on. A
mod file with `add` is refused.
Both ends of the row are found for every seller of the hub, InBetween and DarkBurg (from a dump of each scene); in
the game, slots were looked at in the hub. The automatic place is a start: correct it with `"pos"` and `"rot"`.
Dungeon merchants, on a host: a mod's merchant at the Curiosa stall with its own goods and prices, a merchant that cannot come left
out, the limit at 0% (the game's own merchant and goods), the settings page. The odds are held by unit tests.
Not checked yet: dungeon merchants on a client and at the other five stalls; slots in InBetween and DarkBurg looked at in the
game.

Source and issues: [github.com/largoz/POGLib](https://github.com/largoz/POGLib)
