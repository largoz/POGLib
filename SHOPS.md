# Selling your items: shop files

POGLib lets a content mod sell its items in the game's shops without writing shop code. You
register the item in code (as usual, `Content.Items.Register`), then describe **where it is sold** in a
data file, `pogshop.json`, shipped next to your mod. The in-game shop tools (below) write that file
for you.

Design background: `docs/design/shops.md`. Example: `samples/ShopDemo/`.

## What a shop slot is in this game

- A **slot** is one place on a counter that offers one item with a price tag. A merchant owns a handful
  of slots; the merchant's faction sets the price (reputation) and the slots become free when the
  merchant dies, or can be stolen from.
- Each vanilla slot either always offers the same item or draws one at random from a small pool.
- The **price** is the item's own `ShopCost` (the game then adjusts it for reputation). Set `ShopCost`
  on your `ModItemDefinition`: it defaults to 0, which a shop sells for free (the log warns).

## Chances are weights

Every candidate in a slot has a **weight**; its chance is its weight divided by the slot's total. Mods
never need to know about each other: three mods adding to one slot each just get their share.

| Candidate | Weight | Chance |
|---|---|---|
| vanilla potion | 10 | 50% |
| your berry | 5 | 25% |
| another mod's sword | 5 | 25% |

Each vanilla candidate counts as weight **10** (per occurrence in its pool), so "weight 5" means "half as
often as a vanilla item". `"nothing"` is a candidate too: give it a weight to make a slot sometimes
empty.

Players can change the vanilla weight: `VanillaWeight` under `[POGLib_Shops]` in
`UserData/MelonPreferences.cfg` (read on the host, from the next scene; `0` = in a slot some mod adds to, only
the mods' items come up). Write your weights against 10 all the same.

## The file

```jsonc
{
  "format": 1,                       // required
  "mod": "BerryMod",                 // your mod id; unqualified ids below belong to it
  "requires": [],                    // mods whose content your "patch" entries change
  "slots": [                         // new slots you place
    {
      "id": "berry_stand",
      "scene": "PitOfGoblin_Lobby",
      "seller": "vanilla:NPC_ShopKeeperShort_Lobby_StarterShop",   // who sells it: faction, theft, the seller's death
      // where it stands: next to one of the seller's own slots, in the row of its goods (no coordinates) ...
      "after": "vanilla:PitOfGoblin_Lobby/StarterShop/Game.Spawning.ShopLocation Item_Torch_Unlit",
      "pool": [ { "item": "GlowBerry", "weight": 10 }, { "item": "nothing", "weight": 2 } ]
    },
    {
      "id": "shelf_spot",
      "scene": "PitOfGoblin_InBetween",
      "seller": "vanilla:NPC_ShopKeeper_...",
      // ... or anywhere, by coordinates: "anchor" + "pos" instead of "after"
      "anchor": "vanilla:PitOfGoblin_InBetween/...",  // position is relative to this (default: seller)
      "pos": [0.35, 0, 0],           // metres, in the anchor's space
      "rot": [0, 90, 0],             // degrees, optional
      "priceTag": { "pos": [0, 0.3, 0.3] },     // optional, relative to the slot
      "pool": [ { "item": "GlowBerry", "weight": 10 } ]
    }
  ],
  "add": [                           // your items in anyone's slots (vanilla or mod)
    { "slot": "vanilla:PitOfGoblin_Lobby/StarterShop/Game.Spawning.ShopLocation Item_Bread",
      "item": "GlowBerry", "weight": 10 }
  ],
  "patch": [                         // change entries others put in a slot: exactly one of
    { "slot": "...", "item": "vanilla:Item_Bread", "weight": 0 },          // set weight
    { "slot": "...", "item": "OtherMod:Sword", "remove": true },           // remove
    { "slot": "...", "item": "vanilla:Item_Key", "replaceWith": "MyKey" }  // replace
  ],
  "sellers": [                       // dungeon merchants (copies of a vanilla merchant)
    { "id": "berry_merchant", "preset": "EntranceCave_Shopkeeper_Curiosa", "weight": 1,
      "slots": [ [ { "item": "GlowBerry", "weight": 1 } ] ] }
  ],
  "items": [                         // how YOUR items stand on a counter (optional)
    { "item": "GlowBerry", "standing": { "pos": [0, -0.02, 0], "rot": [0, 45, 0] } }
  ]
}
```

### Where a new slot stands

`"after"` names one of the seller's own slots **at an end of the row** its goods form (the hub's starter shop lays
them out in a line on its mat). The new slot goes one step past it along that row, at its height and turn; a second
slot after the same one goes a step further. A slot in the middle of the row, or a seller whose slots stand in no
row, is refused with the reason in the log. Use `"anchor"` and `"pos"` instead when the slot belongs somewhere else.

### Ids

