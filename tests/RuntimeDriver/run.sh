#!/usr/bin/env bash
# Run POGLib in the real game (Steam + the r2modman profile "Tests") under the runtime driver, then clean up.
#
#   run.sh <out dir> [extra DLL or folder to deploy into Mods/ ...]
#
# Deploys: the library build (../../bin/Release/net6.0/POGLib.dll), POGShops (../../Shops/bin/Release/net6.0/POGShops.dll;
# POG_NO_SHOPS=1 leaves it out), the driver, the extras, and
# <out dir>/driver.txt as UserData/POGLib/driver.txt when it exists. Screenshots the window ~2 s
# after every "[DRIVER] shot <name>" log line, stops at "[DRIVER] done", closes the game and removes
# everything it deployed. Prints the library's and driver's log lines.
# Ask/announce before running: the game takes the screen for a few minutes (see _research/TESTING.md §0).
set -u
PROFILE="${POG_PROFILE:-$HOME/.config/r2modmanPlus-local/PitOfGoblin/profiles/Tests}"
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
OUT="$1"; shift
LOG="$PROFILE/MelonLoader/Latest.log"
MODS="$PROFILE/Mods"
CFG="$PROFILE/UserData/POGLib/driver.txt"
TIMEOUT="${POG_TIMEOUT:-420}"
START_WAIT="${POG_START_WAIT:-300}"

if pgrep -x 'Pit of Goblin.e' >/dev/null; then echo "game already running (the user may be playing): abort"; exit 1; fi
# Without a running Steam, -applaunch starts Steam and launches the game whenever the login finishes,
# possibly after this script has given up and removed its files (seen 2026-11-14).
if ! pgrep -f 'steamrt64/steam|ubuntu12_32/steam' >/dev/null; then echo "Steam is not running: start it (logged in) first: abort"; exit 1; fi
mkdir -p "$OUT"
# Under heavy load the game stalled on the loading screen and never hosted (load 62, 2026-11-14).
echo "load average: $(cut -d' ' -f1-3 /proc/loadavg)"

