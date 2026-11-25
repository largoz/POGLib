# 23 Shops: dungeon merchants

A merchant of your own at a dungeon's entrance, with your goods, instead of the game's merchant on some visits. Added from
code with [POGShops](https://thunderstore.io/c/pit-of-goblin/p/Largo/POGShops/) 0.1.4 or later (0.1.2 has no `With`, `Endless`,
`Name` and hands). **Shown in the game** at all six stalls on a host, and at the Curiosa
stall over LAN: the goods, prices, name and weapon the same for a client, a client buys twice from a place that does not run out, and
one who joins inside the dungeon sees the merchant as the host does (the game lets them buy only from the next level on).

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
`Largo-POGShops-0.1.4` to your manifest's dependencies, and:

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
                            new PoolEntry { Item = "GlowBerry", Weight = 2, Endless = true },
                            new PoolEntry { Item = "vanilla:Item_Crossbow", With = { "vanilla:Item_CrossbowBolt" } },
                            new PoolEntry { Item = "vanilla:Item_CrossbowBolt", Weight = 0 },
                            new PoolEntry { Item = "vanilla:Item_Key" },
                        },
                    },
                },
            });
        }
    }
}
```

Here the berries never run out, the crossbow never stands without its bolts, and the bolts never come alone.

Only the host's merchants count; what they sell and how they look reaches the other players through the game, their name
through POGShops. A mod's item itself is needed by everyone who plays, as always.

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
| `Name` | text | the seller's own | over their head, up to 40 characters. A player without POGShops sees the seller's own |
| `Look` | function | none | gives the seller's look as the game's own outfit text (`Content.Cosmetics.LookJson` writes it, [50](50-cosmetics-your-first-part.md)); the seller's armour, hat and hand items then come off. `null` from it: the merchant cannot come now |
| `LeftHand` | item id | none | only with a `Look`: their weapon, as the shiv of the game's sellers. Hidden until somebody hurts them, gone when they die; how it looks, not how hard they stab |
| `RightHand` | item id | none | only with a `Look`: what they hold all the time |

## PoolEntry in Goods

| Field | Type | Default | Means |
|---|---|---|---|
| `Item` | text | required | `"GlowBerry"` is your own, `"vanilla:Item_Key"` the game's, `"OtherMod:Thing"` another mod's |
| `Weight` | number | 1 | against the other goods; 0 only for an item another one comes `With` |
| `Endless` | yes/no | no | the place does not run out: every buyer gets a copy, as with the bread of the starter shop. When the seller dies, the item itself can be taken, once |
| `With` | list of text | empty | other entries of the same `Goods` put out right after this one. If not all of them find a place, this one is passed over |

The stalls have few places: Antique, Foodlover, Smith and Tinkerer 3, MysticTent 4, Curiosa 5.

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
| `... comes With 'X', which is not in Goods` | the companion is no entry of its own | add it to `Goods` (`Weight = 0` if it only comes with another) |
| `merchant <id>: X and what comes with it need 4 places, the stall has 3; it is left out there` | too many companions for that stall | fewer companions, or leave it to the bigger stalls |
| `merchant <id>: no look now; they do not come` | your `Look` gave `null` | fine if you meant it |
| `merchant <id>: Look threw ...` | your `Look` failed | fix it; the merchant stays out, nothing else is hurt |

When a stall is decided the log says so:

```
[Shops] merchant stall Curiosa: BerryMod:trader (BerryMod 10%, the game's own 90%)
```

Next: [20 Shops: your first slot](20-shops-your-first-slot.md), [22 Shops: sellers and scenes](22-shops-sellers-and-scenes.md),
[90 Troubleshooting](90-troubleshooting.md).
