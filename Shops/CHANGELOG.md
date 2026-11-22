# Changelog

## 0.1.0 (2026-11-22)

First release.

- A mod places its own shop slots at the game's sellers with a `pogshop.json` file: `"after": "left"` or `"right"`
  puts a slot at that end of the seller's row of goods, on the floor like the seller's own.
- Each slot has its own pool of items with weights; the price is the item's `ShopCost`.
- Only the host needs the shop files; clients learn about new slots from the host.
- In 0.1 a mod's file places only its own slots; a host's own file in `UserData/POGLib/Shops` can do more.
