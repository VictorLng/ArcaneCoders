# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Arcane Code: Unity 2D roguelite prototype (Unity **6000.6.0f1**, URP 2D, Input System). The player moves a mage; attacks are automatic and driven by a player-written program in a custom typed pseudo-language (`class MagoDeFogo extends Mago { void attackOne() {...} }`). README.md (Portuguese) documents gameplay, the full language spec, complexity costs and limits — consult it before changing language/cost rules. Code, UI text and docs are in Portuguese.

## Commands

There is no CLI build script; everything goes through the Unity Editor (or `Unity -batchmode -executeMethod`):

- `Arcane Code → Preparar projeto` (`ArcaneBuild.Setup`): creates GameConfig/material/scene if missing, sets URP, PlayerSettings and build scene list. Idempotent; logs `ARCANE_SETUP_OK`.
- `Arcane Code → Build Linux / Build Windows` (`ArcaneBuild.Linux` / `.Windows`): run Setup, then output to `Builds/Linux/ArcaneCode.x86_64` or `Builds/Windows/ArcaneCode.exe`; logs `ARCANE_BUILD_OK`.
- Tests: Window → General → Test Runner (EditMode + PlayMode). Headless example: `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Artifacts/Tests/editmode.xml` (use `PlayMode` for the other suite; add `-testFilter <name>` for a single test).
- Env var `ARCANE_PROFILE_DIR` redirects the save directory (`profile.json`); tests rely on temporary profiles.
- Tunables (health, speeds, spells, enemies, timings) live in `Assets/Resources/GameConfig.asset` (`GameConfig` ScriptableObject).

## Architecture

Three assemblies under `Assets/ArcaneCode/` (dependency direction Core ← Runtime ← Editor):

- **`ArcaneCode.Core`** (`noEngineReferences: true` — no UnityEngine usage allowed): pure logic.
  - `SpellLanguage.cs`: lexer/parser/type checker/complexity costing for the pseudo-language (limits: chars, tokens, methods, nesting, loop bounds, recursion rejection).
  - `SpellMachine.cs`: step-limited interpreter executing `attackOne()` over frames; talks to the game only through the `ISpellWorld` interface.
  - `Dungeon.cs`: seed-reproducible branching room graph generation.
- **`ArcaneCode.Runtime`**: the game, a single `ArcaneGame` MonoBehavior split into partial files — `ArcaneGame.cs` (state, profile/save, progression), `.Combat.cs` (rooms, enemies, projectiles; implements `ISpellWorld`), `.UI.cs` (code-built UI, grimoire editor, syntax highlight, autocomplete). `WorldArt.cs` generates all art procedurally; `GameConfig.cs` is the config asset. The scene `Assets/Scenes/ArcaneCode.unity` only contains a camera, a global light and one `ArcaneGame` object — UI and world are built at runtime.
- **`ArcaneCode.Editor`**: `ArcaneBuild` menu items.
- Tests: `Tests/Editor/CoreTests.cs` (language, costs, limits, 1000 dungeon seeds) and `Tests/PlayMode/GameTests.cs` (full-game flows for both classes, boss, pause, persistence).

Key behavioral invariants (from README): the last *valid* applied program is kept even when a draft is invalid; energy persists within a room and resets on room change; paused game advances no energy/projectiles; fragments are deposited exactly once per run end; the profile save uses temp file + backup + preserves corrupt files.

## Repo notes

- `Artifacts/` holds committed validation output (test XMLs, screenshots, `VALIDATION.md`); `Builds/`, `Library/` etc. are not tracked.
- `Assets/_Recovery/` contains Unity auto-recovered scenes; `Assets/Scenes/SampleScene.unity` is template leftover — the game scene is `ArcaneCode.unity`.
- Fonts (DejaVu) are loaded from `Resources` and their license must ship alongside builds (`ArcaneBuild` copies it).
