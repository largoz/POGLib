# POGLib — Roadmap

**Renamed from POGContentLib on 2026-11-21** (the user's choice, the `<Game>Lib` name
other games use: LethalLib, REPOLib). The dated records below keep the old name, as the logs of their runs do.

Modular community content framework for **Pit of Goblin** (Unity IL2CPP, MelonLoader 0.7.2,
Unity Netcode for GameObjects). One package, modular inside: a stable `Core` plus per-domain
modules (`Items`, `Mobs`, `Levels`, `Effects`) each with its own versioned data contract.

Design rationale and the reverse-engineering that backs it: see
the content-framework research notes
(§2.5 lists the statically-confirmed facts this roadmap relies on).

---

## Plan to the first release and after (written 2026-11-19)

This section is the order of work. Everything below it is the record: what was found, tried and shown, with the
runs. Where an older section disagrees with this one (the game's `ForceSamePrefabs`, the parity layer, Milestone 0),
this section and the dated records win. The working rules (proof from the game on two machines, backups before a
launch, who may commit, push or publish) are in `_Modding/ModSource/_research/TESTING.md` and at the top of
"Current plan" below.

**Where it stands.** Shown over LAN (PC host, laptop client), game build 26668562: a mod item in vanilla slots and
in a new slot, bought, eaten, passed between players, put in the bucket; a client without the mods breaks nothing;
a new slot reaches every player and only the host needs shop files (R1); POG Mod Check (a separate, published mod)
asks before joining. Not shown anywhere: a client joining through the game's own lobby, a round in the dungeon with
a client, saves, theft, the shop tools under a human's hands, a bundle model. Nothing of the library is on
Thunderstore.

**The rule for scope.** A feature ships in the first release only with a proof from the game on both machines;
otherwise it is left out of the release or cut. What cannot be changed after release is done before it: file
formats, ids, what a weight means, package names and boundaries. Where a choice is open, the stricter or smaller
option goes first if it can be loosened later without breaking published mods.

### Phase 0. Decisions (no code)

- [x] 0.1 Review LethalLib (the task in "Design review" below) and write the findings there. *(2026-11-19)*
- [x] 0.2 Put the three design questions to the user again, with the findings, and write the answers there.
      **Answered 2026-11-19** (after the LethalLib review): a mod may not change another mod's entries, slots are
      private, no `patch` for mods; Shops is a mod of its own and the core decides nothing about balance; the
      host's last word on chances comes with vanilla slots, after the first release (0.3).
- [x] 0.3 A fourth question, new: **may a mod add to vanilla slots in the first release at all?** **The user:
      not in the first release (2026-11-19).** Recommended: no.
      The first release then has items and a mod's own slots at a seller, nothing vanilla is displaced, and what a
      weight means against vanilla content (1.1) does not have to be frozen yet; `add` into vanilla slots comes in
      a later version with the vanilla floor. Adding it later breaks nobody.
- [~] 0.4 Names that cannot change later. Decided: the author in `MelonInfo` is "largo" (as in the user's other
      mods), the first release is 0.1.0 (2026-11-19); **the core is POGLib** (2026-11-21, the user: "rename
      everything to POGLib", the `<Game>Lib` name of LethalLib and REPOLib): the package, the DLL `POGLib.dll`, the
      namespace `POGLib`, the name in the log, the settings categories (`POGLib_Shops`) and `UserData/POGLib`. Done
      in code the same day; nothing of it was released, so nothing broke. **Open:** the names of the Shops and tools
      packages (proposed in the same style: POGShops, POGShopTools). The GitHub repository is `largoz/POGLib` since
      2026-11-21 (renamed on the user's word; the old address redirects).

Done when: the answers and the names are written in this file.

### Phase 1. What freezes at release

- [ ] 1.1 What a weight means against vanilla content: the vanilla floor in place of `VanillaWeight`. *(Moved after
      the first release by the answer to 0.3; it stays here only as a reminder that it is a one-way door then.)*
- [~] 1.2 The shop file, format 1, final. By the answers: a mod's file may define `slots` (with its own pool
      inside each); `add`, `patch` and `requires` are not for mods in 0.1 and get an exact error. To settle here:
      whether the host's own file (`UserData/POGLib/Shops`) may still `add` to vanilla slots, which would
      keep that code path and its tests alive without promising it to mods.
      **2026-11-21:** a mod's file is a file under `Mods/`, a working copy in UserData that replaced one, or a file
      a mod adds from code; it may hold `slots` and `items` only. `add`, `patch`, `requires` and **`sellers`**
      (dungeon merchants change the game's entrance list: R2, not proven, 2.8) refuse the whole file, each reason
      named (`ShopFileIO.CheckModScope`, 5 unit tests; a planted `checks/mods/ScopeCheck.pogshop.json` in the
      asserting run). A file in UserData under a mod id with no packaged file is the host's own and keeps every
      section (proposed answer to the open question, so the code paths and their tests stay alive; SHOPS.md calls
      them the host's settings that may change, not something to publish). **The user has not said yes to that
      part yet.** SHOPS.md and the README describe only this.
- [~] 1.3 R5: slot and seller ids that survive a game update (seller flag + index, the path as a fallback).
      **2026-11-21, from a dump of the hub (run checks-12-scope):** the seller's own list is not in spatial order
      (the starter shop lists key, rock, bread, torch, big item), so "seller + index" would name an arbitrary slot
      and break when the list changes; slot paths carry the default item's name. Since a mod's file in 0.1 names
      the game's slots only to place its own (1.2), the stable form is **`"after": "left" | "right"`**: the end of
      the seller's row on that side, as a customer facing the seller sees it (the seller stands behind its goods;
      the starter shop's seller faces -x, its right end is the rock). It names only the seller (its flag, already
      the seller id). A slot path in `after` stays for a seller with more than one row. Seller ids: the NPC flag
      (unchanged). Still open: host files' paths for `add`/`patch` (host-only now) and dungeon merchants' slots.
- [ ] 1.4 R4: a new slot at a seller as the main path. A slot needs coordinates, and today they come from the
      in-game tools: either the library places a slot by itself next to the seller's own, or the tools ship (1.5).
      Try the first; decide then.
      **The user, 2026-11-21 (LAN run 22, looking at the starter shop):** "why are the goods at his face? Slots can
      be made on the ground." The sample's `berry_stand` sits on the counter, 0.17 m above and 0.3 m behind the
      bread slot (as written: the host's log puts it 0.35 m from the bread slot, the file says 0.38 m, so `pos` is in
      metres as SHOPS.md says; a suspected scale error was checked and is not there). The shopkeeper is short, so
      the counter is at his face. The game's own goods lie in a row on the mat, about 0.45 m apart (torch, bread,
      key, rock at x 8.8, z -11.7 … -13.1). Idea to try: `"after": "<a seller's slot id>"` places the new slot one
      step beyond that slot along the row, at its height and turn (a second one goes a step further), so a modder
      writes no coordinates. The sample also still `add`s to the vanilla bread slot, which 0.3 keeps from mods.
      **Done in code 2026-11-21 (the user: "роби"):** `"after": "<slot id>"` in a slot (instead of `pos`/`anchor`,
      refused together with them), `Shops/Model/SlotRow` (fit of the seller's own slots on the floor plane, a slot
      off the row left out, step = median gap, refused for a slot in the middle or a seller with no row; 8 unit
      tests, calibrated on two planted bugs), `ShopRuntime.PlaceAfter` (the game's own slots of the seller; a
      second slot after the same one goes a step further). The sample's stand now goes after the torch and adds
      nothing to the bread slot (that rule moved to the host's own test files). Found on the way: the parser's list
      of known keys was a second copy of the model and called `after` a typo; it is now taken from the model.
      Run `checks-11-after` (PC): 0 failures; the stand at (8.77, 3.78, -11.30), one step past the torch
      (8.8, 3.8, -11.7), in the row. **Not seen on screen yet, and not over LAN** (the laptop was off).
      **Then the same evening:** the row only at the named slot's height (Curiosa's cog hangs 0.7 m above its table
      row) and at most 1.5 m between slots; `"left"`/`"right"` (1.3); 12 row tests on the dumped starter shop and
      Curiosa, calibrated by swapping left and right. **The look:** the game's standard slot prefab holds its item
      0.75 m up on a small table (that, on the counter, was the "goods at his face"); its other registered prefab,
      `ShopLocationSlim` (3511405297), holds it at the origin. A slot placed with `after` takes the slim one unless
      the file names a `template`. Seen on screen, run `checks-14-floor` (PC): the sample's berry at the left end
      past the torch, a host-file apple at the right end past the rock, both on the floor in line with the shop's
      goods (`look_0.png`, `look_1.png`); 0 failures, 36 checks, among them both holders at the origin
      (calibrated on `checks-13-sides`, where the table one fails it). Over LAN: not yet.
      **2026-11-22, the user looking at the screen: "it is crooked; why not stand it on the ground surface?"** The
      slot took the neighbour's height and tilt (the mat's slots tilt a few degrees), and past the mat the ground is
      higher and sloped: the berry's price tag lay half in the ground. Now a slot in a row stands on the first solid
      surface under its place (a ray from 1 m above to 1 m below, triggers and network objects skipped, so no item,
      slot, NPC or player is taken for the floor), upright, turned like its neighbour; with no floor within 1 m it
      keeps the row's height and says so. Run `checks-17-ground`: 0 failures, 38 checks; the berry on the rug's
      edge (`SM_Prop_Rug_02`), the apple on the floor rock past the rock, both level on screen.
      **Over LAN, `lan-1/run23` (2026-11-23), with the DLLs downloaded back from Thunderstore (POGLib 0.1.0
      2195078…, POGShops 0.1.0 631684b7…, POG Mod Check 0.1.2 355f9309…):** the host stood the stand on the rug past
      the torch; the client's copy took the host's name and position ((8.8, 3.9, -11.3), tilt 0) on the slim prefab,
      showed the berries and the "7" tag in the row on the rug (`run23/lap-shots/client-204248-01-look.png`), bought
      from the bread slot (14 → 7), ate, bought from the new stand (7 → 0) and passed the berry through the bucket to
      the host. No error in the client's log. The store page of POGShops 0.1.0 still says "not yet checked on a
      client": correct it with the next version.
      **2026-11-23, the row model on every seller of the hub, InBetween and DarkBurg** (dumped on the laptop without
      Steam: POGLanTest hosted, the driver waited with `start=0` and jumped): two defects of the published POGShops
      0.1.0. (1) The right end was found on one fit of the row and stepped from on another (refitted at the end
      slot's height), which called Curiosa's and Pitaxas's right ends "the middle" and placed nothing; now one fit
      (`SlotRow.TryPlaceAtSide`). (2) A far slot tilted the line (the Darkburg smith's rack 4 m from its table: "4.25 m
      apart, not a row"); now the line through two slots that most slots lie on, within 0.15 m (`OffLine`; at 0.3 m
      Curiosa's treasure display joined the table). Probe on all 10 sellers: every one gets both ends, the starter
      shop's unchanged. 98 unit tests (Curiosa both ends, the smith; calibrated by loosening OffLine). In game,
      `checks-20-shift`: Curiosa's right end placed, but past the table's edge, in the decoration (the ground under it
      is a candle prop): the automatic place is only a start. **So, the user's point:** `pos` with `after` shifts the
      slot from its automatic place (slot's axes: x sideways, y up, z forward, after it is stood on the ground) and
      `rot` turns it with tilt; seen on screen (a test slot 0.25 m up, tilted 25°). Wiki 22 lists the sellers.
- [ ] 1.5 R3: the shop tools out of the library, into a mod of their own on the public API. **The user, 2026-11-20:
      a separate mod** (only mod authors need them; today 630 lines ship to every player). Packages then: the core,
      Shops on it, the tools on Shops; content mods on the core, and on Shops when they have a shop file. Nothing
      depends on the tools, so they can be released later than Shops, unless 1.4 leaves them the only way to get
      a slot's coordinates. A human has never used them: the user tries them once before they are documented.
      **The user, 2026-11-21: turning the model by hand is hard to get right at once.** Add an "item" mode to the
      tools: T cycles slot → price tag → item; in item mode `[` `]` turn the item about the vertical, `;` `'` tilt
      it, Shift finer; the ghost follows at once and Save writes `items[].standing` (rot). Today only data can turn
      an item on a counter (`StandingRotation`, `items[].standing.rot`, seen after the next scene load), the tools
      turn only a slot, and an OBJ model cannot be turned inside its item at all (`ItemVisual.At` is bundle-only and
      internal): open a turn for `ItemVisual.Obj` when models are proven (2.12).
- [~] 1.6 The package boundary (question 3): Shops as a mod of its own, the core without balance decisions. It
      needs the same public API as 1.5.
      **Split 2026-11-21:** `Shops/` builds into `POGShops.dll` (`Shops/POGShops.csproj`, MelonInfo "POGShops"
      0.1.0, priority -900: after POGLib, before content mods); `POGLib.dll` holds no shop code (`Content.Shops` is
      `POGLib.Shops.ShopsApi` now). POGShops adds its plugin after POGLib has booted (`PluginRegistry.Add` builds a
      late plugin into the running graph and runs its Boot systems), patches its own classes (`HarmonyPatcher`,
      shared with the core), runs its own compatibility probe, and **starts only on the POGLib major.minor it was
      built against** (the core's version is a const, `LibraryVersion.VERSION`, compiled into POGShops; with another
      POGLib it logs why and stays off). It reaches the core through `InternalsVisibleTo`, so the two are released
      together; the rule that keeps that safe: what POGShops uses of the core changes only in a minor version. For
      POG Mod Check it is `same-version` (host and client speak "shops.slot"; a player without it can still join).
      The game references are one list (`GameReferences.props`) for both projects. In game, run checks-10-split
      (PC, 2026-11-21): 0 failures (7/7 driver checks, 29/29 expectations), both mods start, each probe OK (78 and
      11 members), 13 + 7 patch classes, each patch once, the shops behave as before. **Over LAN, `lan-1/run22`**
      (PC host, laptop client, both with POGLib + POGShops 0.1.0, same builds by hash): POG Mod Check lists POGShops
      as "same version" on both and first stopped the client over POG Timer 1.1.0 against the host's 1.1.1 (right:
      the timer is marked so; the client ran again without it). Then the client joined, its slot copy took the
      host's name `POGShopSlot|ShopDemo:berry_stand` at the host's position (SlotSync over HostBridge works across
      the two DLLs), bought a berry from the vanilla bread slot (gold 14 → 7) and ate it (health 100 → 110), bought
      the stand's berry (7 → 0) and passed it through the bucket to the host. No error on either side.
      Still open: whether POGShops gets its own repository before its release (the user's rule: one repository
      per mod), and its settings names (`POGLib_Shops`, `UserData/POGLib/Shops`) with the package name (0.4).
- [~] 1.7 The C# surface: what a pack calls (`Content.Items.Register`, `ModItemDefinition`, the capabilities) is
      listed in the README, everything else is `internal`. `Content.ContentAssemblies` is a contract already: the
      published POG Mod Check reads it by reflection.
      **2026-11-21, the core:** the plugin graph, registries, shells, loot injector, assets, names, probes and
      `ItemsPlugin` are `internal`; POGShops and the runtime driver reach them through `InternalsVisibleTo`. Public:
      `Content` (`Items`, `ContentAssemblies`), `ItemsApi`, `ModItemDefinition`, `ItemShellKind` (moved out of
      `ShellFactory`), `ItemVisual` (only `Obj` and `GameMesh`; `Bundle`, `Png` and the placement fields are
      internal until a bundle is shown), `LootPlacement`/`LootCategory` (moved to `POGLib.Items`), the capabilities
      and their base classes (the extension seam stays open: no privileged core), `UseContext`,
      `ModItemUseHandler`, `HostBridge` (its constructor and plumbing internal), `ModItemHandle` (public only
      because the game's class injection needs it; its members are internal), `Plugin`. Loot left the surface the
      same day (2.7): 144 lines now. `Content.Diagnostics` and
      the content version (`IContentVersion`, a parity-only field) are gone from the surface.
      **The list is held by a check:** `api/POGLib.txt` is the surface line by line; `tools/apisurface <dll> check
      api/POGLib.txt` fails on any difference (`write` records a change that is meant). 160 lines. POGShops has its
      own file (`api/POGShops.txt`, 194 lines), not narrowed yet: that is its own 1.7, with 1.2-1.5.
      Still open here: which unproven pack features stay public (2.12).
- [x] 1.8 Remove what is dead: the Steam-lobby parity layer (no callback fires on this game; POG Mod Check does that
      job), its three patches and its entries in the compatibility probe. **Removed 2026-11-21:** `Core/Parity*.cs`
      (4 files), the three `SteamRuntimeManager` patches, the by-name probe entries, `GameNames.Steam`,
      `Content.Parity`, the content version that only parity read, and the two Steam assembly references.

Done when: unit tests hold each rule, the asserting run is green, and `SHOPS.md` describes only what is left.

### Phase 2. The gate: prove what ships

Each line is a run on both machines with screenshots and both logs, written into the records below.

- [ ] 2.1 Use on a client. An item with a use handler of its own is host-only today (the client's click is put
      off). Send the request through `HostBridge` (it works since R1), or say so in the README.
- [x] 2.2 Saves. A save with mod items loaded with the mod and without it; no run of the save guard is recorded.
      **Done 2026-11-23 (PC, Steam), runs `save-1` … `save-6`, and it found a defect of the published 0.1.0 (D16):**
      the driver (`savetest=buy`) bought the berry as the host and saved like the bucket does: "Succeeded"; the file
      `remote/Save_2.sav` (plain JSON, the oracle) held the berry in `BucketStorage.Items` with ObjectHash 1195225463
      (= 0x473DB177, POGLib's hash of ShopDemo:GlowBerry) next to two torches and a spear. Loading it with the mod
      (`savetest=load`) brought back the torches and the spear **but not the berry** (save-3). Cause, from the game's
      code: `StorageVessel.LoadStorage` finds a saved item in the game's own `NetworkHandler.NetworkPrefabsList`, and
      POGLib registered its prefabs only with NGO. Fix (`NetworkRegistrar.AddToGameList`): the mod prefabs go into
      that list too, straight into its `List` (no second NGO registration). Loading again with the fix (save-4): the
      berry back in the bucket, 0 exceptions in Player.log; the same test failed on 0.1.0, so it tells the two apart.
      Without ShopDemo (save-5): the save loads with 0 exceptions, the vanilla items come back, the berry does not,
      and the file is not rewritten by loading (the berry stays in it until the game saves again: the bucket reaching
      the hub or a round's end). The slot was deleted through the game afterwards (save-6, "Empty"); `remote/` holds
      only `MetaData_3.sav` again. Reward chests were not tested (no mod item reaches one in 0.1).
      **The fix is not released: POGLib 0.1.0 on Thunderstore drops mod items from saves.**
      The player's saves are backed up first and the test uses a slot of its own.
      **2026-11-23, from the game's code (`_research/SAVES_NATIVE.md`, not run):** a slot is plain JSON in Steam
      Cloud (`remote/Save_<slot>.sav`); **nothing is saved without Steam**, so this test needs Steam (not the Wine
      prefix, not the laptop). An item is saved by its GlobalObjectIdHash only, so a mod item comes back with its mod
      and is silently dropped without it (bucket, storage); reward chests throw on it, which `SaveGuard` catches.
      The test: the driver hosts slot index 2, buys a berry, saves with `GameMode_Goblin.SaveCurrentGame` (what the
      bucket does); the save file itself is the oracle (the berry's hash 473DB177 = 1195225463 in `BucketStorage`).
      Then a run with the mod loads the slot and finds the berry in the bucket; a run without ShopDemo loads it with no
      error and no berry. Steam Cloud syncs `remote/`: ask the user before writing a slot file there, and remove it
      afterwards the way the user wants.
- [ ] 2.3 A round in the dungeon with a client: a mod item carried down, used, carried up in the bucket, the round
      ended; a late join in the dungeon.
- [ ] 2.4 G4, the rest: theft, and the seller's death making a new slot's item free.
- [ ] 2.5 G5: a seller that is absent on a visit (hubs draw some sellers at random): the slot is not placed, and
      the log says why.
- [ ] 2.6 G6: a real pack. ~~POGCustomStones rebuilt on the library~~ (the user, 2026-11-21: the stones are not
      ready, only the library is prepared now): POGCustomItems (Ember Lantern: an OBJ model, glow) and the sample.
- [x] 2.7 Loot. A mod item put into a loot table and found by a client; no such run is recorded. Or the injector
      stays out of the first release. **Out of 0.1 (the user, 2026-11-21, on the recommendation):** `LootPlacement`,
      `LootCategory` and `ModItemDefinition.Loot` are internal (16 lines left `api/POGLib.txt`); the injector's
      code stays. Reason: loot adds copies to the game's own tables, so its weight means something against vanilla
      content, the question 0.3 left for after the release. It opens with vanilla slots, and a client's find.
- [ ] 2.8 Dungeon merchants: with a client and after R2, or out of the first release.
- [ ] 2.9 A client joining through the game's own lobby (two Steam accounts): every run so far went around it.
      Needs the user and a second player.
- [ ] 2.10 Open defects: D12 (price tags over empty slots for a player without the mod) and "Smaller defects".
      Each is fixed or written into the README as a known limit.
- [ ] 2.11 A game update shows as one clear log line: every game member a patch uses is in the compatibility probe
      (on 2026-11-14 one was missing), and a test holds that. And the library alone, with no content mod, changes
      nothing in the game: the same network prefab list, no shop or loot change (LethalLib #103 shows what users
      expect).

- [ ] 2.12 Every pack feature that is public has a run, or leaves the public surface before the release (its code
      stays, the user's rule: dead code that will live again is kept). Shown so far: OBJ model, `IconFile`, journal
      group, eating (`EatCapability`), a big item, shop cost and standing point. Not shown anywhere: `GameMesh`,
      `MeleeWeaponCapability`, `ThrowableCapability`, `UsableCapability`, `ParticleEffect`, `SpawnOnConsume`,
      `Tint`, `PickupStaminaPenalty` / `MovementFactorWhenHeld`, `Consumable` / `MaxUses` with a use handler (2.1),
      localisation keys, loot (2.7). Glow: the Ember Lantern loaded with it on both machines (D2); whether the glow
      itself showed is not written down.

Done when: every feature the README promises has its run.

### Phase 3. The first release

**Published 0.1.0 on the user's word, 2026-11-22 ("upload these two mods to TS for now")**, before Phase 2 was
done: POGLib and POGShops, both in the category Libraries. What was done for it: POGShops' public surface cut from
195 to 63 lines (`ShopsApi.AddFile`/`Reload` and the shop file's shape), store pages (`README_STORE.md`) that name
what is shown in the game, what is experimental (2.12's list) and what is not tested (saves, use on a client, a
client and the floor slots), changelogs, manifests (`website_url` of POGShops = this repository, which holds its
source), the README's code sample compiled against the packed DLL, and the packed DLLs run in the game first
(`checks-17-ground`, hashes in its `tested-dlls.sha256`). **Not done before it:** Phase 2 (LAN of the floor slots,
saves, a dungeon round, use on a client), 3.5 (POG Mod Check 0.1.2, which knows POGLib by its name; the published
0.1.1 looks for POGContentLib), a repository of its own for POGShops (the user's rule).


- [ ] 3.1 The modder's first page in the README: an item in a dozen lines, a shop slot, how to see it work; written
      the way POG Mod Check's "For mod developers" is. The sample (`samples/ShopDemo`) builds from a clean clone.
- [ ] 3.2 The promise, in the README: what will not change (ids, the shop file format 1, the listed API) and what
      may (everything else).
- [ ] 3.3 The package: `manifest.json`, the icon (the user's), `CHANGELOG.md` with the date in its header, the
      MelonLoader dependency. The stale parts of this file, of the README and `RUNTIME_TESTS.md` rewritten or
      removed.
- [ ] 3.4 Publish on the user's word. **The user, 2026-11-21: "only the lib goes to Thunderstore"**: POGLib and
      POGShops (counted as part of the library; the user was told so and may correct it), no content pack and no
      sample (POGCustomItems, ShopDemo stay on GitHub). Check the store pages through the API afterwards.
- [x] 3.5 **POG Mod Check 0.1.2 before the library** *(published 2026-11-22, right after the library, on the user's
      word; run checks-18-modcheck with the packed DLL: POGLib and ShopDemo "required, network content through
      POGLib", POGShops "same version")*: the published 0.1.1 finds the library by the old name
      (`POGContentLib`), so with POGLib it would not mark content mods as required by itself. The code knows POGLib
      since 2026-11-21 (not released).

Done when: a clean profile holding only the Thunderstore packages of the library, plus the sample built from a clean
clone of this repository, shows a mod item in a shop to a host and a client.

### Documentation: the wiki (agreed 2026-11-23)

- [x] W1 The map and the page template: `wiki/README.md` *(written 2026-11-23)*.
- [x] W2 The pages marked "for 0.1" in that map, each example compiled against the current DLL. *(2026-11-25: 12 pages up, 4 of 4 blocks build against the published 0.1.1 DLL.)*
- [x] W3 An upload script (Thunderstore's wiki API): creates the pages, then rewrites links to their real ids. *(2026-11-25: run from a logged-in tab; read back from the API, all 12 texts equal the files, 41 links, none to a missing page; POGShops' wiki has its one page.)*
- [x] W4 Whether a link can point inside a wiki page (an anchor): **no**, headings on a wiki page have no id (2026-11-25). Link to pages only.
- [ ] W5 Open effect classes from other mods ("donor" mods): a page (14) and a list (95) only; a registry by name
      waits until content can be described as data, and for a second author (the user, 2026-11-23: "maybe I am
      overcomplicating it").

### After the first release

In this order unless the user says otherwise. Each starts with its smallest experiment, and its data contract is
written after the experiment, not before.

- **Cosmetics, a section of POGLib** (the user, 2026-11-25: "so we can make a section about cosmetics, for POGLib").
  The facts: `_Modding/ModSource/_research/COSMETICS_NATIVE.md`. In this order:
  - [x] C1 A seller in a look of the game's parts and in ours, host and client with and without the mod (runs `goblin-2b`, `goblin-5`,
        `noir-lan-with`, `noir-lan-without`, LAN 2026-11-25).
  - [x] C2 A hat of a mod's own in the wardrobe, on the player, seen by a client with the mod (`cosm-2` ... `cosm-4`, LAN `wear`). The
        players' list of rewards stays as it was (17 before, 17 after the game's own `ConfirmUnlockedCosmetics`).
  - [x] C3 Decided 2026-11-25: random goblins, new players and *Randomize* never wear a mod's part. Held by construction (the arrays
        are never changed, only the answer of `GetPartList` is longer) and by 6000 random looks, none with a part of ours.
  - [x] C4 A coat that moves with the body: GLB with weights by the bones' names. Seen running, sprinting, crouching, jumping (`run-3`, filmed
        on the king's plate; the user: "ideal").
  - [x] **`Content.Cosmetics`** (0.1.3, published 2026-11-26): `docs/design/cosmetics.md`, wiki 50-52, API list +39 lines, none
        gone. 203 unit tests (the GLB reader against files Blender wrote, with planted values; nine planted faults in the reader, all caught);
        the asserting run `cosm-6` on the last build (9e5cb848), 63 checks, 0 failures; the shop checks `checks-25-poglib013` on the same build with
        the **published** POGShops 0.1.3, 0 failures of 41: seven groups worn in the menu, by an NPC and by the player (through the
        network look), the wardrobe's items on the screen, locked and unlocked, the game's colours where it colours and not where
        it does not, refusals with reasons. Black Market 0.0.6 stands on it: 531 lines became 272, no reflection, no arrays swapped.
        **First person** (the user: "there are things that must be invisible in first person; POGLib must be able to do that"): the game
        draws only chest, legs and feet there and keeps the own third-person parts on a layer the own camera does not draw (`LocalPlayerModel`);
        a mod names objects of its model to leave out of the first-person copy (`HiddenInFirstPerson`). Seen: the coat's collar covered
        half the view when looking down, without it the coat and the belt are there (`run-2`).
        **The skirt** went through the legs in a run (seen on the first film): the game has no clothes below the hips to copy. Its weights were
        fitted to the game's own movements outside the game: edges of the legs that cross the cloth 16.2 -> 1.6 on average, in movements
        not fitted to as well (falling 24.3 -> 0.1), stretch 1.48 -> 1.53.
        **Shading:** ours was faceted; the game's clothes are 84% smooth with creases at the rims (149 meshes). Ours too now.
        **Over LAN** (PC host, laptop client without Steam; `merch-lan-all.sh wear|wearbare`, `tests/ClientProbe` dresses each player through the
        look the wardrobe keeps): both players in the coat and the hat, each sees the other in them, in the colour each chose; the merchant the
        same; his clothes unlocked for both by coming near, kept in the file (the next run had them already). A client with none of our
        mods, **whose own saved look names the coat and the hat**: plays without them, sees the host without them, while the host sees the
        client in them. No exception with the game's cosmetics in it on any client (the rest are from the missing Steam).
        (The LAN runs are on 98b8cb47: the last build differs by two guards, a goblin of another kind waking first and a thumbnail that is no PNG.)
        Test clothes are now cut from the body itself (`tests/RuntimeDriver/cosmetics/make_parts.py`, local only); the first ones were plain shapes and the user
        took them for the mod's. Shots of goblins are taken on the king's plate (`Stage.cs`): by the starter shop it is dark (the user, twice).
        Left: see "Not checked" below.
  - **Not checked:** a look that names a removed mod's **accessory** (the code says it keeps one of the six places until *Randomize*); a part on the
    guard, imposter and fungus goblins; a texture seen close up; the wardrobe with the mouse by a person on 0.1.3; saves through Steam.
- **The noir goblin sells in the dungeon, not in the hub** (the user, 2026-11-25: "maybe with some chance he turns up
  instead of a merchant on the level? After all he has contraband. In the lobby he will not sell"). This is R2 with a
  look: after the game has drawn a merchant preset and before its seller is spawned (`SpawnerBase.SpawnNetworkPrefab`,
  host), the host decides by a chance whether it is ours, dresses it and stocks its slots. The entrance list is not
  touched and only the host decides. The user, same day: each look has a chance of its own (plain coat, thief mask,
  bandana over the mouth). **Tried on both machines 2026-11-25** (driver `Noir.cs`; host `noir-1`, `noir-2`, LAN
  `noir-lan-with`, `noir-lan-without`): all six dungeon sellers are goblins of parts; the look is set before
  `NPCEntity.OnNetworkPostSpawn`; the seller's own equipment (the Tinkerer's barrel, weapons) is taken off before
  `EnemyInventory.OnNetworkPostSpawn`; a client that knows our parts sees the same goblin with the name, a client that
  does not sees him without coat and hat under the vanilla name, no error from any of it; looks drawn by weight 5/3/2.
  **Stalls** (they stay the replaced merchant's): Curiosa's tent (wagon, crates) fits best, the Tinkerer's and the
  mystic's work, the smith's (anvils, cages) and the food vendor's do not.
  **A mod of it, `ModSource/POGBlackMarket` 0.0.3 (dev, 2026-11-25):** chance, stalls, weights of the looks and the goods
  (17 vanilla items: magic tomes, scrolls, poisons, dynamite, a shiv; no item twice) as settings; no faction, base price;
  one decision per stall, made by whichever part the game spawns first. It reaches POGLib's OBJ loader and POGShops'
  stocking by reflection: those calls are what the API of C2/C4 and R2 must become. Host, run `blackmarket-3`: the
  Antique stall sold a shiv and two tomes, prices 24/20/20, faction None. The user saw him in their own game.
  Left: buying from him (host and client), his goods on a client, the stones as goods, the chance as a setting
  (proposed 15% of merchants), a floor for the vanilla merchants, theft and death, a player joining late, the API.
- **The user, 2026-11-25 (afternoon):** the noir goblin is an ordinary merchant for now: **25%** instead of the vanilla one;
  he sells **human things** (crossbow, bolts, dagger, ward, the mages' tomes), holds a **dagger**; **the stones go to the
  fortune teller** (Mold Muncher in InBetween: 8 slots, 3 of them Speaking Stones, fixed items; a full lobby is 6): slots
  of our own after her row, hers are not replaced. **His clothes open in the wardrobe of a player who has met him**
  (proposed: within 6 m, his sight is 65 degrees and 20 m, 10 m in the dark), and **the player colours them with the
  game's own tool** ("just in time we extend POGLib"): a chest part takes the clothes palette already; the game colours
  no accessory, so ours follows the clothes colour. Written in POGBlackMarket 0.0.4, **not run yet**: wardrobe arrays
  swapped in only while it is open (random goblins are drawn from the same arrays), found parts in a preference, never in
  the game's list. Then: the same in POGLib (Content.Cosmetics).
  From the files: no seller shoots (the ranged attack state is off in every seller); they stab with the shiv in the left hand.
- **Merchant classes** (the user, 2026-11-25: "the system of weights seems very unstable to me; with many mods there will be
  some chaos. I think I should add a system of classes, so that not a certain merchant is drawn but a certain class, which
  holds several merchants of one mod for example, and then chance decides which of them turns up on the level"). For R2.
  Today's prototype is the bad case: each mod would patch the stall and roll by itself, so two mods can claim one stall
  and the later one only gets what the earlier ones left. Simulated (200 000 draws per case, every mod asks for 25%;
  not the game): the game's own merchant keeps 24% of stalls with 5 such mods and 6% with 10 (one shared list of weights:
  37% and 23%); a mod with 5 merchants against a mod with 1 takes 56% against 11% in a shared list.
  **Decided 2026-11-25** (the user: "almost; I would lower the ceiling to 25% for vanilla merchants. Get to work", first read the
  wrong way round (mods at most 25%), then: "25% to vanilla. To the others 75%. No more. But it can be set in the config"): the game's own
  merchants keep at least 25%, the mods together take at most 75% (`StandardCeiling`), the host's setting.
  One decision per stall, in POGShops, on the host, the game's own draw untouched: (1) the classes' chances add up to the
  ceiling, past it all shrink in proportion; (2) a class by its share; (3) a merchant of it by weights that mean something
  only inside. **One mod = one class** (its ModId) and a class asks for **at most 25%** (both can be loosened later); the host sets
  the ceiling and a chance per class, over 25% too, on a POG Config page. A merchant that cannot come (look or goods missing)
  is left out before the draw, so a stall is wholly a mod's or the game's (the prototype could put contraband on an ordinary
  merchant's stall when the dressing failed). No conditions in the first version but the stalls. Refused: a class id of the
  author's own (`CategoryID`: ten ids would be ten shares), equal weights for all classes (a rare class could not be rare).
  **Built the same day, POGShops 0.1.2 (POGLib unchanged, so no new version of it):** `Shops/Model/Merchants.cs` (pure, 58 unit tests; 22 planted defects, all caught, with a control),
  `MerchantStalls`, `MerchantSettings`, `ShopsPage`; `ShopsApi.AddMerchants`, `MerchantOf` (15 new lines of surface, none gone;
  `api/POGShops.txt` rewritten). `SellerTemplates` is gone: a host's file `sellers` is a class too and no longer
  changes the entrance list. POGBlackMarket 0.0.5 stands on it (three looks = three merchants, no chance of its own, no reflection
  into POGShops).
  **In the game, host, no Steam (`pog-runs/local/merchants.sh`), POGShops 0.1.2 (11717eba) on the published POGLib 0.1.2 (a25916ec), 0 failures:**
  `merch-1-bm` (Black Market at 100% beside a class that cannot come: his coat, name, five different human things with prices), `merch-2-solo`
  (one good, four places empty without price tags), `merch-3-game` (ceiling 0: the game's own merchant and goods), `merch-4-page` (POG
  Config's page: shown, moved, read back from the file on disk, put back), `merch-5-noconfig` (without POG Config: starts, says where the
  settings are), `merch-6-file` (a host's file: `sellers` and `add` into its slot 2), `merch-7-default` (no settings: 25% / 10% / the game's own
  65%), `merch-8-town`, and the asserting run `checks-23-shops012`.
  **Published 2026-11-25:** POGShops 0.1.2 on Thunderstore (the downloaded DLL is the tested one, 11717eba), wiki page 23 and the corrected build
  numbers uploaded (13 + 1 pages equal to the files).
  **Found before the release:** the dungeon merchants' presets stand in DarkBurg too, another one each visit (Smith, Curiosa, MysticTent, Tinkerer seen), and the first
  build let a mod's merchant keep them there (`merch-8-town-defect`: two stalls of two); the user wants none in a lobby. Now only in `GameScene`.
  Also found: the driver's race with POGLanTest (it missed the main menu by 0.1 s and waited for ever), likely the one early local run of seven that hung.
  Forced to 0 or 100%: **no frequency was measured in the game**, the odds are held by the unit tests only.
  **Over LAN, 2026-11-25 (PC host, laptop client without Steam; `pog-runs/local/merch-lan-all.sh`, probe `tests/ClientProbe`: one DLL, the game's
  own objects only, on both machines; build 26791993):** `full`/`full2` (POGLib, POGShops 0.1.2 and Black Market 0.0.5 on both): goods, prices, tags, look and name the same
  on both; the client is refused a thing too dear, buys the cheapest (gold 18 -> 15 on both), the host buys; the merchant killed: reputation of all five factions
  unchanged, what is left costs 0 and a client takes it without paying; his clothes are found by both within 6 m. `bare` (the client
  has none of our mods): the same trade; it sees him without the hat and coat, in the trousers, gloves and mask its game has, under the
  game's name. `solo` (one good, four empty places): found **D18**.
  Left: the other five stalls, a player who joins inside a dungeon, the client side of a merchant's name (Black
  Market does it itself by the coat's id), `sellers` in a mod's own file.
- **D18 (found 2026-11-25 over LAN, in the published 0.1.0-0.1.2; fixed in POGShops 0.1.3): an empty place had a price tag of "0" for every player but the host.**
  The host hid its own copy of the tag and told nobody; the game replicates an item and its price, not "no tag". Every check until then
  looked at the host only, and said "without price tags". `Shops/ShopEmptySlots`: the host also sends the game's own `HidePriceTagRpc` (what a sold item
  uses, so a client needs no mod), a second after the slot spawned and again whenever a player has loaded the scene or joined.
  Control first, the same probe and files: `tags0` (the published DLL) shows "0" on the client at the hub's torch place, at a mod's new place and at four
  places of the dungeon's stall; `tags1` (the fix, a client without mods) and `tags2` (the fix on both) show none, and the 12 other places of the hub and
  the one good of the stall keep theirs. Not D12: there the place has an item, which the client lacks.
  The fixed build (91305752, API list unchanged, 160 unit tests) also passes the asserting host run `checks-24-shops013`, 0 failures of 41. **Published 2026-11-25** (07f2164; the package downloaded from the site holds this DLL).
- **POGShops 0.1.4 and Black Market 0.1.0 (2026-11-25/26, the user: "finish POGShops and Black Market"). Published 2026-11-26** with
  POGLib 0.1.3 (8e7961c; the packages downloaded from the site hold the tested DLLs: POGLib 9e5cb848, POGShops 9c7f7605, Black Market
  dc017fd8, the last run made from those packages as a mod manager lays them out, `pog-runs/rc-curiosa`); wiki uploaded (pages 50-52 new).
  Black Market is **alpha** (the user: items of future mods may join his goods); its own repository largoz/POGBlackMarket, the store page
  shows the three looks in black (`docs/looks.png`, rendered in Blender from our coat and hat on the goblin body read on this machine). From the game's code first: a place that does not run out is the game's own `ItemSpawner.m_setSpawnCopyOnSpawn` (the
  starter shop's bread); the spawner marks the item before it spawns (`InventoryItem.SpawnCopyInsteadOfPickup`, sent with the item),
  `BuyItem` then gives a copy (`SpawnCopyAndPickup`), and the seller's death clears the mark on the host. From the game's files: the
  stalls have 3 places (Antique, Foodlover, Smith, Tinkerer), 4 (MysticTent), 5 (Curiosa), each preset's counter along its own x, and
  the Smith's place of bolts does not run out. All six sellers hold a shiv table in the left hand; the game hides that weapon while
  idle, draws it when hunting, and takes it away at death; a seller's stab does not depend on it.
  API (additive, `api/POGShops.txt` +5): `PoolEntry.Endless` (also `"endless"` in a file's pool), `PoolEntry.With` (code only; weight 0
  = only with another; a companion drawn before gets the entry put in beside it; an entry whose group no longer fits is passed over),
  `MerchantDef.Name` (sent to clients as `shops.seller`, asked with the slot question, so an older host ignores it), `LeftHand`,
  `RightHand` (the game's own equipment tables). Goods now go along the row, so companions stand side by side. 235 unit tests; 25
  planted defects, 24 caught, the 25th changed nothing (the condition removed).
  **D19 (in the published 0.1.2 and 0.1.3; fixed in 0.1.4): a mod's good at the Smith's third place did not run out.** The slot's own
  flag stayed. Control `pog-runs/d19-control` (published 0.1.3, Black Market 0.0.6 at the Smith): its bolts there `copy True`; with
  0.1.4 (`trade4-Smith-bm`) all three places run out.
  **In the game, host, no Steam (`pog-runs/local/trade.sh`, driver `trade=1`):** all six stalls (`trade3-Curiosa-bm` 32/32,
  `trade-Smith-pair`, `trade4-Smith-bm`, `trade-Antique-bm`, `trade-Tinkerer-bm`, `trade4-MysticTent-bm`, `trade4-Foodlover-pair`): name over
  the head, weapon, companions side by side, only the endless good marked, bought twice and still there, a good that runs out sold once,
  after the death what is left taken once. The Foodlover's seller does not draw after one hit, the game's own one neither
  (`trade5-Foodlover-game`). A seller kills a player in seconds (`trade-MysticTent-bm` ended in the lobby). Black Market's page in POG Config:
  moved, saved, put back (`page-bm`). **Over LAN** (`merch-lan/trade`, `late`, `tradebare`): the client sees "Nobody" (POGShops' answer),
  buys the bolts twice (18 -> 15 -> 12) and they stay, sees the dagger drawn; a client without our mods sees the seller's own name and
  trades the same; one who joins inside the dungeon is named and sees goods and marks, but the game makes them a ghost until the next level.
  Not checked: the client's bag counted after buying a copy (gold and the game's "Item purchased" only); a real Steam session.
- **POGLib 0.1.4 and Black Market 0.1.1 (published 2026-11-26, the user: "if a part is locked it should stand there with a padlock;
  POGLib must have it").** The game's wardrobe (`SetContent`): lock type 0 goes to `m_scrollViewContentUnlocked`, every other to
  `m_scrollViewContentLocked`; `LockedReward` is open when its id is in `MetaData`'s list, `LockedAchievement` by the achievement,
  `LockedHidden` is left out unless open; then `CustomizationItem.SetLocked`, which shows the padlock and leaves the toggle
  interactable only when it is on. Ours stay lock type 0 (nothing enters `MetaData`); a prefix of `SetLocked` tells our button by its
  picture, locks it and moves it to the locked section (the divider is counted after the list). `HiddenWhileLocked` keeps the old way.
  Shown (`cosm-8`, `rc2-cosm` from the packages: 67/67): the locked part under the padlock among the locked ones, a mouse click puts nothing
  on, the secret not there, the unlocked one open. Black Market 0.1.1: darker cloth (`_assets/NoirGoblin/cloth.py`, a 160/255 grey picture
  the game's colour multiplies: the darkest clothes colour ~#2E3034), icon and store pictures from the game without its interface
  (driver `clean=1`, `goblinviews`, `runshowturn`). Downloads hold the tested files: POGLib 76592fc9, Black Market 46ce6cd9, coat ec39c895.
  Black Market 0.1.2 the same day: the store picture back to the Blender sheet (darker cloth), no GIF, icon from the game on the plate (the
  user: the Blender icon "is not the goblin itself"); DLL 1ef37187 (only its version changed), checked at his stall (`rc3-curiosa`).
  A display that sleeps stops window shots: `kscreen-doctor --dpms on` and `kde-inhibit --power --screenSaver` around a run.
- **Names** (the user, 2026-11-25): the mod is **Black Market** on Thunderstore; inside (DLL, repository, ids) `POGBlackMarket`.
  The POG prefix is for libraries and tools, not for content mods.
- **Spy goblins, a faction of their own** (the user, 2026-11-25, seeing three noir goblins side by side: "maybe it will
  be a whole faction? Spy goblins? They will trade information of some kind. But for now in the plans"). Not started;
  after the first noir seller works. To find out first: what a faction is in the game (the `Faction` enum, reputation
  per faction) and whether one can be added or an existing one borrowed.
- **D17 (found 2026-11-25, in the published 0.1.0 and 0.1.1; fixed in 0.1.2): an OBJ as modelling tools write
  it is drawn inside out.** The loader negated Z and left the triangles' order alone; the sample's berry hid it (504 of
  its 520 triangles are written the wrong way round). `Core/ObjWinding`: where a face has normals they decide, else the
  order is turned. 4 unit tests; in the game: our hat, inside out in run `goblin-4`, right in `goblin-5`; the berry the
  same as before (`goblin-6-berry`). The Ember Lantern (a normal
  Blender export), read back from its mesh in the game: 0 of 2000 faces outward on 0.1.1, 2000 on 0.1.2 (runs
  `lantern-old`, `lantern-new`, seen on screen too). The asserting run holds it now (`objcheck`). Release run:
  `checks-22-release012` (0 failures of 41), the published POGShops 0.1.1 DLL on POGLib 0.1.2. The sample's berry is
  written the usual way now.
- **Vanilla slots**, if 0.3 left them out: the vanilla floor, `add`, a POG Config page for the host.
- **Bundle models.** Needs the game's Unity editor (6000.3.24f1) installed, with the user's Unity account. First
  the round trip (a cube from a bundle shown in the game), then D5. Mob models and animation wait for this.
- **R2, dungeon merchants** without changing the vanilla list, if they were left out.
- **Mobs** (the facts: `_Modding/ModSource/_research/MOBS_NATIVE.md`). Three levels: a retuned copy of a game mob
  (numbers only), a rewired one (the game's states, the mod's own transitions), a new behaviour (the mod's own
  states). Experiments, each decides the next: (1) a copy of a simple mob with other numbers, spawned on the host;
  (2) a client sees it and fights it; (3) into a level's spawn table inside the danger budget, with the host's
  floor for vanilla mobs; (4) a transition with a managed condition; (5) a state class of the mod's own.
- **Levels.** The first consumer is the user's own labyrinth. Read how the game builds a level before any API.
- **Only when someone asks:** a multiplier per content mod, `"open"` slots, `patch` for mods, codeless packs.

### Beside the library (other repositories)

- [ ] POGConfig, POGCustomStones, POGSpawner probably patch twice the way D15 did: each calls `PatchAll` itself
      and does not say `HarmonyDontPatchAll`. Check each the way D15 was checked (count the patches per method in
      the game), fix, and release POGConfig on Thunderstore. POGSpawner also fails on `Bucket.Depth`.
- [ ] POG Mod Check: D13 (toasts over the window), D14 (F10 is also POGSpawner's key), a join through the game's
      lobby, a gamepad. "Shop files without a DLL" stopped being a difference between players with R1.
- [ ] Repositories for POGBrightMod and POGGraphics; the laptop's test state cleaned at the end.

---

## Current plan (2026-11-19): the library working in co-op, and a mod-mismatch window

Every item is done only with proof from the game on both machines (PC host, laptop client, POGLanTest, laptop sound
off): screenshots of host and client, unit tests green, each defect found goes into this ROADMAP. No publishing or
pushing without the user's command; commits by Largo (noreply), no co-authors; back up saves before launches; undo
the PC's test changes after each session.

- [x] **P1 D8:** a mod item in a vanilla slot stands upright. **Done 2026-11-19 (run 7, 08:46):** host log
      "EmberLantern in '…Item_Torch_Unlit': stood upright (the slot tilted it 52°)", both berries "upright already
      (tilt 0°)"; the lantern stands on the counter in the host's and the client's screenshot
      (`pog-runs/lan-1/run7/host-look.png`, `client-after-pass.png`).
- [x] **P2 Buy and eat:** the client buys the glow berry (gold taken, berry in its inventory with its icon) and eats
      it (berry gone, effect in the log). POGLanTest: a client stuck on "Spawning Character..." rejoins.
      **Done 2026-11-19, run 9 (09:02), client log and screenshots:** gold 14 → 7; a new berry in its inventory
      slot 0, in hand, owner = the client (the bread slot sells copies, D9); its icon is the berry's own (256x256,
      D10); the "New Discovery" and "Item Purchased" toasts show the berry icon
      (`pog-runs/lan-1/run9/lap-shots/client-090304-04-bought.png`); primary action → health 100 → 110, the berry
      destroyed. Rejoin: with `SimulateStuckOnce` the client counted itself stuck 45 s after the hub, left through
      the game's own path (`NetworkHandler.Disconnect`, `NetworkMenu.OnDisconnected` → main menu), joined again
      6 s later, got its player and did the whole test after that. (Run 8: leaving with `NetworkManager.Shutdown`
      alone kept the client in the hub scene, and every rejoin came back without a player: a POGLanTest defect,
      fixed.) A real hang came by itself in run 12 (09:20, the laptop's battery at 6%): no player 45 s after the
      hub, a black screen; the client left through the menu, joined again 9 s later, got its player and ran the
      whole big-item test after that.
- [x] **P3 Pass and bucket:** an item dropped by one player and picked up by the other, and put in the bucket, both ways.
      **Done 2026-11-19 (run 7, 08:48), both logs agree:** the client bought the `berry_stand` berry (gold 7 → 0) and
      dropped it; the host picked it up (owner 0) and put it in the bucket; the client saw it in the bucket (listed
      in `ItemsInBucket`), took it out (owner 1) and put it back; 30 s later the host and the client both see it in
      the bucket, listed, owner 1. The host → client hand-over was through the bucket, not the floor.
- [x] **P4 Client without the content mods** (POGLanTest only): what it sees; neither it nor the host breaks.
      **Done 2026-11-19, run 10 (09:06), both logs and screenshots:** the client (POGLanTest and POG Graphics only,
      645 network prefabs against the host's 648) was let in, NGO refused nothing, and both stayed connected for the
      whole run (three reports each, `clientsConnected=2`). The client's NGO logged "Failed to create object
      locally" four times, for the mod objects: `641707B8` (the `berry_stand` slot), `473DB177` twice (the glow
      berries) and `F65A65F8` (the Ember Lantern); it sees the bread and torch slots empty under their price tags
      "7" and "140" (D12) and no berry stand (`run10/lap-shots/client-090645-01-look.png`). The host logged no
      exception (`Player.log`), its items stayed in place; the client's 6 exceptions are its missing Steam
      (`SteamClient.get_Name`, achievements), as in every client run. Nothing tells either player what is missing:
      that is P7.
- [~] **P5 D5–D7:** bundle model under `PickupItem`/`HeldItem`, journal entries, big-item shell: fix, or record why not.
      D6 and D7 fixed and seen in game (runs 11–12, below). D5 not fixed, and why: see D5.
- [x] **P6 One library:** each file only in the old `_Modding/ModSource/POGContentLib` copy ported or rejected (reason
      written down), then the old copy archived. **Done 2026-11-19.** Ported: the OBJ tier (D2) and the OBJ section
      of `CUSTOM_ASSETS.md` (rewritten for build 26668562; the bundle section's Unity version corrected to
      6000.3.24f1). Rejected: `ShopFactory` and the old `ShopsApi` (the Shops module replaced them; written against
      fields the 2026-11-14 update removed), `CyrillicFallback`, `LocalizationProbe` and the font probes (POGFonts'
      job), `ColliderProbe` and `ProbeObj` (the OBJ loader logs the same facts), and older forms of code this
      repository has in a newer form. The table with each reason is in `_Modding/ModSource/_archive/README.md`; the
      copy is `_Modding/ModSource/_archive/POGContentLib-2026-08` (commit 54158ad there, not pushed).
      POGCustomStones and the old POGCustomItems copy now build against this repository; POGSpawner does too, but
      fails on the game, not the library: `Bucket.Depth` is gone (`WorldProbe.cs:51`).
- [x] **P7 Mod-mismatch window** (a separate mod, own repo `largoz/<name>`): after connecting, host and client exchange
      their mod lists (id, version, DLL hash) by an NGO named message, not the Steam lobby; each mod is "required for
      everyone" (network content through POGContentLib, or listed in a config) or "own only" (the default); a window in
      game lists missing required mods, different versions and extra ones, does not block play, closes with a button,
      and is copied to the log.
      **Shown over LAN 2026-11-19 (run 15, `pog-runs/lan-1/run15`), PC host and laptop client, POG Mod Check 51a118a
      on both:** (1) client without ShopDemo: host logs and shows "Player 1 joined with different mods / Extra:
      ShopDemo 1.0.0", client "Your mods differ from the host's / Missing: ShopDemo 1.0.0" (`host-modcheck-window.png`,
      `client-modcheck-window-3.png`); the game goes on on both; F10 on the laptop closes the window
      (`client-after-f10.png`: pressed 13:41:24, gone at 13:41:42, client still connected). F10 on the PC was not
      pressed: the user was working in another window, and the close is the same code. (2) client without POG Mod
      Check: 15 s after it joined the host logs "Player 2 sent no mod list" and stacks "Player 2: no mod check" under
      the first report (`host-no-modcheck.png`). (3) client with everything: "Mods match player 3 (8 mods there, 3
      required)" and "Mods match the host (12 mods there, 3 required)"; the mods only one side has (POG Fonts,
      BrightMod, Player Limit, POVChanger on the PC) are not reported; no false "no mod list" for player 3. No NGO
      error in Player.log. Not run: a host without POG Mod Check (the client's "host did not answer" path; unit
      logic only). Repository `largoz/POGModCheck`, public, pushed 2026-11-19. Defects found: D13, D14.
      Build history: POG Mod Check 0.1.0 (local git b3b18a0 at first). Named message
      `POGModCheck/List`; "required" = the assemblies that registered content (`Content.ContentAssemblies`, new
      here, f577930) or the config's `Required`; window top right, F10 or its button; log on both sides. Unit tests
      green (19: the list format, every kind of difference, own-only mods never reported). On the PC alone (run 13,
      09:28): it loads, lists the 12 mods with hashes that match the deployed DLLs, marks POGContentLib, POG Custom
      Items and ShopDemo required and the rest own only, and registers its message when the host starts. Not seen:
      the exchange with a client and the window, because the laptop's battery ran out at 09:23 (6% at run 12,
      then off). Next: the laptop on its charger, POG Mod Check on both, a client without ShopDemo (P4 setup), and
      one with everything: screenshots of the window on both machines and the F10 close.
      **Added 2026-11-19 (the user's word):** (1) a mod's author can mark it "required for everyone" without any
      reference to POG Mod Check, with the standard `[assembly: System.Reflection.AssemblyMetadata("POGModCheck",
      "required")]`; (2) the host names a joining player that sends no list within 15 s (no POG Mod Check there),
      so a client without the mod is no longer silent on the host's side; the log says why each mod is required;
      reports stack in the window. Unit tests 31 green, calibrated (the planted attribute removed and two
      `PeerWatch` rules broken: 4 tests fail on their own lines). On the PC alone (run 14, 13:06): a probe mod
      carrying the mark is listed "Mark Probe (its author marks it)"; the host read its player list every second
      for 35 s in the hub with no warning and no NGO error. Not seen: the warning itself, which needs a client
      without POG Mod Check (the P4 laptop setup), so it joins the LAN check above.
      Also to decide with it: D12 (price tags over empty slots for a client without the mod), and shop files
      without a DLL (they add slots, so they are network content, but there is no melon to list).

## Design review 2026-11-19: what the library decides for mod authors (answered 2026-11-19, see Phase 0)

The user's doubt: the library decides things for the mod author and can change the balance of the whole game; with
many content mods it becomes a war for slots and weights; one mod can rewrite another's weights through the library;
maybe slots should be private. Asked for an honest opinion, the answer on all three questions below was "I do not
know yet". **Nothing here is decided or built.** Agreed by the user: do well what cannot be changed after release;
building for needs nobody has yet is a known habit in this project.

Facts:
- **Dilution.** In a vanilla slot with one item, every mod entry at the standard weight 10 is as likely as the
  vanilla item: vanilla keeps 1/(N+1) with N mods (50 % with 1, 17 % with 5, about 1 % with 100). The host's
  `VanillaWeight` setting is a weight, so it has to be retuned with every mod added.
- **`patch`** (`SHOPS.md`, "Rules when several mods meet"): a mod can change entries another mod put in a slot if it
  lists that mod in `requires`; the last `patch` wins; patching vanilla content needs nothing.
- **Scale.** 11 packages for the game on Thunderstore on 2026-11-14, none adds items or shops. The certain consumer
  of the library is the user's own packs.
- **Lethal Company** (the user's notes of 2026-11-19, not checked here): LethalLib registers items and monsters with
  integer weights per moon, one weighted pool, nothing protects vanilla content; big packs fix that afterwards with
  each mod's config and with separate regulator mods (LethalQuantities, SpawnWeightOverrides) that list everything
  and let the pack maker change the weights.
- **The library's rules are the default path, not a lock.** Any mod can still change anything with Harmony. A
  private slot protects from accidents and from the easy path, not from intent.

The three questions, with the recommendation given to the user (2026-11-19):
1. **Who has the last word on chances.** Recommended: the host. A **vanilla floor** replaces `VanillaWeight`: in a
   vanilla slot the game's own items keep at least X % (default 50), the mods' entries share the rest by weight;
   when the weights alone leave vanilla more than X, the weights stand (so one mod at weight 10 behaves as today).
   One slider on a POG Config page (POG Config optional). Later, if asked for: a multiplier per content mod (off,
   x0.5, x1, x2). Fine control stays where it is: a file in `UserData/POGContentLib/Shops` replaces a mod's file.
2. **May a mod change another mod's entries.** Recommended: not in the first release. Mods only `add`: to vanilla
   slots (inside the mods' share) and to their own slots; a mod's slot is private. `patch`, `requires`, the order
   and the history leave the mod-facing format; changing someone's entries is the host's power. If addon makers ask
   later: `"open": true` on a slot, set by its owner.
3. **Shops as a mod of their own.** Recommended: yes, before release. The core does one thing (a mod item works in
   multiplayer) and decides nothing about balance; weights, slots and the settings page ship as a separate mod. The
   seam is there (`ShopsPlugin` is a plugin); the cost is a public API in place of `internal` access (R3 needs the
   same).

The rule behind all three: before release take the stricter or smaller option wherever loosening it later breaks
nobody and tightening it later would. A private slot can be opened later, `patch` can be added later, a split
package can be merged later; the reverse of each strands mods already published. The vanilla floor is the one that
has to be chosen before release either way, because it changes what a weight means.

If taken, the plan changes so: R4 (a new slot at a seller as the main path) comes before more work on `add`; R2 is in
the same spirit (mods stop changing the vanilla merchants' odds), the user said yes to it, and it waits for these
decisions; R3 and question 3 share the public API.

- [x] **Review LethalLib** (https://github.com/EvaisaDev/LethalLib). **Done 2026-11-19** from the source (main
      `bc9d11f`, 2026-04-01), its 67 GitHub issues, the README of its successor DawnLib
      (https://github.com/TeamXiaolan/DawnLib, `2ed587e`, 2026-11-01, by the people who now maintain LethalLib) with
      its weight code (`src/API/Weights`), and LethalQuantities' README. Lethal Company is a Mono game: a mod there
      ships its own MonoBehaviours in an asset bundle, which IL2CPP does not allow here.

      What they do:
      - **Items and enemies go into one weighted list per moon** (`level.spawnableScrap`, `level.Enemies`; an integer
        `rarity` per moon, per "vanilla moons", "modded moons" or all; `Items.AddScrapItemToLevel`,
        `Enemies.RegisterEnemy`). Nothing protects vanilla content: dilution is exactly ours.
      - **The shop is a catalog, not slots**: `RegisterShopItem(item, price)` appends to the terminal's buy list.
        Nothing is displaced, so a war for places cannot happen there. The war for slots is a problem of this game's
        physical counters, and R4 (a mod's own slot at a seller) is the catalog's equivalent here.
      - **Weights are left to each mod's own config**, which every player must have the same (#10: "turn the weights
        to 0 in the config file; everyone needs to have the same config"). Big packs then use **regulator mods**:
        LethalQuantities (all settings off by default; per-moon rarity and amounts for vanilla and modded items,
        enemies, traps; a web editor), CentralConfig.
      - **Enemies**: the library only registers and places them (rarity, inside / outside / daytime, a bestiary
        entry); behaviour is the modder's own `EnemyAI` subclass, which Mono makes free. No behaviour toolkit.
      - Removal and change APIs exist (`RemoveScrapFromLevels`, `RemoveShopItem`, `UpdateShopItemPrice`), for items
        registered through it; #83: removing a shop item shifted the terminal's sales by one, unfixed.

      What hurt their users (issues):
      - **Saves by index**: a save's items turned into other items after mods changed (#5: "the game uses indexing";
        the opt-in fix `EnableItemSaveFix` warns it "may cause save corruptions"). Ours are hashed from `mod:id`,
        which is the right call; keep it and prove it (Phase 2.2).
      - **A library update broke every mod on it**: after 0.14.0 "only the host can see modded items" (#56, #59, a
        network-registration regression); on game version 50 no enemies spawned at all, vanilla ones included (#72,
        several users; #88). (#104, the same on version 72, was closed as probably another mod.) A bug in the shared
        layer breaks vanilla play for everyone. Our D15 was of that kind.
      - **Installing the library alone must change nothing**: #103 blamed it for breaking vanilla lobbies (it was
        another library). Users expect a library with no content on it to be invisible.

      What the successor (DawnLib, "a modern API ... was CodeRebirthLib") changed:
      - **Everything has a namespaced key**, vanilla content included (`NamespacedKey.From("facility_meltdown",
        "geiger_counter")`), and **tags** on moons and content. Weights are given per key, per tag or globally, first
        match wins (`ProviderTable`), and may depend on the context (moon, weather) and change at runtime.
      - **Two layers**: DawnLib, "hands-off" (registration only), and DuskMod, "more opinionated" (bundle loading,
        config generation, content with no code).
      - **Registries freeze** once the vanilla content is in (`OnFreeze`).
      - The README says content made with DawnLib is "likely unsupported" by LethalQuantities and CentralConfig,
        because its weights are dynamic; they paired with one config mod (LunarConfig) instead.

      What follows for this library:
      1. Dilution is real and in Lethal Company it was never solved inside the libraries; players fixed it with
         separate regulator mods, years later. A library that owns its weights should give the user the control
         itself (the vanilla floor, a per-mod multiplier), or publish its weights so a regulator can change them.
      2. Where possible, do not compete for places at all: their shop never had the problem because it appends.
         R4 first; vanilla slots later, behind the floor.
      3. Their own split, after years, is ours: a hands-off core and an opinionated layer on top. It supports
         question 3 (Shops as a mod of its own).
      4. Ids that survive (saves, slots) and a library that is invisible without content are the two things their
         users suffered from most. Both are in the plan: 2.2 (saves) and a test that the library alone changes
         nothing (added to 2.11).
      5. Nothing there lets one mod rewrite another's entries through the library, except the remove/change APIs
         for the mod's own content. That supports question 2 (no cross-mod `patch`).
      6. Mobs: their modders write behaviour as MonoBehaviours, which IL2CPP does not allow. The tooling this library
         would need (states, transitions, injected classes) has no model there: plan it from the game's own FSM
         (`_research/MOBS_NATIVE.md`), not from LethalLib.
      Not checked: the claims in the user's pasted notes about SpawnWeightOverrides; DawnLib's source beyond the
      weights; LunarConfig.

## Pre-release gate — checks and rework before anything is published (review of 2026-11-14)

Nothing of the Shops module is pushed or published. Until the first release the file format and the ids
are free to change; after it they are a one-way door (modders ship files). As of 2026-11-14 Thunderstore
lists 11 packages for the game and **none adds items or shops**: this library is the first of its kind, so
no other content mod exists to share a slot with yet. The multi-mod rules (patch / requires / history) are
built ahead of need; the foundation matters more. The first real consumers are our own content packs.

### Game update 2026-11-14 (BuildID 26652532): the library is broken on it

Found by the first asserting driver run (`tests/RuntimeDriver/checks/`), 2026-11-14:
- **The game moved its spawners to Addressables.** `SpawnerBase` now has `m_networkPrefabRef`
  (`AssetReferenceGameObject`), `m_prefabHandle`, `m_loadedPrefab`, `LoadNetworkPrefab()`, `PreloadNetworkPrefab()`;
  `ShopLocation` has `m_lootTableRef` (`AssetReferenceT<InventoryLootTable>`), `LoadLootTable()`, `CurrentShopItem`,
  `SetShopItemRpc(NetworkObjectReference)`, `ResolveShopItemOnClient()`, and `CreateShopItems()` is a coroutine.
  `m_networkPrefab` and `m_lootTable` are gone: the shop hook throws `MissingMethodException` on every slot (26 in the
  hub) and the game keeps its own choice.
- **No mod item is built:** `Shell not in memory yet: Item_Diamond`, `Network prefabs registered: 0 ok`. Shells are
  found with `FindObjectsOfTypeAll`, and item prefabs are no longer in memory before they are loaded by address. This
  breaks the Items module, so everything else waits for it.
- `ShopLocation.OnNetworkSpawn` ran **once** per slot on this build (26 calls, no second call). G3 may be gone with
  the update; one run, not settled.
- The compatibility probe listed only `ShopLocation.m_lootTable` (1 of 90): `m_networkPrefab` is not in its list.
- Separate, not the library: since the Proton Experimental update of the same night (experimental-11.0-20261112),
  MelonLoader hung at start twice (loader lock, before any log line) and started once with
  `--melonloader.hideconsole`. One launch each way; not settled.

Port first, in this order: Items (load shells by address, or find where the game keeps them), then the shop hook on
the new pipeline (read `CreateShopItems` / `LoadNetworkPrefab` / `HasNetworkPrefab` first), then the gate below.

**Port status (2026-11-14, afternoon), from `explore=1` runs and the asserting run `checks-2`:**
- What the game does now: nothing is in memory at the main menu; `NetworkHandler.EnsureNetworkPrefabsRegistered`
  (called from `WaitForDeferredAssets` right before `StartHost`) loads `Assets/Gameplay/Data/CoreNetworkPrefabs.asset`
  (647 entries, 218 items) by address; NGO registers it at `StartHost` (645 links in the hub). **`ForceSamePrefabs` is
  false.** Addresses are asset paths; the file name is the prefab name. A slot's item is made by the `CreateShopItems`
  coroutine a few seconds after the slots spawn, in one batch: fixed slot `LoadNetworkPrefab → CanSpawn →
  InstantiateNetworkPrefab → SpawnNetworkPrefab`, pool slot `LoadLootTable → GetRandomItem`.
- [x] Items: new stage `GamePrefabsLoaded` (postfix on `EnsureNetworkPrefabsRegistered`) builds the templates from the
      game's list (`Core/GamePrefabs`); the session-start prefix makes the game load it if it has not. In game: built and
      registered (`Network prefabs registered: 2 ok`).
- [x] Shop hook: a chosen slot looks like a fixed slot holding the choice (`ShopChoices` + prefixes on
      `HasNetworkPrefab`, `LoadNetworkPrefab`, `PreloadNetworkPrefab`, `HasLootTable`, `LoadLootTable`). In game: the
      starter shop's bread slot held `ShopDemo:GlowBerry`, read back from the game's `CurrentShopItem`.
- [x] Vanilla content of a slot is read by address (`Core/Addresses`: the fixed item's address, the pool's table loaded
      with its own handle and released).
- [x] Slots without an item reference (a pool slot, a new slot) did not take the choice: the game reads
      `m_networkPrefabRef` itself, so the `HasNetworkPrefab` prefix alone does not reach them. A chosen slot without one
      gets a stand-in reference (the persistent player prefab's GUID); `LoadNetworkPrefab` answers and the preload is
      skipped, so it is never loaded. A new slot placed after the scene's batch also starts its own coroutine
      (`StockIfMissed`).
- [x] Food items threw in `ActiveItem_ReadyItem.OnNetworkSpawn` when a shop spawned them (Unity log only): `EatCapability`
      left the base class fields null. Both capabilities now fill them (`ReadyItemFields`, end pose from the donor).
- [x] **Asserting run `checks-5`: 0 failures** (18 log expectations, 7 driver checks, Unity log free of exceptions): the
      game's own `CurrentShopItem` matches the choice in a fixed slot (mod item), a fixed slot made empty, a pool slot
      (mod item) and a new slot; the warnings, the setting and the registration as above. Host only.
- [x] Dungeon merchants (run `dungeon-1`): the entrance list grew 10 → 110, the Curiosa preset came up as the mod variant,
      and its slot 1 held the chosen mod item (the game's `CurrentShopItem`). The dev tools' round-timer freeze held the
      timer (0.00 s in 8 s frozen, 8.00 s running).
- [ ] Clients, the dev tools under a human's hands, eating a mod food item: not checked on this build.
- Unity's log in the dungeon has one `NullReferenceException` in `DestructibleInventory.SpawnItem → InventoryItem.Pickup`.
  No mod item is in any loot table in that run (the library injects loot only on request), so it is taken for the
  game's own; not checked against a run without mods. The Unity-log expectations now look only for frames of code the
  library feeds (`ShopLocation`/`SpawnerBase`/`ItemSpawner`, `ActiveItem_*`); calibrated: they fail on the run with the
  food bug and pass on this one.
- [ ] Runs stall on the loading screen under heavy load (62 and 28 seen from another session's builds); queue runs
      behind `load < 10`.

### G. Check first (in this order; each one decides something)

- [~] **G1 Two players on the library's foundation** (= Milestone 0.3, never run since v0.1). Decides whether
      prefabs registered at runtime reach a client at all. If they do not, the core needs rework before
      shops do, and the rest of this list changes.
    - **2026-11-19, game build 26668562, LAN (POGLanTest, `_research/lan-test`): they do.** Host = this PC (Steam,
      Gale), client = the Windows laptop (no Steam, r2modman), UnityTransport, the host started through the game's
      own save start with the EOS relay bypassed. Both registered `ShopDemo:GlowBerry` under the same hash
      (473DB177; 647 prefabs each). The client's `CurrentShopItem` matched the host's in all 14 hub slots: the
      bread slot and the new `berry_stand` slot (a network-spawned slot prefab on the client) hold the glow berry
      at price 7, the 12 vanilla slots the same items and prices. Seen on both screens (screenshots in
      `/run/media/tim/Games/pog-runs/lan-1/`). A client joining the running hub (late join) got the same.
    - **Defect found:** on the client, mod item instances carry `hideFlags = HideAndDontSave` (host: `None`),
      inherited from the library's template. `FindObjectsOfType` does not return them there, and by Unity's rules a
      `DontSave` object is not destroyed when a scene unloads (not observed yet). Clear the flags on spawned
      instances.
    - Not done yet: a purchase or a theft by the client, eating, passing an item between players, a client
      without the mods. POGLanTest has a client buy-and-eat step (`[LAN-BUY]`: `Interact` on the bread slot's berry,
      `ChangeSlot`, the primary action); its one run did not reach the purchase, see the next line.
    - Run 5 (2026-11-19 04:18): the client joined while the host went from the menu lobby into the hub, NGO threw
      `Server Scene Handle (-24508) already exist! ... scene load of PitOfGoblin_Lobby` on the client, and the client
      stayed on "Spawning Character..." (no local player). Run 4 joined at the same point without it. Not the library
      as far as the logs show; the test tool should join only once the host is in the hub, or retry.
    - Client-side noise, not the library: without Steam the client logs Steamworks/LootLocker NREs, and a
      `PushToTalk`/Dissonance NRE every frame. Joining before the main menu is up leaves the client's player
      half-built (no camera); POGLanTest now waits for the menu.
    - Try two instances on one machine first: one through Steam, one in the offline wine prefix (testing
      notes §3.2). The game has `NetworkHandler.HostServerDebug` / `JoinServerDebug` and `HostDiscovery` /
      `JoinDiscovery` (LAN); they are in the proxies, whether they work is unknown. Memory for two
      instances on a 16 GB box is unmeasured.
    - Otherwise: a second PC with a second Steam account (`RUNTIME_TESTS.md`).
- [x] **G2 Dump the game's network prefab list** at session start (the driver prints
      `NetworkManager.NetworkConfig.Prefabs`, name + hash). Decides whether a new slot prefab is needed at
      all: if the standalone vanilla slot prefab (the `ShopLocation` the templates are copied from) is
      already registered, the host can spawn that one as is (see R1).
      **Done 2026-11-19, game build 26668562, asserting run `pog-runs/checks-6` (host only, 0 failures, 7 driver
      checks):** it is. 649 prefabs registered; the game's own list holds two standalone slot prefabs, `ShopLocation`
      (1055139892) and `ShopLocationSlim` (3511405297), both with a price tag and an item holder, both registered;
      the library's templates are "copied from 'ShopLocation'". Against `checks-5` (2026-11-14, build 26652532): the
      same two hashes, nothing removed or renamed, one prefab more (`ModItem_ShopDemo_BerrySack`, the sample's newer
      item). The library still registers one prefab of its own per mod slot (`POGShopSlot|ShopDemo:berry_stand`,
      1679230904): that is what R1 can drop. Not checked here: the list on a client by hash (a client without the
      content mods had 645 against the host's 648 in run 10, three fewer = the three mod prefabs of that day, so by
      count it holds the vanilla ones); a runtime spawn of the vanilla `ShopLocation` reaching a client (R1's first
      step).
- [x] **G3 Why `ShopLocation.OnNetworkSpawn` runs twice per hub load.** Log a stack trace, `IsSpawned` and
      the frame in the prefix. Today a memo hides the symptom. Decides whether slots are re-spawned during
      a session and what "one roll per spawn" really means.
      **2026-11-19: it does not, on the last two builds (host).** The asserting runs' trace (`G3` lines):
      `checks-5` (2026-11-14, build 26652532) and `checks-6` (2026-11-19, build 26668562) each saw 14
      `OnNetworkSpawn` calls on 14 distinct slot objects in the hub, at most one per object, each already spawned
      (`isSpawned=True`). The double call of 2026-11-12 came from an older build and is not reproduced. So "one roll
      per spawn" is one roll per slot object per hub load; the memo (one choice per slot object per session) stays
      as a guard. Not checked: a client, a dungeon scene, the hub reloaded after a round.
- [ ] **G4 A purchase.** A mod item from a vanilla slot and from a new slot: gold taken, the item in the
      inventory and usable, reputation up by round(price × 0.3). Then theft, and the seller's death making
      a new slot's item free.
- [ ] **G5 Random sellers in permanent locations.** Seen 2026-11-12: `InBetween_RandomShopkeeper_Bango_01(Clone)`
      in InBetween and `EntranceCave_Shopkeeper_Foodlover(Clone)` in DarkBurg, so hub sellers are drawn from
      a list too. The mechanism was not read (`ScenePropSpawner` is the likely place). Matters twice: a
      slot's anchor or seller may be absent on a given visit, and it is the route to mod sellers in hubs.
- [ ] **G6 A real pack instead of the sample.** Rebuild POGCustomStones on the current library (the "Port
      POGCustomStones" item below): the API on real content, use handlers included. Eat a food item built
      with the rewritten `EatCapability`.

Before any launch: copy the Steam remote save folder and the save folder in the prefix. The 2026-11-12 runs
had no backup. What can be said afterwards: no new save files appeared, Steam's stats cache was last written
on 2026-11-10 (before the runs), and the account metadata file was rewritten with the same size (cosmetics
and settings only; its content before and after was not compared).

### What the 2026-11-12 run did not verify

- **The distribution.** "50 % / 50 %" in the logs is the computed chance. No frequency was measured.
- **A purchase, a theft, a seller's death.** Only the "Buy" prompt was seen.
- **The tools under a human's hands.** Every action was called from code by the test driver. No key and no
  button was pressed; the panel was only drawn. (Legacy `Input` works in this game: POG Config uses it.)
- **Eating.** `EatCapability` was rewritten and its item was never eaten.
- **A client.** Nothing.

### R. Rework (decide after G1–G3)

- [x] **R1 Shop files as host-only data.** Today each new slot is its own network prefab, so every player
      needs identical shop files or the join fails with no explanation (ForceSamePrefabs). *(2026-11-19: the game
      now runs with `ForceSamePrefabs` false, so the join does not fail: a player without the file joins and the
      slot is simply missing for it, NGO logging "Failed to create object locally" (run 10). G2: the vanilla
      `ShopLocation` is registered on its own, so the slot can be that prefab, its id and price-tag offset sent in a
      message.)* The tools'
      working copy in `UserData` turns that into a trap: save a draft, forget it, and you cannot join a
      friend. Target: one shared slot prefab (or the vanilla one, G2), with the slot id and the price-tag
      offset sent host → clients in a named message (`CustomMessagingManager`, the `HostBridge` item),
      late joiners included. A client without the file still sees the slot.
      **Done 2026-11-19 (the user's word: "так, роби R1 і R2").** A new slot is a copy of the game's `ShopLocation`
      (network hash 1055139892) with the donor's references cleared and its price tag posed; it keeps the game's
      hash, so nothing is registered (`SlotTemplates`). A client with the library asks the host about each runtime
      copy of a slot it receives; the host answers with the slot id, the price tag's local pose and whether it is
      hidden (`SlotSync`, `Model/SlotInfo`, over `HostBridge`, which now registers at session start and writes
      strings byte by byte). Templates can be built during a session. 7 unit tests for the message (71 in all).
      Checked: asserting runs `checks-7` and `checks-8` (0 failures; the network list holds 648 prefabs, no
      `POGShopSlot`, new expectations calibrated on the `checks-6` log, where they fail). LAN `lan-1/run20`: (A) a
      client with every mod sees the stand, the berry and the price tag as the host does (`host-…-look.png`,
      `client-A-look.png`); (B) a client with the library and no shop file ("0 new slot(s)") gets the slot from the
      host, tag posed, berry absent (the item is ShopDemo's) (`client-B-look.png`); (C) a client without the
      library: the slot is created (NGO fails only on the three mod items, not on the slot, unlike run 10), its tag
      where the game's slot has it, on top of the stand (`client-C-look.png`). `run21`: with the fixed library on
      both, one answer per slot; the client found the stand by its id from the host and bought its berry (gold 7 → 0),
      then the pass through the bucket. Not checked: a slot placed by the dev tools during a session; the tag pose
      compared by numbers (the posed tag is out of the camera's view; host and client agree in what is visible).
- [ ] **R2 Dungeon merchants without touching the entrance list.** Today a merchant adds copies of its preset
      to `PropList_EntranceCave_MerchantOrAdventurer`. Every mod then shrinks the dead adventurers' share
      (4 of 10 → 4 of 20 with ten mods), and by the code read the same seed draws a different entrance
      than vanilla. Choose the variant only after the game has drawn the preset. Cost: a mod merchant
      appears at most as often as its preset.
- [ ] **R3 The tools as a separate mod** on the public API. Today 630 lines of IMGUI ship to every player and
      reach into internals. The switch belongs on a POG Config page (as the design said), not in a
      hand-edited cfg. Add the standing-point editor: the `items` section exists in the format and no tool
      writes it.
- [ ] **R4 "Sell at seller X" as the main path.** `add` into a fixed vanilla slot displaces the vanilla item: at
      weight 10 the starter shop has no bread half the time. Main path: the library places a new slot at
      the seller. `add` into someone's slot stays for deliberate competition, with a warning in `SHOPS.md`.
- [ ] **R5 Vanilla ids that survive a game update.** A slot id holds the object path and the default item's
      name; slot counts already changed between builds. Address a slot as seller flag + index, keep the
      path as a fallback.

### Silent failures (a modder gets no log line today)

Done 2026-11-14 (`Shops/ShopChecks.cs`, `ShopCatalog.CheckModSlotTargets`), each logged once per run with the
closest existing ids. In game = seen in the asserting run on the 2026-11-14 build.
- [x] A rule targets a vanilla slot id that matches nothing in its scene: warned 5 s after the scene's slots spawned,
      closest ids listed (in game). A rule for a mod slot the mod does not define: warned at load (in game, unit test).
- [x] An unknown item id: warned before the session (mod items) or when a slot uses it (in game); the dev tools show
      the slot's notes. **Wording wrong for one case (fixed 2026-11-14, not seen in game since nothing is unbuilt now):** an item that is registered but whose template was not built
      (2026-11-14: every item) is reported as "not installed, or misspelt". Ask the Items module and say which.
- [~] `ShopCost` 0: warned before the session and when a slot uses it. **Not seen in game:** the test item could not
      be built on the new game build.
- [x] The vanilla weight is a player setting, `[POGContentLib_Shops] VanillaWeight` (host, next scene; read in game).
      Its effect on a pick is unit-tested only: the pick itself is broken on the new build.

### Smaller defects

- [x] **One weighted pick** (2026-11-14): `Model/WeightedPick.Index`, used by `ResolvedSlot.Pick`, `ShopRuntime` and
      `SellerTemplates`. Unit tests measure the distribution (400 000 draws) and the half-open boundaries; each was
      shown to fail on a planted defect (closed interval; negative weights counted).
- [x] The slot memo, the empty-tag set and the merchant variant are keyed by `GetInstanceID()` (2026-11-14; compiles,
      not exercised in game: stocking is broken on the new build).
- [ ] Standing point: the "bottom centre of the mesh" branch is probably dead. The four prefabs checked have
      their standing point at the pivot (other shells not checked), which is wrong for an item whose model
      was replaced. Recompute when the visual was replaced.
- [ ] A pool slot touched by a rule goes through the fixed-item path: with a dead seller it stays empty where
      vanilla would still draw from the pool (from the code read; not tested).
- [ ] `EatCapability` fields became nullable: packs built on 0.1.0 must be rebuilt (binary-incompatible).
- [x] The runtime driver asserts (2026-11-14): `checks=1`, `checks/expect.txt` checked by `expect.sh`, `run.sh` exits
      with the number of failures and refuses a log older than the launch (a stale log had passed three checks).

### Defects found 2026-11-19 (LAN test, the decompile, the old library copy)

Open unless marked. "In game" = seen on build 26668562; "from the code" = read, not run.
- [x] **D1 Client copies of mod items carry `hideFlags = HideAndDontSave`** (in game, on the client; the host's copies
      have `None`). `ShellFactory` sets the flags on the template itself, not only on its holder (`SlotTemplates` sets
      them on its holder). On the client `FindObjectsOfType` misses those items, and Unity does not unload `DontSave`
      objects with their scene (not observed yet). Fix: clear the flags on every spawned instance of a mod prefab
      (items and slots), and check host and client both report `None`.
      **Fixed 2026-11-19** (`Core/TemplateCopies`, called from the item and slot spawn hooks). In game, next run: host
      and client report `None` for both glow berries, the lantern and the `berry_stand` slot, and the client's
      `FindObjectsOfType` now finds the 2 berries it missed.
- [x] **D2 OBJ models are lost from this repository.** The 2026-09 copy of the library in
      `_Modding/ModSource/POGContentLib` has `Core/ObjMesh.cs` (OBJ → mesh at runtime, several materials, `UCX_`
      collision, handedness), `ItemVisual.Obj`, `ItemVisuals.ReplaceMeshes(target, mesh, materials)` (the mesh goes
      into the shell's own renderers, so it stays under `PickupItem`/`HeldItem`) and `ItemVisuals.ApplyColliders`.
      This repository has none of them, and POGCustomItems 2.0.0 (Ember Lantern, `ItemVisual.Obj`) cannot be built
      against it (from the code). The same copy also has `ColliderProbe`, `CyrillicFallback` and `LocalizationProbe`,
      not here; decide each. Two copies of one library diverged: keep one.
      **Restored 2026-11-19:** `ObjMesh`, `ItemVisual.Obj`, the mesh swap with materials, `ApplyColliders`, and
      `GlowCapability.MaterialFilter` (lost too); `PhysicMaterial` is `PhysicsMaterial` in Unity 6. POGCustomItems builds
      again (its `.csproj` used paths relative to the old Windows layout; now `GameDir`). In game, host and client: the
      Ember Lantern (1116 vertices, 3 materials, 4 `UCX_` hulls) and the glow berry load, the mesh goes into the
      shell's 2 renderers, and both stand in the starter shop on both screens. Still to decide: `ColliderProbe`,
      `CyrillicFallback`, `LocalizationProbe`, and 158 lines of `DiagnosticsApi` that only the old copy has.
- [x] **D3 The ShopDemo sample has no model**: its glow berry is the diamond shell tinted green, so on screen it is a
      green gem (in game, host and client). A sample should show a custom model. **Fixed 2026-11-19:**
      `samples/ShopDemo/model/GlowBerry.obj` (a berry cluster made by `make_glowberry.py`), shipped next to
      `pogshop.json`; seen on host and client.
- [~] **D4 An item without `DisplayName` keeps the shell's tooltip id** (from the code): it is called "Diamond" and
      counts as discovered with the diamond. Also in v0.2 below. **Fixed in code 2026-11-19** (always our own keys,
      the content id as the name); an item without a name was not run in game.
- [ ] **D5 `AttachBundlePrefab` parents the model under the item's root** (from the code): visible in the inventory,
      wrong layer in the hands, and the shell's renderers come back on `SetVisibility(true)`. Also in v0.2 below.
      **Not fixed (2026-11-19), and why:** the fix (the model under `PickupItem`, a copy under `HeldItem`, the
      shell's renderers removed rather than switched off) can only be shown with a real AssetBundle, and none
      exists: building one needs the game's Unity editor (6000.3.24f1), which is not installed here and needs the
      user's Unity account. The whole bundle tier is unproven on this build anyway (Milestone 0.2). The recipe
      stays in the v0.2 list; do it together with the first bundle.
- [x] **D6 Mod items are never in the journal** (from the code: the game builds no item entries). Also in v0.2.
      **Fixed 2026-11-19** (`Items/JournalEntries.cs`): each journal tab's entries are placed in its UI
      (`UI_JournalGroupItems.m_entries`); right before a tab initializes, every mod item for that tab gets a copy
      of one of its entries with the item's `JournalEntryDataItem` (name and description terms, icon, values) and
      picture. New: `ModItemDefinition.JournalGroup` (default from `ItemType`) and `ShowInJournal`. In game: run 11,
      host and client, the berry listed under Food, shown after it was bought (client) and picked up (host);
      run 12, the client's journal opened at the sack, its name and description in the detail page
      (`run12/lap-shots/client-092219-04-journal.png`). An item with no icon of its own now takes its shell's for
      the entry (the sack showed another entry's picture; built, not run yet).
- [x] **D7 No big-item shell** (from the code): `Big = true` keeps the diamond's grips and size. Also in v0.2.
      **Fixed 2026-11-19:** `ItemShellKind.BigItem` = `BigItem_Decoration_01` (the starter shop's fur roll: grips
      for both hands, `ActiveItem_Throwable`; its `BucketUpgrade` is taken off the template, or the mod item would
      decorate the bucket). `Big = true` on the default shell picks it. The sample has a big item (Sack of Glow
      Berries). In game, run 12: built on that shell on both peers, sold in the decoration slot; the client bought
      it (gold 14 → 7): in hands, `m_isBigItem` true, `BucketUpgrade` 0, grips LeftHand/RightHand/Generic/
      Backpack/Table/ItemRack; dropped again. The vanilla big items, for reference: `BigItem_Decoration_01` and
      `BigItem_TreasureDisplay` carry `BucketUpgrade`, `BigItem_Scrap_Cog` is the game's 'BigCog'.
- [x] **D8 A mod item in a vanilla slot takes the pose that slot gives its own item** (in game, host and client,
      2026-11-19): the Ember Lantern lies on its side in the torch slot, where the torch, the key ring and the rock lie
      flat too; the glow berry stands upright in the bread slot. The game places the item by the slot's holder
      (`ShopLocation.get_SpawnTransform`), so a holder posed for a lying item lays down anything. Not the mesh: the
      diamond's mesh child was turned only 6.6° off upright (`ItemVisuals.ReplaceMeshes` now sets a custom mesh upright
      to the item, logged; that alone did not change the picture). Fix: on every peer, after the slot places a mod item
      (`SetShopItemInternal`), keep the holder's position and yaw and stand the item upright, or take the pose from the
      shop file's `items` section; then the standing point at the model's bottom centre (Smaller defects).
      **Tried 2026-11-19, did not work:** a postfix on `ShopLocation.SetShopItemInternal`
      (`Patch_ShopLocation_SetShopItemInternal`, still in the code) never ran on the host: the decompile has one call
      site, the client's `ResolveShopItemOnClient`; the host sets its item another way. The host decides the pose
      (it is synced to clients), so the next try is there: the spawn pose (`ShopLocation.get_SpawnTransform`, used
      when `SpawnerBase` instantiates the chosen prefab).
      **Tried again, did not run:** a postfix on `SpawnerBase.InstantiateNetworkPrefab` logged nothing (run 6;
      `CreateShopItems` reaches it through the vtable). **Fixed 2026-11-19:** the pose is `get_SpawnTransform` =
      holder × inverse(item's `m_standingTransform`), and `CreateShopItems` then calls
      `ClientNetworkParenting.SetParentBeforeSpawn(slot)`, which records the pose relative to the slot that every
      client applies. A prefix there stands a mod item upright about its standing point. In game, run 7: P1 above.
- [x] **D9 Buying a mod item from a slot that sells copies takes the gold and gives nothing** (in game, run 6:
      gold 14 → 7, the berry stayed in the slot). Such a slot (`ItemSpawner.m_setSpawnCopyOnSpawn`, e.g. the starter
      shop's bread slot) hands the buyer a new item via `InventoryItem.SpawnCopyAndPickup`, which asks
      `NetworkHandler.GetNetworkPrefab(hash)`; that walks only the game's own prefab list, so NGO logged "The
      NetworkObject prefab parameter was null!" and `BuyItem` threw after `PurchaseLocal` (host `Player.log`).
      **Fixed 2026-11-19:** a postfix answers a mod hash the game's list does not know with the mod's template
      (`Patch_NetworkHandler_GetNetworkPrefab`). In game, run 7: the copy reached the client's inventory (P2).
- [x] **D10 An item with an OBJ model has the shell's icon** (in game, run 7: the client's "Added to …" toast shows
      the diamond). `ItemVisual.Obj` makes no icon and the sample sets none; only `Icon`, `IconSpriteName` or a PNG
      visual change it. **Fixed 2026-11-19:** `ModItemDefinition.IconFile` (a PNG of the mod's own; logged when
      loaded; a model without any icon logs a warning), and the sample ships `GlowBerry.png`, rendered from its
      model by `samples/ShopDemo/model/make_icon.py` (Blender, headless). In game, run 9: host and client load it
      (256x256); the client's purchase toasts and the host's "Added to Bucket" toast show the berry.
- [ ] **D12 A client without the mod sees price tags over empty slots** (in game, run 10: "7" and "140" in the
      starter shop, nothing above them). The slot is a scene object every peer has; only its item is a mod
      prefab the client cannot create. Nothing in the library can run on such a client; the host could keep mod
      items out of vanilla slots when a peer lacks the mod, once it knows (P7's mod list). Decide with P7.
- [x] **D15 Every Harmony patch of the library ran twice** (found 2026-11-19 by R1: a client asked the host twice
      about one slot). `Plugin` patches class by class, and MelonLoader patched the whole assembly too, because the
      assembly did not say `[assembly: HarmonyDontPatchAll]`: all 26 prefixes, postfixes and the finalizer ran twice
      (StartHost/StartClient/Shutdown hooks, the shop and item patches, the item's primary action). Fixed with the
      attribute. `CheckEachPatchOnce` logs at the first scene whether any patch is there more than once; calibrated
      on the laptop: "26 patch(es) more than once" without the fix, "each patch once" with it (log hash matched the
      DLL); the asserting run expects "each patch once". What the doubling changed before is not re-measured: the
      shop choice had its memo, the berry's eating stopped on "already consumed"; a mod item with its own use handler
      may have fired twice.
- [ ] **D13 The game's toasts cover POG Mod Check's window** (run 15, laptop: at 13:35 an "Added to … Glow Berry"
      toast in the top right hid the window; at 13:38, the toast gone, the window was there again). The game's
      canvas draws over the IMGUI window. Move the window out of the toasts' corner (top centre, or below them).
- [ ] **D14 POG Mod Check closes on F10, which POGSpawner also uses** (its panel toggles on F10, per MAP.md): with
      both installed, closing the report also toggles the spawner. POGSpawner is internal; make the key a setting.
- POGLanTest (the test tool), run 15: the laptop's screenshots covered only the top-left 1536x864 of its 1920x1080
  screen (125% scaling, a DPI-unaware PowerShell), so the window at the right edge looked missing; `shot.ps1` and
  `shot-watch.ps1` now call `SetProcessDPIAware` and capture the whole screen.
- POGLanTest (the test tool), run 6–7: `ScreenCapture.CaptureScreenshot` fails under Il2CppInterop ("Method not
  found: ReadOnlySpan`1.GetPinnableReference") (D11, tool only), so screenshots are taken from outside the game.
- Not the library, seen the same night: POG Timer 1.0.2 (Thunderstore) logged "Hook failed" four times a second
  while the menu was up; 1.1.0, which does not, was published 2026-11-19. POGLanTest (the test tool) had four
  defects, fixed during the test: hosting from the main menu never loaded a scene, a client that joined before the
  main menu came up had no camera, and the first launch's intro and OK screen waited for input.

### Order

*(Superseded 2026-11-19 by "Plan to the first release and after" at the top of this file.)*

1. Silent failures and the single pick: small, safe, independent of everything else. *(done)*
2. G1 and G2, then decide R1 and R2. *(2026-11-19: G1 shown over LAN, G2 and G3 done, R1 done; R2 has the user's
   yes and waits for the design review near the top of this file)*
3. G3–G6. *(G4: a purchase from a new slot by a client is seen, run 21; theft and the seller's death are not)*
4. R3–R5, then the first release.

---

## Milestone 0 — De-risk (requires launching the game; done by hand)

These cannot be verified without running the game in two instances — the framework is coded
to the statically-confirmed facts, but these three must be validated before trusting them:

- [ ] **0.1 ForceSamePrefabs** — already resolved statically to `TRUE`
      (NetworkConfig+0x6C). Confirm at runtime via a config log on session start.
- [ ] **0.2 AssetBundle round-trip smoke test** — build an `HDRP/Lit` cube in Unity
      2022.3.62f2, load it via `AssetLoader.LoadBundle`, spawn it in-game. Round-trip is not
      yet proven anywhere in this project; this gates the full bundle visual pipeline.
- [ ] **0.3 Two-player registration test** — host + client, confirm a deterministic-hash mod
      prefab registers on both peers and the client sees the real mod item (not the shell),
      and that a vanilla client is rejected at connect (expected, given ForceSamePrefabs=true).

---

## v0.1 — Core + Items (current)

Framework skeleton compiles clean; correctness of the runtime paths pending Milestone 0.

- [x] Bevy-style plugin graph (`IPOGPlugin` / `PluginGraph` / `PluginRegistry`) driven by real
      MelonLoader + NGO lifecycle stages (Boot / SceneLoaded / BeforeSessionStart / SessionStop).
- [x] `ContentRegistry` — deterministic `GlobalObjectIdHash` = MD5(modId:contentId), def-by-hash.
- [x] `ShellFactory` — clone a safe vanilla shell (Diamond default), assign the hash, strip
      the self-spawn chain; prefers clean asset instances over dirty live ones.
- [x] `NetworkRegistrar` + `NetworkLifecycleHooks` — register prefabs via a Harmony prefix on
      `StartHost/StartClient/StartServer` (the only legal window), post-verify via
      `NetworkPrefabOverrideLinks`, re-salt on hash collision, reset on `Shutdown`.
- [x] `LootInjector` — both table families (fixed `Il2Cpp.LootTable` array-realloc **and**
      `Il2CppInventory.InventoryLootTable` List.Add), targetable by category and/or table name
      (biomes, ore veins, shop pools, gambling, reward chests); host-side, zero consumer patching.
- [x] Full item identity: `ItemType` (slot routing) + `MapIcon` + `FoodValue`/`ShopCost` —
      driven by the item-system map (the item-system research notes).
- [x] `SaveGuard` — Harmony finalizer on `ItemContainer.FromContainerData` (the one save path
      that throws on an unknown hash → turns "lose all reward chests" into "lose one").
- [x] `Items` module — `ModItemDefinition`, host-authoritative use flow, `ModItemHandle`
      (hash-resolved state), Interact/OnNetworkSpawn Harmony patches.
- [ ] **Port POGCustomStones** onto POGContentLib as the reference content pack (validates the API).
- [~] **Minimal parity layer (multiplayer PRECONDITION, promoted from v0.2) — SCAFFOLDED, compiles.**
      Because `ForceSamePrefabs=TRUE`, mismatched content packs make co-op joins fail with an opaque
      "connection failed". The Lib does this self-sufficiently (no other mod required):
    - [ ] **2026-11-12, seen in every hosted test session: no Steam lobby callback fires**, so the log says
          "Parity/Steam bridge unavailable" and parity is off. The game hosts through an EOS lobby now
          (`NetworkHandler.m_onlineService` / `ServicesEOS`, see POGPlayerLimit's probe); move the manifest to
          EOS lobby attributes.
    - [x] `ParityManifest` — deterministic, order-independent snapshot of registered content
          (`modId:contentId@version` + an 8-hex token); versioned wire format; pure logic, no game dep.
    - [x] `ParityService` — host advertises the manifest, a joining client reads and compares the
          host's; a mismatch is logged AND raised via the public `Content.Parity.OnMismatch` event
          (`ParityReport`: Missing / Extra / VersionMismatch). Fallback is a plain MelonLogger line.
    - [x] Channel = **Steam lobby metadata** (`NetworkHandler.Singleton.Lobby.SetData/GetData`, key
          `pog_parity`), read *before* connecting so it touches no NGO state. The NGO
          ConnectionApproval/`DisconnectReason` path is intentionally NOT used (vanilla already sets
          `ConnectionApproval=TRUE`; wrapping the game's own approval callback is riskier).
    - [x] `ParitySteamBridge` binds the Facepunch interop **reflectively** (guarded, degrades to one
          log line); `CompatibilityProbe` now name-checks those members too.
    - [ ] **RUNTIME-TODO (Milestone 0):** confirm `NetworkHandler.Singleton.Lobby` is populated and
          host-written metadata is visible to the joiner at the Start*/join moment we hook.
      Rich presentation is the optional POGConfig UI in v0.2. Validate in the 2-player test.
- [ ] Run Milestone 0; fix whatever the two-player test reveals.

## v0.2 — Items hardened + assets

- [~] **AssetBundle visual pipeline — IMPLEMENTED, unproven in-game.** `ItemVisual.Bundle(path, asset)`
      instantiates a custom prefab as the item's visual (with placement via `.At(offset, euler, scale)`)
      and disables the shell's renderers. Engine-side effects ride along inside the prefab (particles,
      lights, trails, animation); custom C# MonoBehaviours cannot survive IL2CPP and are dropped by
      Unity — logic belongs in capabilities/use handlers. Materials that lost their shader are repaired
      onto `HDRP/Lit` (same-named properties survive the swap). Modder guide: `CUSTOM_ASSETS.md`.
      Gated on the Milestone 0.2 round-trip test.
- [ ] **What a vanilla item needs, read from the decompile** (`docs/design/item-chain.md`, build 26668562,
      2026-11-19, nothing of it checked in game). The shell covers every hard requirement; the gaps:
    - [ ] Always give the item its own `m_itemTooltipID` (name term = discovery id), also without `DisplayName`.
          Today such an item keeps the shell's key: it is called "Diamond" and counts as discovered with it.
    - [ ] `AttachBundlePrefab` parents the model under the root; the game shows, hides and layers only
          `PickupItem` / `HeldItem` and re-enables every renderer of `m_renderers`. Put the model under
          `PickupItem` (copy under `HeldItem`) and take the shell's renderers out of `m_renderers`.
    - [ ] Journal entries for mod items (`JournalEntryDataItem` + the UI entry before
          `UI_JournalGroupItems.Initialize`); the game never creates item entries in code.
    - [ ] A big-item shell: `Big = true` on the diamond shell keeps the diamond's grips and size.
    - [ ] Expose `m_itemRarity`, `m_breakVelocity` (with a break effect), `m_triggerDiscoveryOnPickup`.
    - [ ] Check a definition when it is registered (capability duration > 0, curve set, a grip present) and
          say what is wrong, instead of an NRE in game.
- [ ] Loose-file tier polish: PNG icons, OGG audio, OBJ meshes (no Unity Editor needed).
- [ ] `HostBridge` client→host use requests via `CustomMessagingManager` named messages
      (so non-host players can trigger item effects).
- [~] **Shops module** (`Shops/`, design `docs/design/shops.md`, guide `SHOPS.md`, sample `samples/ShopDemo/`) —
      2026-11-12, works for a single host; seen in the real game through `tests/RuntimeDriver`. **Not ready
      to publish: see the pre-release gate at the top** for what was not verified and what to rework.
    - [x] Shop files (`pogshop.json`, format 1): parse with exact errors, typo warnings; unit-tested
          (`tests/Shops.Tests`, mutation-calibrated).
    - [x] Weights instead of percentages; add / patch (patch needs `requires`); dependency order; per-slot
          "who changed what" history. The model is unit-tested; the runtime's own pick is not (gate).
    - [~] Rules on vanilla slots: the host picks by weight and hands the game a fixed item. In game: a mod
          item and a vanilla item into fixed and pool slots, priced, linked to the slot, faction kept. No
          purchase was made and no distribution measured.
    - [~] New slots next to any seller (network prefab per slot, built before the session). In game: placed,
          stocked, priced, bound to the seller with its faction. No purchase; no client (gate R1).
    - [~] Standing points (`m_standingTransform`, what the game really uses; `TableGrip` is not) for mod items.
          Checked for an item that keeps the shell's model only.
    - [~] Dungeon merchants as weighted variants of a vanilla preset (no new prefab). In game: one dungeon,
          the mod merchant selling its own stock (gate R2 changes how the weight works).
    - [~] In-game shop tools: aim + place, nudge, panel with chances/history/items, Save, round-timer freeze
          (in game: frozen 8 s → elapsed +0.00), scene jumps (in game: Lobby → InBetween → DarkBurg). All of
          it driven from code by the test driver; no key or button was pressed by hand.
    - [ ] Two-player test: new slots and merchant stock on a client (gate G1).
    - [ ] Shop files in the multiplayer parity manifest (the parity layer itself needs the EOS lobby, below).
- [~] **Item capabilities — SCAFFOLDED, compiles.** Declarative `ItemCapability` on a definition
      attaches the sibling `ActiveItem_*` component and sets its scalar fields (item anatomy §2).
    - [x] `EatCapability` (health/stamina/satiety), `MeleeWeaponCapability` (durability + hit),
          `ThrowableCapability` (health/bounce); attached in `ItemsPlugin.ApplyCapabilities`, guarded.
    - [x] Handling / "weight" — `Big` / `PickupStaminaPenalty` / `MovementFactorWhenHeld` (plain
          `InventoryItem` fields; statically sound, not a component). There is no real weight field.
    - [ ] **RUNTIME-TODO (Milestone 0):** a bare shell lacks the components' serialized refs (VFX/
          sounds/curves) and their spawn-time NetworkVariables (durability/ammo/health seed); confirm
          the scalar effects fire and the added NetworkBehaviours stay in sync across peers.
    - [x] `GlowCapability` — glow/pulse the way vanilla does it (item anatomy §9): a child `Light`
          (colour / intensity / range), optional emissive material tint, optional pulse via the game's
          own `LightFlicker` (strength / duration / vibrato / randomness).
- [x] **Open visual-effect system (no hardcoded effects)** — `ItemCapability.Attach` is public, so a
      third-party mod implements effects through the *same* contract the built-ins use (no privileged
      core). `ItemVisualEffect` base gives `CreateEffectChild` (reused on rebuild, never stacks) and
      `GetVisualRoot` (follows a custom mesh). `ParticleEffect` covers the no-code cases: clone a
      vanilla VFX (`FromGameObject`) or instantiate your own particle prefab (`FromBundle`), with
      placement and recolour. `Content.Diagnostics.ListVfx(filter)` — or a `vfx:<filter>` line in
      `UserData/pog_probe.txt` — finds vanilla effects to reuse. Guide: `VISUAL_EFFECTS.md`.
    - [ ] Torch/light + weapon NetworkVariable seeding via an `OnNetworkSpawn` step (needs a donor
          prefab for the light/VFX refs) — v2.
- [x] **`VisualProbe` + `Content.Diagnostics`** — prefab-serialized visual data (light colour/
      intensity/range, flicker settings, emissive materials) is invisible to static analysis, so this
      logs it from a live instance. Drive it from code (`Content.Diagnostics.ProbeItem("Item_Speaking
      Stone")`) or with no code at all via `UserData/pog_probe.txt` (one prefab name per line). Use it
      to capture real numbers to feed into `GlowCapability`. Read-only; also reports HDRP intensity.
- [ ] Per-item state persistence: slot-keyed companion file (JsonUtility drops unknown fields,
      so mod state cannot live in the vanilla save).
- [ ] **Rich parity UI (optional, soft-dep)** — POGConfig (if installed) subscribes to the Lib's
      `OnParityMismatch` event and renders a native panel (installed packs, add/update/remove,
      shown *before* joining). Presentation only; detection stays in the Lib. NOT a separate mod.
- [ ] Retire legacy **POGCustomItems** to `archive/` once Items reaches parity
      (POGSpawner must be re-pointed at POGContentLib first).

## v0.3 — Mobs module

Same shape as Items (shell-clone + deterministic hash), confirmed feasible in the research.

- [ ] `ModMobDefinition` { shell, visuals, stats, dangerLevel, maxAlive, behavior }.
- [ ] Shell clones from `SkullEntity`/`RatEntity` (cheapest) / `SkeletonEntity` (humanoid).
- [ ] Spawn-table injection into `EnemyLootTable` arrays (fixed → array-realloc) and/or
      `SetEnemyLootTable`/`SetEnemyConfig` before `StartGame` on the host.
- [ ] Managed FSM API layered on the stock brain (`FSM.AddState/AddTransition(Func<bool>)`).
- [ ] Danger-budget balance via `m_dangerLevel` / `m_maxSpawnAmount` (no director patches).

## v0.4 — Levels module (tier a+)

Dungeon geometry is seed-local (no per-object network spawn), so custom piece-based dungeons
need no NGO scene-hash patching.

- [ ] `ModLevelDefinition` via runtime `DungeonDescriptor` + community `DungeonPiece` prefabs.
- [ ] Inject descriptors into `LevelInfoData.m_dungeons` rotation.
- [ ] Resurrect `UIDebugDungeonVisit` (or clone its flow) as a deterministic-seed test launcher.
- [ ] Consider splitting Levels into its own package if it grows an editor SDK.

## Later / stretch

- [ ] `Effects` module.
- [ ] Whole custom **scenes** from AssetBundles (tier b) — needs symmetric NGO
      `NetworkSceneManager` hash handling (`VerifySceneBeforeLoading` is a public settable field).
- [ ] Codeless content packs (manifest + bundle, folder auto-discovery) — the ecosystem-growth
      step; restrict bundles to vanilla-component prefabs under IL2CPP.
- [ ] POGConfig integration: in-game "installed content packs" panel + parity-mismatch UX (soft dep).
- [ ] Thunderstore release: framework package + content packs as pinned dependents.

### For content mods planned on top of the library

- [ ] **POGDeeperDepths** (working name; a separate mod, its plan is in its own folder,
      `_Modding/ModSource/POGDeeperDepths/ROADMAP.md`): depths past the vanilla ones, opened by cog tiers. It
      needs from the library the Levels module and items; extending the depth station is the mod's own work
      unless other mods need it too.

---

## Known limitations (current)

- Runtime correctness of network registration, save behavior and bundle round-trip rests on
  static decompilation until Milestone 0 is run in-game.
- Use effects are host-only for now (client→host RPC is v0.2).
- Loot injection targets `LootTable` category arrays; `InventoryLootTable` (per-chest/enemy)
  targeting is a later, more selective feature.
- Custom visuals are icon/tint level until the AssetBundle smoke test (0.2) passes.
