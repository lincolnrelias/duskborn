# Unity CLI workflow

Use `Tools/unity.ps1` for non-interactive Unity compilation, validation, tests,
and builds. The wrapper reads the required Unity version from
`ProjectSettings/ProjectVersion.txt` and resolves the matching Unity Hub install.

## Commands

```powershell
.\Tools\unity.ps1 compile
.\Tools\unity.ps1 validate
.\Tools\unity.ps1 test
.\Tools\unity.ps1 test-warden
.\Tools\unity.ps1 clear-terrain
.\Tools\unity.ps1 all
.\Tools\unity.ps1 capture-map
.\Tools\unity.ps1 build-windows
.\Tools\unity.ps1 build-warden
.\Tools\unity.ps1 build-briarback
.\Tools\unity.ps1 build-thornwing
.\Tools\unity.ps1 build-ironroot
.\Tools\unity.ps1 test-ironroot
```

- `compile` imports changed assets, compiles scripts, and requires a success marker.
- `build-briarback` imports the original rigged woodland charger and its sounds, builds
  materials/controller/prefab/loot, registers the FishNet and enemy prefab collections,
  installs night 2-6 definitions, and checks the model, attack clock and seeded budgets.
- `build-thornwing` imports the original low-hovering moth and recorded foley, creates
  its ranged projectile/prefab/rewards, registers both FishNet collections, adds its
  weighted pool from night 1 onward, and checks ground reach, obstacles, reuse and seeded budgets.
- `build-bramblekin` replaces the basic Swarmer prefab while retaining its GUID and
  spawn slot, installs the original woodland scavenger and recorded foley, and
  validates targeting, networking, imported pose grounding and seeded budgets.
- `build-ironroot` imports the original Ironroot Humanoid, creates URP materials, replaces
  both player prefab visuals while preserving gameplay/controller references, and validates
  skinning and sampled locomotion clips. Requires `Artifacts/Ironroot/v002/Ironroot.fbx`.
  Already-installed Ironroot visuals retain their prefab overrides and sockets on model refresh.
- `test-ironroot` validates the installed player prefabs without rebuilding them.
- `build-warden` imports the grounded v002 Hollow Warden model, creates its materials,
  Animator, boss/root prefabs and HUD, registers FishNet prefabs, and validates references.
  Run it before `all` when setting up or regenerating the boss assets.
- `clear-terrain` clears procedurally generated meshes/foliage from `SampleScene.unity` and clears NavMesh surfaces before committing.
- `validate` checks enabled build scenes, project prefabs for missing scripts, and
  ScriptableObject assets under `Assets/_Duskborn/Resources`.
- `test` executes the project's existing static Editor test suites and turns any
  logged error, assertion, exception, or thrown exception into a failed process.
  Batch tests start from an empty scratch scene in Temp/DuskbornCliTests.unity.
  Edit-mode UI tests initialize their fixtures explicitly and check requested cursor
  state; native cursor locking and visible rendering still require a manual check.
- `all` runs compile, validation, and tests, but does not build a player.
- `capture-map` renders the production minimap and world-map canvases with seeded terrain,
  player and structure fixtures into `Artifacts/WorldMap`, in edit mode using a hidden graphics
  process. This verifies the static HUD composition, not a live multiplayer session.
- `build-windows` invokes the project's synchronous `BuildPipeline` entry point
  and creates `Builds/Windows/Mugg.exe` by default.

Logs are written to `Logs/UnityCli`. Test results are also summarized in
`Logs/UnityCli/test-results.json`. Both `Logs` and `Builds` are ignored by Git.

## Options

Override executable discovery or the build destination when necessary:

```powershell
.\Tools\unity.ps1 compile -UnityPath 'D:\Unity\Editor\Unity.exe'
.\Tools\unity.ps1 build-windows -BuildPath 'D:\Builds\Mugg.exe'
```

## Operating rules

- Do not open or control the Unity Editor UI and do not enter Play Mode.
- Source changes and offline checks may proceed while this project is open.
  Unity permits only one process to hold a project at a time, so use an isolated
  copied project for batch validation when the live checkout is already held.
  Do not require the user to close their Editor.
- Keep CLI entry points synchronous and under `Assets/_Duskborn/Editor`.
- Prefer `all` after gameplay changes and `compile` for a quick compilation gate.
- Visual behavior remains a manual user check unless a dedicated offscreen test
  or generated artifact exists. Never claim visual verification from CLI checks.
- Do not add `-runTests` unless real Unity Test Framework tests are introduced.
  The current suites are menu-style static methods driven by `DuskbornCli.RunTests`.

The `test-warden` command runs focused boss asset/mechanics/regression checks without rebuilding prefabs.

## Rune presentation capture

`Tools/unity.ps1 capture-runes` runs a hidden batch-mode graphics process to save
`Artifacts/RunePresentation/runes-overview.png`. It renders production prefabs,
all eight rune families, a mixed enemy, player statuses, and etched weapons without
entering Play Mode or controlling the Editor UI. Run against an isolated copy when
the live project is open. This is a static composition check; combat motion and
multiplayer timer behavior still need in-game observation.
