# Ranged combat foundation

The starter action bar now includes **Wooden Bow**. The existing **Simple Bow**
recipe also produces it. Select the bow and use LMB while the gameplay cursor is
locked. This first test weapon has unlimited arrows and no secondary action.

The bow uses the existing humanoid `Archer@BowShot01` animation. Its authored
`OnShoot` time, 0.67418647 normalized, is reproduced as a `SpawnProjectile` timeline
event. A runtime clip copy removes the vendor demo's animation callbacks without
editing the original animation. The left-hand bow draws its string and displays
a nocked arrow until release. Switching weapons or dying cancels a pending shot.

## Flight and damage

- Players aim through the center of the camera. Arrows drop under gravity; aim
  higher for distant targets. Release originates at chest height to keep the
  initial obstruction check inside the shooter's collision volume.
- The server validates the windup, attack cooldown, finite aim and one release
  per attack. It owns damage and sweeps a sphere along each fixed-step ballistic
  segment using a kinematic Rigidbody. Initial overlaps and buffer saturation
  are handled; thin obstacles cannot be skipped by a fast arrow.
- Owner colliders, allied characters and triggers are ignored. Environment
  colliders stop flight. Dynamic rigidbodies receive an impact impulse.
- Enemy/player damage uses existing damage APIs, including crits, player attacker
  attribution, weapon type modifiers, class damage hooks, lifesteal and thorns.
  Other `IDamageable` objects receive the generic damage call.
- Arrows attach to the struck collider transform and expire after 15 seconds.
  Missed arrows expire after 8 seconds. Pooled victims discard embedded arrows.
- Clients display flight and receive the authoritative impact/attachment. Impact
  delivery uses player connections so it survives the firing enemy's despawn.

The inventory remains the project's existing local inventory. The ranged server
accepts only definitions from `Resources/Weapons`; this is a catalog restriction,
not a new server-owned inventory entitlement system. Existing airborne/embedded
arrows are not replayed for late joiners in this first implementation.

## Enemy testing

`Assets/_Duskborn/Prefabs/Enemies/ArcherTest.prefab` is a separate Swarmer-derived
test prefab equipped with the same bow, registered in both FishNet prefab lists.
Use it in a test wave/pool on a baked NavMesh. Normal wave composition is unchanged.
It attacks within 15 m, checks line of sight before winding up, and uses a low
ballistic arc toward its target. Death/stagger interrupts the ranged animation.
Other animated enemies can use the same ranged `WeaponDefinition`.

## Extending it

Create a `ProjectileDefinition` and a `ProjectileImpactBehaviour` subclass. Flight
applies direct damage once, then invokes `OnImpact`. `ArrowImpactBehaviour` embeds
the projectile; a fireball implementation can instead apply server-only area
damage and play its explosion on both server/clients. Check `IsAuthoritative`
before applying additional damage. Keep effect state on the flight, not on the
shared ScriptableObject. Assign the definition to a `RangedWeaponBehaviour` and
author one primary clip with one `SpawnProjectile` event. Add the weapon asset
under `Resources/Weapons` with filename equal to its item ID.

## Assets and validation

`Tools/RangedCombat/create_assets.py` reproduces the original bow/arrow meshes,
materials, prefabs and wiring without launching Unity. Dimensions are in meters;
bow: 1.36 m / 724 triangles; arrow: 0.81 m / 72 triangles, tip at local origin,
flight along +Z. The geometry uses native Unity mesh assets and matte URP materials.
No externally generated model or paid service was used.

Offline checks:

```powershell
.\Tools\RangedCombat\Compile.ps1
.\Tools\RangedCombat\TestTiming.ps1
python Tools/RangedCombat/validate_assets.py
```

After closing this project in Unity, run `.\Tools\unity.ps1 all`. The registered
`RangedCombatTests` covers imported asset references, release timing, thin walls,
nearest obstruction, duplicate impact suppression and arrow attachment.
Offline compilation does not run FishNet weaving or Unity physics tests.

Manual checks still required: left-hand fit on the player/enemy avatars; nock and
release alignment; camera aiming at different elevations; arrows sticking into
moving/ragdoll targets; host plus remote-client fire/damage; switch/death during
windup. Use the existing Item Fitting Studio to tune `WoodenBowLeftHand` if needed.
