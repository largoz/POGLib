# Cosmetics (`Content.Cosmetics`): what was decided and why

State: built 2026-11-25, see ROADMAP for what was shown. Facts about the game: `_research/COSMETICS_NATIVE.md`.

## What it is for

A mod adds pieces of clothing (the game's own six groups: horns, hair, chest, legs, feet, accessories) that players and NPCs
wear. First consumer: Black Market (a trader's coat and hat, found by meeting him).

## Decisions

| Question | Decided | Why |
|---|---|---|
| Model format | **GLB** for every part | A standard file that carries the bone weights and the bones' names; every tool writes it. OBJ has no weights, and a weights file of our own would be a format to support for ever |
| Where the model stands | The whole goblin's T-pose, feet at the origin, facing the file's front; head parts too | One rule for every part; the place of the head is read from the skeleton |
| Bones | By name, only `mixamorig:...`; their places in the file do not matter | The game binds by name and knows no others; a tool turns bones as it likes when it imports a skeleton |
| Head or body | By the file: weighted or not; chest, legs, feet must be weighted | The game has the same two ways, and no third |
| One renderer | All meshes of a file become one, a submesh per material | The game colours and takes off the first renderer only |
| Wardrobe | The answer of `GetPartList` is longer; the game's arrays are never changed | Only the wardrobe asks; random goblins are drawn from the arrays, so they never wear a mod's part |
| Lock type | Always Unlocked; what a player may wear is our own list (`UserData/POGLib/Cosmetics.json`, versioned) | Any other type sends the part through the game's list of rewards, in the player's Steam Cloud |
| First person | The game's own rule (chest, legs, feet only), and objects of the model a mod names are left out of the wearer's own copy (the user, 2026-11-25: "there are things that must be made invisible in first person; POGLib must be able to do that") | A collar stands before the eyes. By object, not by material: the first material is the one the player colours. Only the mesh of the first-person copy is swapped, so others see the part whole |
| Identity | A number hashed from `ModId:ContentId`, as items; a clash is refused, not salted | Every machine must get the same number, whatever mods it has |
| Missing mod | The part is not drawn; not network content (`Content.ContentAssemblies` does not name the mod) | The game checks for an unknown part and leaves it out |
| Faults in a file | Refused at the start with a reason; what is not read is named | A part that looks almost right is worse than none |

## Not done

A GLB writer that gives a modder the goblin from their own game; parts on other bodies (the troll, the maiden); normal maps and
transparency; parts an NPC wears for one player only; GLB for items (the reader is there).
