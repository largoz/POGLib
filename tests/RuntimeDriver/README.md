# Runtime driver — POGLib in the real game, no mouse

A throwaway MelonMod that plays the game from inside and logs one `[DRIVER] …` line per step; `run.sh` launches
the game, screenshots, closes it and cleans up. This is how the Shops module was checked on 2026-11-12. Never ship
the driver. Ground rules for launching on this machine: `_Modding/ModSource/_research/TESTING.md` §0 (say before you
launch; never while the user plays).

## Run

```sh
dotnet build ../../POGLib.csproj -c Release -p:GameDir=$HOME/.local/share/com.kesomannen.gale/pit-of-goblin/profiles/Default
dotnet build RuntimeDriver.csproj -c Release            # GameDir defaults to the Gale profile
mkdir -p /tmp/run && printf "dump=1\n" > /tmp/run/driver.txt
./run.sh /tmp/run [extra DLL or folder for Mods/ ...]   # POG_TIMEOUT=720 for dungeon runs
```

`run.sh` deploys the library build, the driver, the extras and `<out>/driver.txt`; copies `<out>/shops/*.json` to
`UserData/POGLib/Shops/`; shoots `<out>/<name>.png` ~2 s after each `[DRIVER] shot <name>`; stops at
`[DRIVER] done`; then closes the game and removes everything it deployed (`Mods/` is listed at the end).

## How it gets into a game

From the main menu it opens the save-slot panel (`NetworkMenuGameSaveState`, the instance in the menu scene — the
copy `Resources.FindObjectsOfTypeAll` also returns throws) and calls `StartSelectedGame(slot)`: the game hosts and
loads `PitOfGoblin_Lobby`. Default slot = the last one, so slot 1 stays the player's. A new slot creates a save.

## driver.txt keys

| Key | Does |
|---|---|
| `slot=N` | save slot to start (default: last) |
| `dump=1` | one line per seller and per slot in the hub (ids, stock, faction, price, holder) |
| `look=<id>;<id>` | point the game's camera at each slot/seller id and shoot `look_N` (overridden before HDRP culls; a second camera does not show) |
| `devtools=1`, `devslot=<slot id>` | drive the shop tools: aim at the counter beside that slot, place a slot, shoot HUD and panel, Save, print and delete the file |
| `jumps=<scene>;<scene>` | the tools' scene jump to each scene, dump its shops, shoot `jump_N` |
| `dungeon=1` | down the bucket: list sellers, mod merchant slots, shoot `merchant`; then the tools' round-timer freeze for 8 s and 8 s unfrozen |
| `stay=N` | seconds before `done` |
| `start=0` | do not start a save slot; wait for another mod to host (POGLanTest `Auto = "host"` on a machine without Steam) |
| `checks=1` | the asserting run (below): registers a test item with no price, sets the vanilla-weight setting to 0 at the menu (back to 10 at the end), and in the hub prints `PASS`/`FAIL` lines, the game's network prefab list (`G2 …`) and every `ShopLocation.OnNetworkSpawn` with the caller's stack for repeats (`G3 …`) |

## Asserting run

```sh
O=/run/media/tim/Games/pog-runs/checks; mkdir -p $O && cp -r checks/. $O/
setsid -w ./run.sh $O ../../samples/ShopDemo/bin/Release/net6.0/ShopDemo.dll ../../samples/ShopDemo/bin/Release/net6.0/pogshop.json
echo $?    # number of failures
```

`setsid -w`: the run (and its cleanup) survives the shell or agent session that started it; without it, a session
that ended mid-run left the driver in `Mods/` (2026-11-14). Keep `<out dir>` outside temporary session folders.

`explore=1` (`Explore.cs`): read-only probes of the 2026-11-14 build (NGO prefab list, the game's own prefab list from
`NetworkHandler`, address keys, every hub slot's references) and traces of the slot pipeline, lines `[DRIVER] X …`.

`checks/` holds the driver config, a shop file with a mistake per rule (`shops/DriverChecks.json`), and
`expect.txt`: after the game closes, `expect.sh` checks the log against it (`ERE` must appear, `!ERE` must not,
`=N ERE` exactly N lines) and counts the driver's own `[DRIVER] FAIL` lines. `run.sh` exits with the number of
failures. `expect.sh <log> <expect file>` also runs on its own, on a saved log.

Back up the saves and `UserData/MelonPreferences.cfg` before a run (TESTING.md §5): the driver changes a setting
and puts it back, and a game closed by a signal may or may not save preferences.

Shop checks (always): per slot touched by a rule, what was picked and the chances, then the item the game actually
linked to the slot (`InventoryItem.ShopLocation`), active, price, tag, faction, seller.
