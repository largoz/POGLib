# 51 Cosmetics: the model

One GLB file per part ("glTF Binary"). Blender writes it as it is; nothing from Unity is needed.

## Where the goblin stands

Model around the goblin in its **T-pose, feet at the origin, in metres**: in Blender Z up, facing **-Y** (the front view,
numpad 1, looks it in the face). In the file that is +Y up, facing +Z, which Blender's exporter does by itself. Head parts are
modelled in the same place, on the head: POGLib puts them onto the head by itself.

The goblin is small and thin. Measured on its body (half-widths, in metres, at a height):

| Where | Height | Half-width | Depth (front to back) |
|---|---|---|---|
| top of the head | 0.93 | | |
| skull, under a hat | 0.86 | 0.108 | -0.125 to 0.144 |
| neck | 0.63 | 0.021 | |
| chest | 0.54 | 0.101 | -0.055 to 0.089 |
| waist | 0.43 | 0.050 | -0.029 to 0.057 |
| hips | 0.36 | 0.072 | -0.028 to 0.071 |
| shin | 0.14 | 0.083 to 0.126 from the middle | 0.000 to 0.045 |
| upper arm, forearm | 0.58 | 0.030, 0.019 (radius) | |

To model around the body itself, take it out of your own copy of the game with AssetRipper or UnityPy: the mesh is
`CH_Goblin_Default` with 456 vertices, in the bundle `core_networkprefabs`. It is the game's: do not put it into your mod.

**Leave 12 to 15 mm** between the cloth and the body, more where the legs swing. **Shade it smooth**: the game's own
clothes are (84% of their bent edges; the rest are rims, at about 90 degrees), and a faceted coat looks foreign beside them. In Blender: *Shade Smooth by Angle*, about
50 degrees, so that cuffs and rims stay sharp.

## Clothes that move with the body

1. Make an armature with bones of the goblin's names. **Where they stand in your file does not matter**, only the names do.
2. Parent the mesh to it and weight every vertex, at most four bones each.
3. Export: *File > Export > glTF 2.0*, format *glTF Binary (.glb)*, with *Skinning* on (it is by default).

The bones you may weight to, and where they are in the T-pose (Blender's axes):

| Bone | x | y | z |
|---|---|---|---|
| `mixamorig:Hips` | 0.000 | 0.025 | 0.351 |
| `mixamorig:Spine` | 0.000 | 0.023 | 0.403 |
| `mixamorig:Spine1` | 0.000 | 0.022 | 0.464 |
| `mixamorig:Spine2` | 0.000 | 0.020 | 0.534 |
| `mixamorig:Neck` | 0.000 | 0.018 | 0.612 |
| `mixamorig:Head` | 0.000 | 0.010 | 0.706 |
| `mixamorig:HeadTop_End` | 0.000 | -0.009 | 0.927 |
| `mixamorig:LeftShoulder` | 0.035 | 0.018 | 0.602 |
| `mixamorig:LeftArm` | 0.106 | 0.018 | 0.584 |
| `mixamorig:LeftForeArm` | 0.234 | 0.016 | 0.584 |
| `mixamorig:LeftHand` | 0.355 | 0.019 | 0.579 |
| `mixamorig:LeftHandIndex1` | 0.421 | 0.025 | 0.577 |
| `mixamorig:LeftHandIndex2` | 0.439 | 0.030 | 0.576 |
| `mixamorig:LeftHandIndex3` | 0.463 | 0.035 | 0.577 |
| `mixamorig:LeftHandIndex4` | 0.469 | 0.038 | 0.577 |
| `mixamorig:RightShoulder` | -0.035 | 0.018 | 0.602 |
| `mixamorig:RightArm` | -0.106 | 0.017 | 0.584 |
| `mixamorig:RightForeArm` | -0.234 | 0.015 | 0.584 |
| `mixamorig:RightHand` | -0.355 | 0.018 | 0.579 |
| `mixamorig:RightHandIndex1` | -0.418 | 0.023 | 0.575 |
| `mixamorig:RightHandIndex2` | -0.437 | 0.026 | 0.573 |
| `mixamorig:RightHandIndex3` | -0.462 | 0.027 | 0.568 |
| `mixamorig:RightHandIndex4` | -0.467 | 0.030 | 0.568 |
| `mixamorig:LeftUpLeg` | 0.067 | 0.023 | 0.322 |
| `mixamorig:LeftLeg` | 0.094 | 0.019 | 0.184 |
| `mixamorig:LeftFoot` | 0.118 | 0.026 | 0.092 |
| `mixamorig:LeftToeBase` | 0.118 | -0.154 | 0.005 |
| `mixamorig:LeftToe_End` | 0.120 | -0.236 | 0.006 |
| `mixamorig:RightUpLeg` | -0.067 | 0.023 | 0.322 |
| `mixamorig:RightLeg` | -0.094 | 0.020 | 0.184 |
| `mixamorig:RightFoot` | -0.118 | 0.027 | 0.092 |
| `mixamorig:RightToeBase` | -0.118 | -0.154 | 0.005 |
| `mixamorig:RightToe_End` | -0.120 | -0.236 | 0.006 |

Weights to any other bone are refused: the game finds only these.

A part without weights sits on the head and turns with it. `Hair`, `Horns` and `Accessory` may be either.

## Materials

| From the material | In the game |
|---|---|
| base colour, metallic, roughness, emission | yes (the game's own lit material) |
| base colour picture (PNG or JPEG inside the file, first UV map) | yes |
| *Backface Culling* off (double-sided) | yes: drawn from behind too |
| normal map, roughness, emission and occlusion pictures, transparency | no; the log says so |

The first material of the mesh takes the player's colour ([50](50-cosmetics-your-first-part.md)). Its order is the order
of the material slots.

## One file, one part

Several meshes in a file become one. Keep it light: the game's own jackets have 360 to 710 triangles.

Example: the coat and the hat of Black Market are made by a script and exported this way.
