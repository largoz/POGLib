# 11 Items: models and icons

Your own model from an OBJ file, and your own icon from a PNG. No Unity editor needed: any 3D tool that exports
OBJ will do. Both are **shown in the game**.

## The model

```csharp
Visual = ItemVisual.Obj(Path.Combine(folder, "GlowBerry.obj")),
```

Ship `GlowBerry.obj` and `GlowBerry.mtl` side by side in your package. Your mod finds its own folder at run time;
the sample looks for the file under `Mods/` (`samples/ShopDemo/ShopDemoMod.cs`, `ModelPath`).

**What the OBJ may hold**

- One or more objects and several materials: each material becomes a part of the item's mesh.
- **Colours from the .mtl:** `Kd` (colour), `Ke` (glow), `Pm` (metal), `Pr` (roughness; `Ns` is used when there is
  no `Pr`). The materials are the game's own lit material, so the item is lit like everything else.
- **Image textures (`map_Kd`) are not read** in 0.1: colour your model with materials.
- **Collision:** objects named `UCX_…` (also `UBX_`, `USP_`) are not drawn; they become the item's collision shape.
  Hulls must be convex; a hollow shape needs several. Without hulls the shell's colliders are resized to your
  model's bounds: less exact, never worse.

**Export from Blender** with Y up and -Z forward (the exporter's defaults): the model arrives upright and facing
the right way. **Use POGLib 0.1.2 or newer**: 0.1.0 and 0.1.1 draw such a model inside out. The sample's berry is about 7 to 8 cm across in its OBJ and looks berry-sized in the game; start from
that scale. The model replaces the look of the shell (the game item yours is built from), so it is carried, dropped
and shown in shops like the shell.

The model cannot yet be turned inside the item; export it the right way up.

## The icon

```csharp
IconFile = Path.Combine(folder, "GlowBerry.png"),
```

A square PNG with a transparent background (the sample's is 256 by 256). It shows in the inventory, the journal
and the game's pickup messages. Without one, an item with your model shows the shell's icon, which does not look
like your item.

The sample makes its icon from its model with Blender in the background (`samples/ShopDemo/model/make_icon.py`).

## How it stands in a shop

A shop slot puts the item's **standing point** on its holder: by default the bottom centre of your model, so it
stands on its base. Move or turn it with `StandingPoint` and `StandingRotation` in code, or in the shop file's
`items` section ([21](21-shops-the-shop-file.md)). You see a change after the scene loads again.

## When it goes wrong

| Log line | Means |
|---|---|
| `... .obj not found` (your mod's own line) | your path is wrong; log the path you pass |
| `'HDRP/Lit' not found — OBJ materials will be untextured defaults` | the game's material was not found on this build: report it |
| `... the model bounds ...` | your model has no `UCX_` hulls and the shell's colliders were resized to its size |

Model and icon of the sample: [samples/ShopDemo/model](https://github.com/largoz/POGLib/tree/main/samples/ShopDemo/model).
