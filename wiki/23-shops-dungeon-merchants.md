# 23 Shops: dungeon merchants

A merchant of your own at a dungeon's entrance, with your goods, instead of the game's merchant on some visits. Added from
code with [POGShops](https://thunderstore.io/c/pit-of-goblin/p/Largo/POGShops/) 0.1.2 or later (0.1.3 if
some of your places may stay empty). **Shown in the game on a host and a client**: the Curiosa stall with a mod's goods, prices and
look; both buy there. **Not looked at yet:** the other five stalls.

## How it is decided

The game draws what stands at the entrance (one of six merchants or four dead adventurers) and that stays as it is. If it
is a merchant, the **host** decides once whose stall it is:

1. **A mod's or the game's.** The chances of the mods that can keep this stall add up, but never past the host's limit
   (75%); past it all shrink in proportion. The game's own merchant keeps at least a quarter of the stalls.
2. **Which mod.** One mod is one **class**: with ten merchants it comes up as often as with one.
3. **Which merchant of that mod**, by `Weight`, which means something only inside your class.

| Mods that ask for 25% | Each gets | The game's own merchants |
|---|---|---|
| 1 | 25% | 75% |
| 2 | 25% | 50% |
| 3 | 25% | 25% |
| 5 | 15% | 25% |
| 10 | 7.5% | 25% |

Only the dungeon: the same stalls in the towns stay the game's.

## The smallest merchant

Reference `POGShops.dll` beside `POGLib.dll`, name both in `[assembly: MelonAdditionalDependencies("POGLib", "POGShops")]`, add
`Largo-POGShops-0.1.2` to your manifest's dependencies, and:

```csharp
using MelonLoader;
using POGLib.Shops;
using POGLib.Shops.Model;

namespace BerryMod
{
    public class BerryMerchants : MelonMod
    {
        public override void OnInitializeMelon()
        {
            ShopsApi.AddMerchants(new MerchantClass
            {
                ModId = "BerryMod",
                Title = "Berry Traders",
                Chance = 0.1,
                Merchants =
                {
                    new MerchantDef
                    {
                        Id = "trader",
                        Stalls = { "Curiosa", "Antique" },
                        Goods =
                        {
                            new PoolEntry { Item = "GlowBerry", Weight = 2 },
                            new PoolEntry { Item = "vanilla:Item_Key", Weight = 1 },
                        },
                    },
                },
            });
        }
    }
}
```

Only the host's merchants count; what they sell and how they look reaches the other players through the game. A mod's item
itself is needed by everyone who plays, as always.

## MerchantClass

| Field | Type | Default | Means |
|---|---|---|---|
| `ModId` | text | required | your mod's id, the same as for your items. Once per mod |
| `Title` | text | your `ModId` | the name the host sees in the settings |
| `Chance` | number | 0.25 | 0 to 0.25: how often a merchant of yours keeps a stall they may take. The host may change it, above 0.25 too |
| `Merchants` | list | required | your merchants |

## MerchantDef

| Field | Type | Default | Means |
|---|---|---|---|
| `Id` | text | required | its id inside your mod, without `:` and `#`; never change it after release |
| `Weight` | number | 1 | against your other merchants only |
| `Stalls` | list | required | `Antique`, `Curiosa`, `Foodlover`, `MysticTent`, `Smith`, `Tinkerer` |
| `Goods` | list of `PoolEntry` | empty | what is sold: drawn by weight, no item twice on a stall; a place nothing is left for stays empty. Empty list: the stall's own goods |
| `Look` | function | none | gives the seller's look as the game's own outfit text; the seller's armour, hat and hand items then come off. `null` from it: the merchant cannot come now |

A merchant of a mod has the base price and no faction, so killing them costs no reputation.

The outfit text is what the game keeps in `NPCEntity.m_predefinedCosmeticsJson`; an id is a part's `CosmeticItem.m_id`, 0 is none:

```
{"HornId":0,"HairId":0,"ChestId":0,"LegsId":0,"FeetId":0,"HornColorIndex":0,"HairColorIndex":0,"ChestColorIndex":0,"LegsColorIndex":0,"EyeLeftTypeIndex":0,"EyeRightTypeIndex":0,"EyeColorIndex":0,"SkinColorIndex":6,"Accessories":[0,0,0,0,0,0]}
```

`ShopsApi.MerchantOf(npc)` says on the host whose a seller is: `"BerryMod:trader"`, or `null`.

## The host's settings

With [POG Config](https://thunderstore.io/c/pit-of-goblin/p/Largo/POG_Config/): the page **POG Shops** (the limit for all mods, and a
line per mod: as the mod asks, off, or a percentage). Without it, in `UserData/MelonPreferences.cfg`:

| Key | Default | Means |
|---|---|---|
| `[POGLib_Shops]` `MerchantCeiling` | 0.75 | the most of the stalls all mods together take |
| `[POGLib_Shops_Merchants]` `<ModId>` | -1 | -1 as the mod asks, 0 off, up to 1 |

Read when a stall is decided, so a change works from the next dungeon. To see your merchant every time, set both to 1.

## Common mistakes

| Log line | Means | Do |
|---|---|---|
| `merchants of '<mod>': Chance 0.5 must be from 0 to 0.25 ...; they are not used` | a class with a mistake is refused as a whole | correct what the line names |
| `... this mod has its class already` | `AddMerchants` was called twice | put all merchants into one class |
| `... no stall 'curiosa'; there are Antique, Curiosa, ...` | a stall's name is misspelt (capitals count) | use one of those the line offers |
| `merchant <id>: none of their goods is loaded; they do not come` | no item of `Goods` exists | check the ids |
| `merchant <id>: no look now; they do not come` | your `Look` gave `null` | fine if you meant it |
| `merchant <id>: Look threw ...` | your `Look` failed | fix it; the merchant stays out, nothing else is hurt |

When a stall is decided the log says so:

```
[Shops] merchant stall Curiosa: BerryMod:trader (BerryMod 10%, the game's own 90%)
```

Next: [20 Shops: your first slot](20-shops-your-first-slot.md), [22 Shops: sellers and scenes](22-shops-sellers-and-scenes.md),
[90 Troubleshooting](90-troubleshooting.md).
