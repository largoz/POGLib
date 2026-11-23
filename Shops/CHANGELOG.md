# Changelog

## 0.1.1 (2026-11-23)

- `"after": "right"` placed nothing at some sellers (Curiosa, Pitaxas: "the slot is in the middle of the row"); the
  row is now found once for both its ends.
- A seller's row is found even when one of its slots stands far away (the Darkburg smith's item rack).
- With `"after"`, `"pos"` shifts the slot from its automatic place (in its own axes: x sideways, y up, z forward)
  and `"rot"` turns and tilts it. The automatic place is a start; past the end of a table there may be no table.
- Needs POGLib 0.1.1 (mod items come back from saves).

## 0.1.0 (2026-11-22)

First release.

- A mod places its own shop slots at the game's sellers with a `pogshop.json` file: `"after": "left"` or `"right"`
  puts a slot at that end of the seller's row of goods, on the floor like the seller's own.
- Each slot has its own pool of items with weights; the price is the item's `ShopCost`.
- Only the host needs the shop files; clients learn about new slots from the host.
- In 0.1 a mod's file places only its own slots; a host's own file in `UserData/POGLib/Shops` can do more.
