# 02 Your first item

A berry that heals, registered from your mod's code. Set up the project first:
[01 Set up a mod project](01-set-up-a-mod-project.md).

## The code

```csharp
using MelonLoader;
using POGLib.Items;

[assembly: MelonInfo(typeof(BerryMod.BerryModMain), "BerryMod", "1.0.0", "you")]
[assembly: MelonGame(null, "Pit of Goblin")]
[assembly: MelonAdditionalDependencies("POGLib")]

namespace BerryMod
{
    public class BerryModMain : MelonMod
    {
        public override void OnInitializeMelon()
        {
            Content.Items.Register(new ModItemDefinition
            {
                ModId = "BerryMod",          // your mod's id: never change it after release
                ContentId = "GlowBerry",     // the item's id: never change it after release
                DisplayName = "Glow Berry",
                DisplayDescription = "Tastes like an apple, somehow.",
                ItemType = Il2Cpp.ItemType.Food,
                MapIcon = Il2Cpp.MapIconType.Food,
                GoldValue = 20,
                ShopCost = 7,
                Capabilities = { new EatCapability { CopyFrom = "Item_Apple", HealthOnEating = 15f } },
            });
        }
    }
}
```

What each part does:

| Line | Why |
|---|---|
| `MelonAdditionalDependencies("POGLib")` | MelonLoader loads POGLib before your mod |
| `OnInitializeMelon` | the place to register: POGLib has started, the game has not built anything yet |
| `ModId`, `ContentId` | the item's identity on every machine; changing either later makes it a different item |
| `ItemType`, `MapIcon` | the inventory slot it goes to, and its marker on the map (two separate things) |
| `GoldValue`, `ShopCost` | its worth, and its price in a shop (0 = free, and the log warns) |
| `EatCapability` | makes it food; `CopyFrom` takes the eating sound and effects from the game's apple |

Without a model of your own the berry looks like the game's diamond; give it one in
[11 Items: models and icons](11-items-models-and-icons.md).

## See it in the game

In 0.1 a player gets your item from a shop. Add a slot with POGShops: [20 Shops: your first slot](20-shops-your-first-slot.md).
Then host a game and walk to the hub's starter shop.

## Check the log

`MelonLoader/Latest.log` in your mod manager's profile. A working item shows these lines (the hash is your item's
own):

```
[POGLib] Registered definition: BerryMod:GlowBerry (hash=1A2B3C4D)
[POGLib] Built template ModItem_BerryMod_GlowBerry (shell=Item_Diamond, hash=1A2B3C4D).
[POGLib] Network prefabs registered: 1 ok, 0 failed.
[POGLib.Items] Journal: GlowBerry listed under Food (shows once discovered; icon the shell's).
```

- No "Registered definition": your mod did not load, or `Register` was not called (look for your mod's own line
  and for errors above it).
- "Registered" but no "Built template": the game's shells were not found on this game build; the log says which.
  Report it with the log.
- Another problem: [90 Troubleshooting](90-troubleshooting.md).

Next: every field an item has, [10 Items: all fields](10-items-all-fields.md).