deployed=()
deploy() {   # atomic copy into Mods/, remembered for removal
  local src="$1" name; name="$(basename "$src")"
  if [ -d "$src" ]; then cp -r "$src" "$MODS/$name"; else cp "$src" "$MODS/.$name.tmp" && mv -f "$MODS/.$name.tmp" "$MODS/$name"; fi
  deployed+=("$MODS/$name")
}
cleanup() {
  local gp; gp=$(pgrep -x 'Pit of Goblin.e'); [ -n "$gp" ] && kill $gp
  for _ in $(seq 1 20); do pgrep -x 'Pit of Goblin.e' >/dev/null || break; sleep 1; done
  # A launch that hung before the game process (or outlived it) keeps Steam's reaper, Proton and wineserver
  # alive, and Steam then treats the game as running and refuses the next launch (2026-11-14). The reaper
  # takes the rest of the tree with it.
  local rp; rp=$(pgrep -f "reaper SteamLaunch AppId=3253530 ")
  if [ -n "$rp" ]; then
    kill $rp
    for _ in $(seq 1 20); do pgrep -f "reaper SteamLaunch AppId=3253530 " >/dev/null || break; sleep 1; done
    pgrep -f "reaper SteamLaunch AppId=3253530 " >/dev/null && echo "reaper still running" || echo "reaper closed"
  fi
  # What the reaper left (on 2026-11-14 wineserver, services.exe and steam.exe outlived it): every process
  # Steam started for this game carries SteamGameId=3253530 in its environment.
  local left=()
  for p in /proc/[0-9]*; do
    [ "${p#/proc/}" = "$$" ] && continue
    { tr '\0' '\n' < "$p/environ"; } 2>/dev/null | grep -qx 'SteamGameId=3253530' && left+=("${p#/proc/}")
  done
  if [ ${#left[@]} -gt 0 ]; then
    echo "killing ${#left[@]} leftover process(es) of the launch"; kill "${left[@]}" 2>/dev/null; sleep 3
  fi
  pgrep -x 'Pit of Goblin.e' >/dev/null && echo "game still running" || echo "game closed"
  for f in "${deployed[@]}"; do rm -rf "$f"; done
  rm -f "$CFG"
  for f in "${test_shops[@]}"; do rm -f "$f"; done
  # The folders made for the config and the test shops go too, only when nothing else is in them.
  rmdir "$PROFILE/UserData/POGLib/Shops" "$PROFILE/UserData/POGLib" 2>/dev/null
  echo "Mods/ now:"; ls "$MODS"
}
trap cleanup EXIT

deploy "$ROOT/bin/Release/net6.0/POGLib.dll"
[ -z "${POG_NO_SHOPS:-}" ] && deploy "$ROOT/Shops/bin/Release/net6.0/POGShops.dll"
deploy "$HERE/bin/Release/net6.0/POGLibRuntimeDriver.dll"
for extra in "$@"; do deploy "$extra"; done
if [ -f "$OUT/driver.txt" ]; then mkdir -p "$(dirname "$CFG")"; cp "$OUT/driver.txt" "$CFG"; fi
# Test shop rules: <out dir>/shops/*.json -> UserData/POGLib/Shops/ (removed afterwards).
SHOPS_DIR="$PROFILE/UserData/POGLib/Shops"
test_shops=()
if [ -d "$OUT/shops" ]; then mkdir -p "$SHOPS_DIR"; for f in "$OUT"/shops/*.json; do cp "$f" "$SHOPS_DIR/"; test_shops+=("$SHOPS_DIR/$(basename "$f")"); done; fi
sha256sum "$ROOT/bin/Release/net6.0/POGLib.dll" "$MODS/POGLib.dll" | awk '{print $1}' | uniq -c
[ -z "${POG_NO_SHOPS:-}" ] && sha256sum "$ROOT/Shops/bin/Release/net6.0/POGShops.dll" "$MODS/POGShops.dll" | awk '{print $1}' | uniq -c

T0=$(date +%s)
# POG_LAUNCH_ARGS: extra game arguments, e.g. "--melonloader.hideconsole" (split on spaces).
(setsid steam -applaunch 3253530 --melonloader.basedir "Z:$PROFILE" ${POG_LAUNCH_ARGS:-} >/dev/null 2>&1 &)
WIN=""
seen=" "
while true; do
  now=$(date +%s)
  if [ $((now - T0)) -gt "$TIMEOUT" ]; then echo "TIMEOUT after $TIMEOUT s"; break; fi
  if [ -f "$LOG" ] && [ "$(stat -c %Y "$LOG")" -ge "$T0" ]; then
    [ -z "$WIN" ] && WIN=$(xdotool search --name "^Pit of Goblin$" 2>/dev/null | tail -1)
    while read -r name; do
      case "$seen" in *" $name "*) ;; *)
        seen="$seen$name "; sleep 2.2
        [ -n "$WIN" ] && import -silent -window "$WIN" "$OUT/$name.png" && echo "shot $name";;
      esac
    done < <(grep -a -o "\[DRIVER\] shot [A-Za-z0-9_-]*" "$LOG" | awk '{print $3}')
    grep -a -q "\[DRIVER\] done" "$LOG" && break
  fi
  # Proton may prepare for minutes before the game process exists (first launch after a game or Proton
  # update: 2026-11-14, over 90 s); MelonLoader then regenerates its assemblies inside the process.
  if [ $((now - T0)) -gt "$START_WAIT" ] && ! pgrep -x 'Pit of Goblin.e' >/dev/null; then echo "game process gone (or never started within $START_WAIT s)"; break; fi
  sleep 1
done
rm -f "$OUT/Latest.log"
# Only a log written by this run counts: an older one would pass every check it happens to contain.
if [ ! -f "$LOG" ] || [ "$(stat -c %Y "$LOG")" -lt "$T0" ]; then
  echo "no log from this run (Latest.log is from $(date -r "$LOG" '+%F %T' 2>/dev/null || echo never)): nothing checked"
  exit 100
fi
cp "$LOG" "$OUT/Latest.log"
# Unity's own log: exceptions inside game code (a coroutine, OnNetworkSpawn) appear only there.
PLAYER_LOG="/run/media/tim/Games/SteamLibrary/steamapps/compatdata/3253530/pfx/drive_c/users/steamuser/AppData/LocalLow/Friendly Foe/Pit of Goblin/Player.log"
[ -f "$PLAYER_LOG" ] && [ "$(stat -c %Y "$PLAYER_LOG")" -ge "$T0" ] && cp "$PLAYER_LOG" "$OUT/Player.log"
grep -a -E "\[DRIVER\]|POGLib|\[Shops\]|ERROR|Exception" "$LOG" | grep -a -v "\[DRIVER\] G2 prefab" | cut -c1-300

# Expectations (<out dir>/expect.txt, see expect.sh) and the driver's own FAIL lines.
# Exit status = number of failures (0 = all passed).
"$HERE/expect.sh" "$OUT/Latest.log" "$OUT/expect.txt" "$OUT/Player.log"
exit $?
