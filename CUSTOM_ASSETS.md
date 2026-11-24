# Custom meshes and visual effects

**Short answer: yes.** You can ship your own model with your own visual effects and use it as a mod
item — through an AssetBundle. There is one hard limit worth knowing up front, because it decides how
you design the asset.

## The one hard limit: no custom C# scripts

The game is **IL2CPP**, which means C# is compiled ahead of time into native code. A `MonoBehaviour`
script you wrote in the Unity Editor **does not exist** in the shipped runtime, so when Unity loads
your bundle it silently drops that component. No error — the object just arrives without it.

What this means in practice:

| Rides along inside your prefab | Does NOT survive |
|-------------------------------|------------------|
| Meshes, materials, textures | Your own `MonoBehaviour` scripts |
| **Particle Systems** (Shuriken) | Anything driven by your own script |
| **Lights** (incl. HDRP light data) | Custom ScriptableObjects of your own types |
| Trail / Line renderers | |
| Animator + Animation clips | |
| Audio sources & clips | |

So **visual effects are fine** — particles, glow, lights, trails, animation are all engine-side data,
not scripts. You only need to move *logic* out of the bundle and into your mod's C# (a use handler or
a capability in POGLib).

## Building the bundle

1. The game's exact Unity version: **6000.3.24f1** on build 26668562 (2026-11-19; it was 2022.3.62f2 with HDRP 14
   before the 2026-11 updates), with the HDRP that editor ships. A different version is the usual cause of magenta
   materials or a bundle that refuses to load. Check the version again after every game update.
2. Build your item as a **prefab**: mesh + materials + any particle systems / lights / animation.
   Do not attach your own scripts.
3. Assign an AssetBundle name to the prefab and build for **StandaloneWindows64**.
4. Ship the `.bundle` file with your mod.

## Using it

```csharp
Content.Items.Register(new ModItemDefinition
{
    ModId = "MyPack", ContentId = "CrystalLantern",
    Visual = ItemVisual
        .Bundle("Mods/MyPack/crystal.bundle", "CrystalLantern")
        .At(Vector3.zero, Vector3.zero, scale: 1f),   // place it inside the item

    // Effects your prefab does NOT already contain can be added by the Lib:
    Capabilities =
    {
        new GlowCapability { Colour = Color.cyan, Intensity = 2f, Pulse = true },
    },
});
```

The Lib instantiates your prefab as the item's visual and disables the shell's own renderers, so only
your model shows. Everything else about the item — icon, price, loot placement, use handler — works
exactly as with a reskin.

### Shader repair

If a material comes through with the magenta error shader (a version mismatch), the Lib re-points it
at the game's own `HDRP/Lit`. Unity keeps every property whose name matches, so base colour and maps
usually survive. It logs when it does this — treat that log line as "rebuild the bundle against the
right Unity version", not as a fix.

Set `RepairShaders = false` on the `ItemVisual` if you would rather see the failure untouched.

## Your own model without Unity (OBJ)

For a static model you do not need Unity at all. Export it as OBJ (with its `.mtl`) and point the item at it:

```csharp
Content.Items.Register(new ModItemDefinition
{
    ModId = "MyPack", ContentId = "GlowBerry",
    Visual = ItemVisual.Obj(pathToObj),          // GlowBerry.obj, GlowBerry.mtl next to it
    IconFile = pathToPng,                        // the inventory icon; without one the item shows the shell's
});
```

The library parses the OBJ and its `.mtl`, builds a `Mesh` with one submesh per material, creates the materials
on the game's own `HDRP/Lit`, and puts them into the shell's own renderers, so the world view, the in-hand view and
inventory hiding keep working (the renderers stay under `PickupItem`/`HeldItem`; see
`docs/design/item-chain.md`). Every load is logged: vertices, triangles, submeshes with their material names,
and collision hulls.

Find the files at run time, not by a fixed path: under Gale a mod's folder is `Mods/<Author>-<Mod>/`, under
r2modman it can sit deeper. `samples/ShopDemo/ShopDemoMod.cs` searches `Mods/` for its model and finds its icon
next to it.

**Exporting.** Use **Y up / -Z forward** (the Blender default) and triangulate. OBJ is right-handed, Unity is
left-handed, so the loader negates Z and turns the triangles over (since 0.1.2; before that a model came out inside
out); the model arrives upright and unmirrored. Export normals: where a face has them, they say which side is out. One Blender unit = one metre =
one Unity unit, so model at real scale (a hand-held item is about 0.1–0.2 m), with the origin at the bottom centre.

**Materials.** `Kd` → `_BaseColor`, `Ke` → `_EmissiveColor`, `Pm` → `_Metallic`, `Pr` → `_Smoothness`
(as `1 - roughness`); `Ns` is the fallback when `Pr` is absent. Blender does **not** write `Pm`/`Pr` by default,
so metals export as matte plastic unless a post-export step adds them.

> **Do not ship fully metallic materials.** A metal is lit almost entirely by what it reflects, and most of this
> game happens in a mine with nothing to reflect: `Pm 1.0` arrives on screen as a black silhouette. Keep `Pm` at
> 0 (or below about 0.2) and put the look in the base colour. Judge a model under dark lighting.

**Collision.** Objects named `UCX_*` (also `UBX_`/`USP_`, or any name containing "collision") are kept out of
the render mesh and become convex `MeshCollider`s. A moving Rigidbody can only use convex shapes, so a concave
object needs several hulls; keep each under 255 faces. A model with no hulls gets the shell's collider refitted
to its bounds: a box around the model, not the model.

**Icon.** An OBJ makes no icon. Set `IconFile` (a square PNG with a transparent background), `IconSpriteName`
(a sprite the game already has) or `Icon`. The sample renders its icon from the model with Blender:
`samples/ShopDemo/model/make_icon.py`.

**In a shop.** A shop slot places its item by the slot's holder, and some holders are posed for a lying item
(the starter shop's torch slot). The library stands a mod item upright there on the host before it spawns, and
every client gets the same pose.

Worked examples: `samples/ShopDemo/model/` (a berry made by a script, its materials and its icon) and the Ember
Lantern in POGCustomItems (several materials, four `UCX_` hulls).

## Other no-editor options

- **PNG icon as the visual** — `ItemVisual.Png("Mods/MyPack/icon.png")`.
- **Reskin a vanilla mesh** — `ItemVisual.GameMesh("Item_SpeakingStone", "SpeakingStone_01")` clones
  an existing game mesh; combine with `Tint` and a `GlowCapability` to make it look different.

## Copying a vanilla look

To match how a vanilla item glows or pulses, capture its real values first — they live in prefab
serialized data and cannot be read from the game's code. Put the prefab name in
`UserData/pog_probe.txt` (one per line), run the game, and read the `VISUAL REPORT` block in
`MelonLoader/Latest.log`: light colour/intensity/range, flicker settings, emissive materials. Then set
those numbers on your `GlowCapability`. See `RUNTIME_TESTS.md` → T3e.

## Status

The bundle path is **implemented but not yet proven in-game** — the AssetBundle round-trip is
Milestone 0.2 in `ROADMAP.md`. Expect to iterate on scale/orientation the first time, and please
report what you find.

## Multiplayer note

Every player who should see your items needs your content pack, your bundle included. On the current game
build (26668562) the game runs with `ForceSamePrefabs = false`, so a player without it still joins, but your
items do not appear for that player. [POG Mod Check](https://github.com/largoz/POGModCheck) shows players what
differs before they load in.
