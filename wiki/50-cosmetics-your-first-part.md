# 50 Cosmetics: your first part

A coat, a hat, boots, horns, a belt: your own model, worn by players and by NPCs, with its place in the game's
wardrobe. Needs POGLib 0.1.3 or newer.

**Only the look is added.** A player without your mod sees the same goblin without your part, nothing breaks for
them, and nothing of yours enters the game's own list of rewards. So unlike items, cosmetics are not needed by everyone.

## The smallest part that works

```csharp
using System.IO;
using MelonLoader;
using POGLib.Cosmetics;
using POGLib.Items;

public class MyMod : MelonMod
{
    public override void OnInitializeMelon()
    {
        string folder = Path.GetDirectoryName(MelonAssembly.Location);
        Content.Cosmetics.Register(new CosmeticDefinition
        {
            ModId = "MyMod",
            ContentId = "Coat",
            Slot = CosmeticSlot.Chest,
            ModelFile = Path.Combine(folder, "coat.glb"),
            ThumbnailFile = Path.Combine(folder, "coat.png"),
        });
    }
}
```

How to make `coat.glb` is on [51 Cosmetics: the model](51-cosmetics-the-model.md).

## Every field

| Field | What it is |
|---|---|
| `ModId`, `ContentId` | Your ids: letters, digits, `_`, `-`, `.`. A player's saved look names your part by a number made of the two: **never change them after you release.** |
| `Slot` | `Horns`, `Hair`, `Chest`, `Legs`, `Feet` or `Accessory` (up to six at once). The wardrobe shows your part under it. |
| `ModelFile` | A GLB file. Weighted to the goblin's bones: it moves with the body. Without weights: it sits on the head. `Chest`, `Legs` and `Feet` need weights. |
| `ThumbnailFile` | A square PNG with a transparent background for the wardrobe. The game's own are uncoloured (white and grey). |
| `HiddenInFirstPerson` | Objects of the model (Blender's object names) the wearer does not see in first person, such as a high collar: `{ "collar" }`. Everyone else sees them. See below. |
| `Locked` | `false` (the default): everyone who has your mod finds it in the wardrobe. `true`: only after `Unlock`, see below. |
| `HiddenWhileLocked` | `false` (the default): a locked part stands in the wardrobe under the game's padlock. `true`: it is not there at all until it is unlocked (a secret). POGLib 0.1.4 |

`Register` returns `false` and says why in the log when something is wrong with the part. The files are read at once, so
you see it when the game starts.

## Colours

The player colours your part in the wardrobe, as the game's own: **the first material** of the model takes the hair colour
(`Hair`), the horn colour (`Horns`) or the clothes colour (`Chest`, `Legs`). The other materials keep the colours of
your file. The game colours no `Feet` and no `Accessory`. A hat that should take a colour is best made a `Hair` part.

## First person

The wearer sees their own **chest, legs and feet** in first person; hair, horns and accessories are never drawn there (the
game's own rule). A chest part that comes near the head, such as a collar or a hood, can stand before the wearer's eyes: make it an object
of its own in Blender and name it:

```csharp
using POGLib.Cosmetics;

public class Collar
{
    CosmeticDefinition Coat(string folder) => new CosmeticDefinition
    {
        ModId = "MyMod", ContentId = "Coat", Slot = CosmeticSlot.Chest,
        ModelFile = folder + "/coat.glb", ThumbnailFile = folder + "/coat.png",
        HiddenInFirstPerson = { "collar" },
    };
}
```

The other objects stay, with the wearer's colour. Name all of them and the wearer sees nothing of the part.

## A part to be found

```csharp
using MelonLoader;
using POGLib.Items;

public class Finds : MelonMod
{
    void Found()
    {
        if (Content.Cosmetics.Unlock("MyMod:Coat")) LoggerInstance.Msg("a coat!");   // true the first time only
    }
}
```

With `Locked = true` the part stands among the game's locked parts under its padlock, and a click does nothing, until your mod
calls `Unlock`, on that player's machine, whenever you like: when they meet somebody, find something, win. With
`HiddenWhileLocked = true` as well it is not in the wardrobe at all until then (POGLib 0.1.3 did that with every locked part). POGLib keeps it in `UserData/POGLib/Cosmetics.json`. An NPC can
wear a locked part all the same.

## An NPC in your clothes

```csharp
using POGLib.Cosmetics;
using POGLib.Items;

public class Looks
{
    string Json() => Content.Cosmetics.LookJson(new GoblinLook
    {
        Chest = "MyMod:Coat",                        // yours, or the game's own by name
        Legs = "CC_Goblin_Reward_SlimPants_01",
        ChestColor = 11, LegsColor = 11, Skin = 6,   // indexes into the game's palettes
    });
}
```

`LookJson` writes a look as the game stores it: for a merchant of yours it is the `Look` of
[23 Shops: dungeon merchants](23-shops-dungeon-merchants.md). The game's own parts and palettes are on
[52 Cosmetics: the game's parts and colours](52-cosmetics-the-games-parts-and-colours.md).

Random goblins (citizens, guards, *Randomize* in the wardrobe) never wear a mod's part.

## Common mistakes

| Log line | Means |
|---|---|
| `... is refused: coat.glb: Chest is worn on the body: its model must be weighted ...` | no armature in the file, or the mesh is not bound to it |
| `... 14 of its 932 vertices have no weights ...` | some vertices are in no bone's group; weight them or delete them |
| `... it is weighted to Bone.001 ...` | the bones must have the goblin's names (`mixamorig:...`) |
| `... not a GLB file ...` | export as "glTF Binary (.glb)" |
| `... not read from coat.glb: Cloth: the normal map` | only a note: that picture is ignored |
| `... HiddenInFirstPerson names 'collar'; its objects are 'coat' ...` | the object has another name in the file |
| `... a look names CC_... : the game has no such part` | a wrong name, or the game renamed it in an update |

Next: [51 Cosmetics: the model](51-cosmetics-the-model.md).
