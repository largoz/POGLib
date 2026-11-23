# 12 Items: capabilities

A capability gives an item behaviour the game already knows: being eaten, glowing, hitting, being thrown. List
them in `Capabilities`; each is set up on every player's machine the same way, so the item behaves the same for
everyone.

```csharp
Capabilities =
{
    new EatCapability { CopyFrom = "Item_Apple", HealthOnEating = 15f },
    new GlowCapability { Colour = new UnityEngine.Color(0.3f, 1f, 0.5f), Intensity = 2.5f },
},
```

| Capability | Makes the item | State |
|---|---|---|
| `EatCapability` | food | **shown in the game** |
| `GlowCapability` | glow, optionally light up the room | experimental |
| `ParticleEffect` | carry a particle effect copied from a game object | experimental |
| `MeleeWeaponCapability` | a melee weapon with durability | experimental |
| `ThrowableCapability` | something you throw | experimental |
| `UsableCapability` | something you ready and use with a click | experimental |

**Experimental** means nobody has seen it work in the game yet; it may change. Tell us what you find.

## EatCapability: food

Start from a food of the game with `CopyFrom` (its sound, effects and numbers), then change what you want. Every
field you leave out keeps the copied value.

| Field | Means |
|---|---|
| `CopyFrom` | a food of the game to start from, e.g. `"Item_Apple"`, `"Item_Mushroom_Normal"` |
| `HealthOnEating` / `DamageOnEating` | health given / taken |
| `StaminaRefill`, `MinStaminaBonus`, `MaxStaminaBonus`, `UseStaminaMin` | stamina |
| `StomachFullTime` | how long the stomach stays full |
| `BodyValue`, `StaminaValue`, `StrengthValue` | points added to the Body (max health), Stamina and Strength stats |
| `BuffFrom` | apply the buff of another food of the game, e.g. `"Item_Mushroom_Glowing"` |
| `BuffValueToAdd`, `BuffValueToStart` | how much of that buff |

Set `ItemType = Il2Cpp.ItemType.Food` too, so the item goes to the food slot. In the game (LAN, host and client): a
berry with `CopyFrom = "Item_Apple"` and `HealthOnEating = 15f` raised a player's health from 100 to 110 on the
client; the shown health includes the game's own rules for eating.

## GlowCapability: glow (experimental)

| Field | Default | Means |
|---|---|---|
| `Colour` | white | the glow's colour |
| `Intensity` | 2 | brightness; above 1 reads as lit in a dark room |
| `MaterialFilter` | all | glow only the materials whose name contains this text (a lantern's glass, not its frame) |
| `CastsLight` | no | also a real light that lights the room; off, because that is rarely what "glow" means |
| `LightIntensity`, `LightRange` | 1, 4 m | that light |
| `Pulse`, `PulseStrength`, `PulseDuration`, `PulseVibrato`, `PulseRandomness` | off | the light pulses |

## ParticleEffect (experimental)

`ParticleEffect.FromGameObject(effectId, sourcePrefabName, sourceChildName)` copies a particle system from a child
object of a game prefab; `.At(offset, euler, scale)` places it, `.Coloured(colour)` tints it, `FollowVisualMesh`
attaches it to your model rather than the item. Which game prefabs carry useful particle systems is not listed yet.

## MeleeWeaponCapability, ThrowableCapability, UsableCapability (experimental)

| Capability | Fields |
|---|---|
| `MeleeWeaponCapability` | `MaxDurability` (100), `DurabilityLossOnEntityHit` (1), `DurabilityLossOnDestructibleHit` (1), `HitCooldown` (0.5 s) |
| `ThrowableCapability` | `HealthMin`, `HealthMax` (1), `BounceFactor` (0) |
| `UsableCapability` | `ReadyDuration` (0 = instant), `FovChange`, `UseCrosshair` |

## Your own capability

Capabilities are open classes: a mod can write its own (a new visual effect, say) by deriving from
`ItemCapability` or `ItemVisualEffect`, and other mods can use it by depending on that mod. A page on that comes
later ("14 Items: your own capability"); nobody has tried it in the game yet.
