# Repository Guidelines

## Project Structure & Module Organization

Arcane Code is a Unity 2D roguelite with programmable spells. Use Unity **6000.6.0f1** and the URP 2D pipeline.

- `Assets/ArcaneCode/Core/`: Unity-independent spell compiler, interpreter, and dungeon generation.
- `Assets/ArcaneCode/Runtime/`: gameplay, combat, UI, procedural art, and configuration types. `ISpellWorld` connects the interpreter to combat.
- `Assets/ArcaneCode/Editor/`: project setup and platform builds.
- `Assets/ArcaneCode/Tests/Editor/` and `Tests/PlayMode/`: unit and gameplay tests.
- `Assets/Scenes/ArcaneCode.unity`: main scene; `Assets/Resources/GameConfig.asset`: balance settings, alongside materials and licensed fonts.
- `Packages/` and `ProjectSettings/`: dependencies and Unity configuration. `Artifacts/` contains validation reports.

## Build, Test, and Development Commands

Open the repository through Unity Hub, load the main scene, and press **Play**.

- **Arcane Code → Preparar projeto**: create missing resources and configure the scene/build.
- **Arcane Code → Build Linux / Build Windows**: produce executables under `Builds/Linux/` or `Builds/Windows/`; install the corresponding Hub platform module first.
- **Window → General → Test Runner**: run both EditMode and PlayMode suites.

For command-line builds, set `UNITY` to the Unity editor executable:

```bash
"$UNITY" -batchmode -quit -projectPath "$PWD" -executeMethod ArcaneCode.Editor.ArcaneBuild.Linux -logFile build.log
```

Use `ArcaneCode.Editor.ArcaneBuild.Windows` for Windows builds.

## Coding Style & Naming Conventions

Use four-space indentation and braces on separate lines for namespaces, types, and multiline methods. Follow nearby C# formatting; avoid unrelated reformatting. Use PascalCase for types and methods, camelCase for locals and private fields, and matching `ArcaneCode` namespaces. Preserve the `ArcaneGame.*.cs` partial-class organization. No repository formatter or linter is configured.

Commit Unity `.meta` files with their assets; move assets through Unity to preserve references. Keep generated caches and builds out of Git.

## Testing Guidelines

Tests use NUnit and Unity Test Framework. Add descriptive PascalCase tests to `CoreTests.cs` (`[Test]`/`[TestCase]`) or `GameTests.cs` (`[UnityTest]`). Cover language limits and deterministic dungeon behavior in EditMode; gameplay and persistence in PlayMode. Run both suites before submission. No numeric coverage threshold is configured; balance changes also need human playtesting.

## Commit & Pull Request Guidelines

History contains short informal subjects, including Portuguese, without an established prefix convention. Write concise, action-oriented commit subjects. PRs should explain behavior changes, link relevant issues, report test/build results, and include screenshots for UI or art changes.

## Configuration & Save Safety

Use `ARCANE_PROFILE_DIR` for isolated test saves. Never modify player profiles during validation. Preserve the DejaVu font license when changing assets or packaging builds.