| Kind | Form | Example |
|---|---|---|
| Your item | `<Mod>:<ContentId>` or just `<ContentId>` | `GlowBerry` |
| Game item | `vanilla:<prefab name>` | `vanilla:Item_Key` |
| Game slot | `vanilla:<scene>/<object path>` | `vanilla:PitOfGoblin_Lobby/StarterShop/Game.Spawning.ShopLocation Item_Bread` |
| Game seller | `vanilla:<npc flag>` | `vanilla:NPC_ShopKeeperShort_Lobby_StarterShop` |
| Mod slot | `<Mod>:<id>` | `BerryMod:berry_stand` |

You do not have to type these: the shop tools show the id of whatever you aim at.

### How an item stands on a counter

A slot puts the item's **standing point** (the game's `InventoryItem.m_standingTransform`) on its item holder.
Every mod item has one: the shell's own (Item_Diamond's is at its pivot), or, when the shell has none, the
bottom centre of the item's mesh. Move it with `StandingPoint` / `StandingRotation` on the
`ModItemDefinition`, or in data with the `items` section (your own items only).

### Dungeon merchants

The entrance cave of a dungeon draws one entry from `PropList_EntranceCave_MerchantOrAdventurer` (six vanilla
merchants and four dead adventurers, equally likely) with the dungeon seed. A `sellers` entry is a **variant** of
one vanilla merchant (`preset`, e.g. `EntranceCave_Shopkeeper_Curiosa`) with your own stock per slot (`slots`, in
the preset's slot order; a missing or empty slot keeps the preset's stock). Weight w adds the preset w more times
to that list; when the preset comes up, the host decides whose variant it is by the same weights. So your merchant
appears as often as w entries of its own would. It keeps the preset's NPC and look. Its slots are
`<Mod>:<id>#1`, `#2`, … for other mods' `add`/`patch`.

### Rules when several mods meet

- Order: every `slots`, then every `add`, then every `patch`. Mods are applied in dependency order
  (`requires`), ties by mod id. The last `patch` wins.
- `add` is always allowed. A `patch` that touches another mod's slot or item must list that mod in
  `requires`, otherwise it is refused with a log line. Patching vanilla content needs nothing.
- Every change is recorded per slot: the shop tools show who added and changed what.
- An item whose mod is not installed is left out (and logged); the remaining candidates share the chance.

## Where the file goes

The library reads:
- `Mods/**/pogshop.json` and `Mods/**/*.pogshop.json` — ship it inside your package, e.g.
  `Mods/<Author>-<Name>/pogshop.json` (Gale keeps that path; r2modman nests the package folder; both are
  found);
- `UserData/POGLib/Shops/*.json` — local files, where the shop tools save;
- shop files added from code: `Content.Shops.AddFile(new ShopFile { ... })`.

A file with an error is refused with a log line naming the problem (`[POGLib] [Shops] ...`);
unknown keys are reported as probable typos.

Mistakes that leave a file valid but do nothing are logged too, once per game run, with the closest ids that
do exist:

| Mistake | When it is reported |
|---|---|
| A rule for a mod slot that mod does not define (`"slot": "BerryMod:berry_stnad"`) | when the files load |
| An item that is not loaded: its mod is not installed, or a typo | before a session starts (mod items); when a slot uses it (game items) |
| An item with `ShopCost` 0 | the same |
| A rule for a game slot that is not in its scene | 5 s after the scene's slots spawned. A slot of a seller the game draws at random can be absent on some visits, so read this one with that in mind |

## The shop tools (in game)

For mod authors. Set `Enabled = true` under `[POGLib_ShopTools]` in `UserData/MelonPreferences.cfg`, host a
game alone, then:

| Key | Does |
|---|---|
| F7 | aim mode on/off: a box shows the slot and seller you look at |
| F8 | new slot of your mod where you look (next to the nearest seller, sold by them) |
| G | move the selected new slot to where you look |
| arrows, PageUp/PageDown | nudge it (Shift: finer); T switches between the slot and its price tag |
| [ ] | turn it |
| Enter | select the vanilla slot you look at |
| F9 | panel: slots here with chances and "who changed what", weights (− + x), find and add items, jump to Lobby / InBetween / DarkBurg, freeze the round timer, Save |

Save writes `UserData/POGLib/Shops/<mod>.pogshop.json`; copy it into your package as `pogshop.json`.
While that working copy exists it **replaces** your mod's packaged shop file(s) (the log says so), so the same
slot is never loaded twice; delete it to go back to the packaged file.
A change to a vanilla slot applies the next time the scene loads, and so does a new slot (it is a copy of the
game's own shop slot, so nothing has to exist before the session). The panel's chances always include every other
loaded mod, so you see the real share your weight buys.

## Multiplayer

The host chooses every slot's item; clients see the result through the game. **New slots** are copies of the
game's own shop slot, so every player sees them and **only the host needs the shop files**. A player with
POGLib gets the slot's price tag where the file puts it (the host tells it); a player without the library
sees the tag where the game's slot has it. An item from a mod a player does not have does not appear for that
player: the slot looks empty there.
