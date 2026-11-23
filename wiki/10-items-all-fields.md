# 10 Items: all fields

Every field of `ModItemDefinition`. Set them in the object you pass to `Content.Items.Register`
([02 Your first item](02-your-first-item.md)).

**Shown** = seen working in the game on a host and a client. **Experimental** = in the code, not seen working
yet; it may change.

## Identity

| Field | Type | Default | Means | State |
|---|---|---|---|---|
| `ModId` | text | required | your mod's id; never change it after release | shown |
| `ContentId` | text | required | the item's id within your mod; never change it after release | shown |

The item's network identity is a hash of `ModId:ContentId`, the same on every machine, so two players with your
mod agree on what the item is.

## Look

| Field | Type | Default | Means | State |
|---|---|---|---|---|
| `Visual` | `ItemVisual` | none | your model: `ItemVisual.Obj("path.obj")`; [11](11-items-models-and-icons.md) | shown |
| | | | or a mesh of a game item: `ItemVisual.GameMesh("prefab", "child")` | experimental |
| `IconFile` | path | none | a square PNG for the inventory and journal | shown |
| `IconSpriteName` | text | none | a game sprite as the icon, by name | experimental |
| `Icon` | `Sprite` | none | a sprite you made in code | experimental |
| `ShellKind` | `ItemShellKind` | `Diamond` | the game item yours is built from: `Diamond`, `Coin`, `Gem`, `BigItem`; `Big = true` picks `BigItem` itself | shown (`Diamond`, `BigItem`) |
| `Tint` | colour | none | tints the shell's own mesh | experimental |
| `StandingPoint`, `StandingRotation` | `Vector3` | the shell's, or the bottom centre of your model | the point that lands on a shop slot, and its turn | the default is shown |

## Text

| Field | Type | Default | Means | State |
|---|---|---|---|---|
| `DisplayName` | text | none | the item's name | shown |
| `DisplayDescription` | text | none | its description | shown |
| `TooltipKey`, `TooltipDescriptionKey` | text | none | the game's localisation keys instead of fixed text | experimental |

## Kind and value

| Field | Type | Default | Means | State |
|---|---|---|---|---|
| `ItemType` | `Il2Cpp.ItemType` | `Item` | which inventory slot it goes to: `Item`, `Treasure`, `Food`, `Weapon`, `Key` | shown |
| `MapIcon` | `Il2Cpp.MapIconType` | `Default` | its marker on the map (separate from `ItemType`) | shown |
| `JournalGroup` | `ItemGroup?` | from `ItemType` | the journal tab: Food, Treasure, Tools, Equipment, Weapons, Relics, Other | shown |
| `ShowInJournal` | yes/no | yes | listed in the journal once found | shown |
| `ShopCost` | number | 0 | its price in a shop; 0 is free, and the log warns | shown |
| `GoldValue` | number | 50 | its worth in gold | not checked |
| `FoodValue` | number | 0 | the game's food value | experimental |

## Handling

| Field | Type | Default | Means | State |
|---|---|---|---|---|
| `Big` | yes/no | no | carried in both hands, one at a time | shown |
| `PickupStaminaPenalty` | number | 0 | stamina taken when it is picked up | experimental |
| `MovementFactorWhenHeld` | number | 1 | below 1 slows the player who holds it | experimental |

## Use

| Field | Type | Default | Means | State |
|---|---|---|---|---|
| `Capabilities` | list | empty | food, glow, weapon and more: [12 Items: capabilities](12-items-capabilities.md) | food shown, the rest experimental |
| `UseHandlerId` | text | none | a use of your own, registered with `Content.Items.RegisterUseHandler` | experimental, host only |
| `Consumable`, `MaxUses` | yes/no, number | yes, 1 | used up after so many uses (with a use handler) | experimental |
| `SpawnOnConsume` | text | none | another of your items left behind when this one is used up | experimental |

A use handler runs on the host only in 0.1: a client's use of the item is not sent to the host yet.
