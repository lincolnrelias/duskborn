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
.\Tools\unity.ps1 build-windows
.\Tools\unity.ps1 build-warden
```

- `compile` imports changed assets, compiles scripts, and requires a success marker.
- `build-warden` imports the grounded v002 Hollow Warden model, creates its materials,
  Animator, boss/root prefabs and HUD, registers FishNet prefabs, and validates references.
  Run it before `all` when setting up or regenerating the boss assets.
- `clear-terrain` clears procedurally generated meshes/foliage from `SampleScene.unity` and clears NavMesh surfaces before committing.
- `validate` checks enabled build scenes, project prefabs for missing scripts, and
  ScriptableObject assets under `Assets/_Duskborn/Resources`.
- `test` executes the project's existing static Editor test suites and turns any
  logged error, assertion, exception, or thrown exception into a failed process.
- `all` runs compile, validation, and tests, but does not build a player.
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
- Run the CLI only while this project is closed in any interactive Unity Editor;
  Unity permits only one process to hold a project at a time.
- Keep CLI entry points synchronous and under `Assets/_Duskborn/Editor`.
- Prefer `all` after gameplay changes and `compile` for a quick compilation gate.
- Visual behavior remains a manual user check unless a dedicated offscreen test
  or generated artifact exists. Never claim visual verification from CLI checks.
- Do not add `-runTests` unless real Unity Test Framework tests are introduced.
  The current suites are menu-style static methods driven by `DuskbornCli.RunTests`.

The `test-warden` command runs focused boss asset/mechanics/regression checks without rebuilding prefabs.
