# 90 Troubleshooting

Everything POGLib and POGShops notice goes to `MelonLoader/Latest.log` in your mod manager's profile, with
`[POGLib]` or `[POGShops]` at the start of the line. Search the log for the words below.

## At start

| Log line | Means | Do |
|---|---|---|
| `Compatibility OK (N game members resolved)` | the game has every member the library needs | nothing |
| `COMPATIBILITY: N/M expected game members NOT FOUND` | the game updated and changed what the library uses | wait for a POGLib update; report the line |
| `Harmony patch FAILED for <class>` | one of the library's hooks did not fit this game build; that feature is off | report it with the log |
| `This POGShops ... is built for POGLib X.Y.x, but POGLib Z is installed` | the two packages do not match; shops stay off | update both to matching versions |
| `Content.Items used before POGLib booted` | your mod registered too early, or POGLib did not start | register in `OnInitializeMelon`; add `[assembly: MelonAdditionalDependencies("POGLib")]` |

## Your item

| Log line | Means | Do |
|---|---|---|
| `Invalid ModItemDefinition` / `Invalid content definition (empty ModId/ContentId)` | an id is missing | set `ModId` and `ContentId` |
| `Post-verify FAILED: <item> not registered` | the item could not join the game's network list | report it with the log |
| `OBJ not found: <path>` / `OBJ load failed` / `OBJ has no geometry` | your model's path or file | log the path you pass; open the file in a 3D tool |
| `MTL not found next to the OBJ` | the .mtl is missing or named differently | ship it beside the .obj, as the .obj names it |
| `a model of its own but no icon` | your item shows the shell's icon | set `IconFile` ([11](11-items-models-and-icons.md)) |
| `EatCapability: CopyFrom '<name>' unavailable` / `no item prefab named` / `has no ActiveItem_Eat` | the food you copy from is misspelt or is not food | use a food of the game, e.g. `Item_Apple` |
| `Capability '<name>' failed on <item>` | a capability could not be set up | read the reason in the line |

## Your shop file (POGShops)

| Log line | Means | Do |
|---|---|---|
| `... pogshop.json: "add" puts items into slots that are not this mod's ...; the file is not used` (also `"patch"`, `"requires"`, `"sellers"`) | a mod's file may only place its own slots in 0.1 | remove those sections ([20](20-shops-your-first-slot.md)) |
| `unknown key '<key>' is ignored (typo?)` | a key the file format does not have | fix the spelling ([21](21-shops-the-shop-file.md)) |
| `item '<id>' ... is not loaded` | the item's id is misspelt, or its mod is not installed | check the id against your `ModId:ContentId` |
| `... has ShopCost 0` | the item would be free | set `ShopCost` |
| `slot <id>: no left end at <seller>: <reason>; not placed` / `cannot go after ...` | the seller's goods stand in no row, or the named slot is in the middle of one | use the other side, or `"anchor"` + `"pos"` |
| `slot <id>: seller '<seller>' not found` | the seller's id is misspelt, or the seller is absent on this visit | see [22](22-shops-sellers-and-scenes.md) |
| `no floor found within 1 m: at the row's height` | nothing solid under the slot's place | use the other side, or place it by coordinates |
| `slot ... is not in <scene>. Closest slot ids here: ...` | a game slot's id is wrong for this scene | copy one of the ids the line offers |

## In co-op

| What you see | Means |
|---|---|
| A player does not see your item at all | that player does not have your mod (the game lets them join anyway); POG Mod Check asks them before they join |
| A player sees your slot but its price tag sits wrong | that player has no POGShops |
| `HostBridge: no handler for '<action>'` | a message from a mod the other side does not have, or another version of it |

Still stuck: open an issue at [github.com/largoz/POGLib](https://github.com/largoz/POGLib/issues) with the whole
`Latest.log` of the host (and of the client, if it happens there).
