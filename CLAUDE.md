# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

A 2D grid-based puzzle/movement game built in Unity 6000.3.21f1 (Universal Render Pipeline). There is no CLI build/test workflow — this is a Unity Editor project. Changes are verified by opening the project in the Unity Editor and pressing Play, or by using the `run` skill to launch/screenshot the app.

There is no automated test suite (`com.unity.test-framework` is a dependency but no test assemblies exist yet).

## Working with this codebase

- Scripts live entirely under `Assets/Scripts/` — the whole gameplay codebase is currently ~11 files, small enough to read in full rather than guess from partial context.
- Do not edit generated/IDE files: `*.csproj`, `Analog-Override.slnx`, `Library/`, `Logs/`, `UserSettings/`, `ProjectSettings/` (except when a deliberate project-settings change is requested).
- MonoBehaviour fields exposed via `[SerializeField]` are wired to prefabs/scenes in the Unity Editor (Inspector references, event hookups like button `OnClick`). Renaming or removing a serialized field, or changing a public method signature invoked from the Inspector (e.g. `PauseMenu.Pause`/`Resume`/`ResetLevel`/`QuitGame` wired to UI buttons), breaks that wiring silently — it won't show up as a compile error, only as a missing reference/null method in the Editor. Flag this risk when making such changes.
- This codebase leans heavily on descriptive XML-doc comments to explain *why*, not just *what* — match that style when touching these files rather than leaving logic undocumented.

## Architecture

The game is a top-down grid where movement, pushing, and interaction are all mediated through one system rather than raw physics/transform manipulation.

### Grid system (`Assets/Scripts/Grid/`, namespace `AnalogOverride.GridSystem`)

- **`GridManager`** — scene singleton (`GridManager.Instance`, `[DefaultExecutionOrder(-100)]` so it initializes before anything reads it). Single source of truth for:
  - World↔cell conversion, delegating to a Unity `Grid` component.
  - Static walkability, read from an optional collision `Tilemap`.
  - Dynamic occupancy: a 2D array of `IGridOccupant`, indexed by cell minus `origin` (never index it directly — always go through `TryPlaceOccupant`/`RemoveOccupant`/`TryMoveOccupant`/`GetOccupant`).
  - Height: a stack of `Tilemap` layers (topmost populated layer wins, default height 0) plus an optional climbable `Tilemap` marking cells that can be entered/exited across a height difference (ladders, cliff edges). With no height layers configured, the grid behaves as fully flat — this is intentionally backward compatible.
  - Fires `CellOccupantChanged` for systems (minimap, AI, fog-of-war) that want to react without polling.
- **`GridEntity`** (`IGridOccupant`) — base class for anything that lives on the grid (player, pushable blocks, future enemies/NPCs). Extend this to get registration, movement, pushing, and bump-interaction for free. Key method: `TryStep(direction)` — see the extensive doc comment on it in `GridEntity.cs` for the full resolution order (walls → occupied-and-pushable → occupied-and-interactable → height/climbing → accept). Pushing recurses through chains (A pushes B pushes C) and returns the summed `Weight` of everything displaced, so callers can charge a cost without knowing chains exist. Movement is logically instant (occupancy/`CurrentCell` update synchronously); the visual slide is a separate coroutine (`moveDuration`) — always read `CurrentCell`, never derive position from `transform.position`, which lags during the tween.
  - Also optionally owns per-entity sprite sort order, keyed off world Y, so entities always draw correctly relative to each other independent of the render pipeline's distance sort.
- **`IGridOccupant`** — minimal contract (`CurrentCell`, `IsPushable`) for anything occupying a cell.
- **`IInteractable`** — implement on a non-pushable occupant (door, NPC, chest) to react when bumped; bumping never moves the mover into the occupant's cell.

### Entities (`Assets/Scripts/Entities/`, namespace `AnalogOverride.Entities`)

- **`PushableBlock`** — minimal `GridEntity` subclass: `pushable = true`, `climbHeight = 0` by default (a pushed block doesn't climb ledges unless raised on a specific instance/prefab). Deliberately kept logic-free; add behavior (pressure plates, breaking, trails) as a separate component on the same GameObject rather than growing this class.

### Player (`Assets/Scripts/Player/CharacterController.cs`)

- Extends `GridEntity`. Reads WASD every `Update()` (only one direction at a time; no diagonals) and calls `TryStep`. Drives an `Animator` "Direction" int. Bails out early if `GameManager.IsGamePaused`.
- Ties grid movement into the energy/spring system: every `stepsPerBar` plain steps costs 1 `SpringManager` bar; pushing something costs `pushedWeight * energyCostPerWeight` bars instead (and does not advance the plain step counter).
- On spawn, teleports to `GameManager.RespawnCell` *before* calling `base.Start()` (which snaps to the grid and claims the cell), if a checkpoint has been set.

### Managers (`Assets/Scripts/Managers/`, no namespace)

- **`GameManager`** — cross-scene singleton (`DontDestroyOnLoad`, `[DefaultExecutionOrder(-60)]`, runs after `GridManager`). Owns checkpoint bookkeeping (`RespawnCell`, `HasCheckpoint`, tracks all live `Checkpoint`s and deactivates the others when a new one activates) and centralized pause state (`SetPauseState` drives `Time.timeScale`). `ReloadScene()` always unpauses before reloading, to avoid a frozen reload.
- **`SpringManager`** — scene singleton (`[DefaultExecutionOrder(-50)]`, runs after `GameManager` but before default scripts). Tracks an energy/"bars" resource (`ReduceBars`, `ResetBars`) with `BarsChanged`/`BarsReachedZero` events. Hitting zero is the death/game-over trigger — `CharacterController` subscribes and reloads the scene.

### UI (`Assets/Scripts/UI/`, no namespace)

- **`PauseMenu`** — reads/writes pause state through `GameManager`, never touches `Time.timeScale` directly except as a fallback when `GameManager.Instance` is null. `Pause`/`Resume`/`ResetLevel`/`QuitGame` are wired from Inspector button `OnClick` events (see the "don't rename Inspector-wired methods silently" note above).
- **`UiManager`** — purely reactive to `SpringManager.BarsChanged`; renders the energy bar as a visual "spring" by lerping `HorizontalLayoutGroup.spacing` between compressed (full energy) and released (empty) and force-rebuilding the affected `RectTransform`s. Spawns its bar segments (`barPrefab`) at `Start()` based on `MaxBars`, destroying any placeholder children first.

### Misc

- **`Checkpoint`** (`Assets/Scripts/Checkpoint.cs`, top-level, uses `AnalogOverride.GridSystem`) — caches its own grid cell at `Start()`, registers/deregisters with `GameManager` on enable/disable. On player trigger-enter: always resets the player's step counter and the spring bars, and additionally saves respawn state + fires an "activate" animator trigger only if not already active. `GameManager.ActivateCheckpoint` then deactivates every other checkpoint.

## Singleton/init order

Execution order matters across these singletons and is enforced via `[DefaultExecutionOrder]`: `GridManager` (-100) → `GameManager` (-60) → `SpringManager` (-50) → everything else. If you add a new manager that other `Start()`-time code depends on via `Instance`, give it an explicit `DefaultExecutionOrder` earlier than its consumers rather than relying on scene object order.
