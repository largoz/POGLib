# POGLib

A library for content mods for Pit of Goblin: new **items**, with their own model and icon, that work in co-op.

**Players:** install it when a mod lists it as a dependency (your mod manager does that for you). POGLib adds no
items of its own.

## Multiplayer

- Every player needs the same content mods. [POG Mod Check](https://thunderstore.io/c/pit-of-goblin/p/Largo/POG_Mod_Check/)
  shows a joining player what differs before anything loads.
- A player without a content mod can still join (the game allows it on build 26668562); that mod's items simply do
  not appear for them.

## For mod developers

Reference `POGLib.dll`, add `Largo-POGLib-0.1.0` to your manifest's dependencies, and register items in your
`OnInitializeMelon`:

```csharp
using MelonLoader;
using POGLib.Items;

[assembly: MelonAdditionalDependencies("POGLib")]

public class BerryMod : MelonMod
{
    public override void OnInitializeMelon()
    {
        Content.Items.Register(new ModItemDefinition
        {
            ModId = "BerryMod",                       // your mod's id: never rename it after release
            ContentId = "GlowBerry",                  // the item's id: never rename it after release
            DisplayName = "Glow Berry",
            DisplayDescription = "Tastes like an apple, somehow.",
            ItemType = Il2Cpp.ItemType.Food,
            MapIcon = Il2Cpp.MapIconType.Food,
            Visual = ItemVisual.Obj("path/to/GlowBerry.obj"),   // your own model, with its .mtl beside it
            IconFile = "path/to/GlowBerry.png",                  // square PNG
            GoldValue = 20,
            ShopCost = 7,                                         // the price in a shop (POGShops)
            Capabilities = { new EatCapability { CopyFrom = "Item_Apple", HealthOnEating = 15f } },
        });
    }
}
```

An item's identity is a hash of `ModId:ContentId`, the same on every player's machine. To sell your items in the
game's shops, use [POGShops](https://thunderstore.io/c/pit-of-goblin/p/Largo/POGShops/). A complete sample is
`samples/ShopDemo` in the [repository](https://github.com/largoz/POGLib).

### What you can call

Everything public is listed line by line in
[`api/POGLib.txt`](https://github.com/largoz/POGLib/blob/main/api/POGLib.txt); the rest of the library is internal.
What that file lists stays through 0.x, **except the parts marked experimental below**.

## Status of 0.1.0

Shown in the game (build 26668562), on a host and on a client over LAN:

- items registered from code appear for every player with the mod; bought in a shop, eaten, carried, passed
  between players, put in the bucket;
- your own model (`ItemVisual.Obj`) and icon (`IconFile`); eating (`EatCapability`); a big item (`Big = true`);
  the item's journal entry.

**Experimental** (in the code, not shown in the game yet; may change): `ItemVisual.GameMesh`, `GlowCapability`,
`ParticleEffect`, `MeleeWeaponCapability`, `ThrowableCapability`, `UsableCapability`, `SpawnOnConsume`, `Tint`,
`PickupStaminaPenalty`, `MovementFactorWhenHeld`, `Consumable` / `MaxUses` with a use handler. **Use handlers run on
the host only**: a client's use is not sent to the host yet.

**Not tested yet: saves with mod items** (loading a save made with the mod, and without it). Back up your saves.

Not in 0.1: natural loot drops, AssetBundle models, mobs, levels.

Source, roadmap and issues: [github.com/largoz/POGLib](https://github.com/largoz/POGLib)
