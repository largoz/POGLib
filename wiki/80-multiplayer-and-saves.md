# 80 Multiplayer and saves

## Who needs what

| Player | Needs |
|---|---|
| Everyone in the game | your mod, the same version, and POGLib: otherwise your items do not appear for them |
| The host | your shop file, if you have one: the host decides what every slot sells |
| A client | POGShops too, if your mod has shop slots: without it the slot is there but its price tag may sit wrong |

The game lets a player without your mod join anyway (on build 26668562): your items simply do not appear for them.
[POG Mod Check](https://thunderstore.io/c/pit-of-goblin/p/Largo/POG_Mod_Check/) tells a joining player what differs
before anything loads, and counts a mod that adds items through POGLib as required for everyone by itself.

## Who runs what

- **The host** chooses what each shop slot sells and places your new slots. Clients learn about a new slot from the
  host (POGShops sends them its name and price tag).
- **Every player** builds your item the same way from your code, so it looks and behaves the same everywhere.
- **A use handler runs on the host only** in 0.1: when a client uses your item, the handler does not run yet.

Shown in the game over LAN (a host and a client): a client sees your item and your new slot, buys from it, eats the
item, drops it, and passes it to the host through the bucket.

## Saves

*Tested in the game (build 26668562) with POGLib 0.1.1: a berry saved in the bucket came back after loading; without
its mod the save loaded with no error and the berry was gone.* **POGLib 0.1.0 does not bring mod items back from a
save** (they vanish from the bucket on loading): use 0.1.1 or newer.

- The game saves when the bucket returns to the hub and at the end of a round, on the host, through Steam Cloud.
- Items in the bucket and the hub's storage, and what the players carry (it comes back loose in the bucket), are saved
  **by their network identity**, which for your item is the hash of `ModId:ContentId`. With your mod installed they come back; that is why your ids must never
  change after release.
- If a player removes your mod, your items in their save are skipped when it loads: they are not there, and the rest
  of the save loads normally. They are still in the save file until the game saves again (the bucket reaching the
  hub, or a round's end); put the mod back before that and they return.
- Reward chests are the one place where an unknown item breaks loading (the chests after it are lost); POGLib catches
  that while it is installed. In 0.1 your items do not reach reward chests anyway.

Next: [90 Troubleshooting](90-troubleshooting.md).
