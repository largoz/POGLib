# POGLib

A library for content mods for Pit of Goblin: new **items**, with their own model and icon, that work in co-op, and new
**clothes** for players and NPCs.

**Players:** install it when a mod lists it as a dependency (your mod manager does that for you). POGLib adds no
items of its own.

## Multiplayer

- Every player needs the same content mods. [POG Mod Check](https://thunderstore.io/c/pit-of-goblin/p/Largo/POG_Mod_Check/)
  shows a joining player what differs before anything loads.
- A player without a content mod can still join (the game allowed it on build 26668562); that mod's items simply do
  not appear for them. Clothes are not needed by everyone: a player without them sees the goblin without that piece.

## For mod developers

Reference `POGLib.dll`, add `Largo-POGLib-0.1.4` to your manifest's dependencies, and register items in your
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

### Clothes

```csharp
using POGLib.Cosmetics;

Content.Cosmetics.Register(new CosmeticDefinition
{
    ModId = "MyMod", ContentId = "Coat", Slot = CosmeticSlot.Chest,
    ModelFile = "path/to/coat.glb",          // glTF Binary, weighted to the goblin's bones; without weights it sits on the head
    ThumbnailFile = "path/to/coat.png",      // for the game's wardrobe
});
```

They stand in the game's wardrobe, take the player's colours, can be locked until your mod unlocks them, and dress an NPC
(`Content.Cosmetics.LookJson`). How to make the model: the wiki, pages 50 to 52.

### What you can call

Everything public is listed line by line in
[`api/POGLib.txt`](https://github.com/largoz/POGLib/blob/main/api/POGLib.txt); the rest of the library is internal.
What that file lists stays through 0.x, **except the parts marked experimental below**.

## Status of 0.1.4

Shown in the game (the host on build 26791993), on a host and on a client over LAN:

- items registered from code appear for every player with the mod; bought in a shop, eaten, carried, passed
  between players, put in the bucket;
- clothes (`Content.Cosmetics`): worn by players and NPCs, in the wardrobe, in the colours the player chose, hidden from the wearer in first
  person where the mod says so; a locked part under the game's padlock, a click on it refused; a client without the mod, even one whose saved look names the clothes, plays without them;
- your own model (`ItemVisual.Obj`) and icon (`IconFile`); eating (`EatCapability`); a big item (`Big = true`);
  the item's journal entry.

**Experimental** (in the code, not shown in the game yet; may change): `ItemVisual.GameMesh`, `GlowCapability`,
`ParticleEffect`, `MeleeWeaponCapability`, `ThrowableCapability`, `UsableCapability`, `SpawnOnConsume`, `Tint`,
`PickupStaminaPenalty`, `MovementFactorWhenHeld`, `Consumable` / `MaxUses` with a use handler. **Use handlers run on
the host only**: a client's use is not sent to the host yet.

**Saves** (since 0.1.1): a mod item saved in the bucket comes back when the save loads; without its mod the save loads
with no error and the item is gone. (0.1.0 lost mod items from saves.)

Not in 0.1: natural loot drops, AssetBundle models, mobs, levels.

Source, roadmap and issues: [github.com/largoz/POGLib](https://github.com/largoz/POGLib)
