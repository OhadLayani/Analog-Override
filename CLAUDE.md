# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

A 2D top-down grid puzzle game built in Unity 6000.3.21f1 (Universal Render Pipeline). This is a Unity Editor project: there is no CLI build, and there is no automated test suite, and none should be added (`com.unity.test-framework` is installed, but no test assemblies exist).

- All gameplay code lives under `Assets/Scripts/` (~25 files). The codebase is small enough to read the relevant files in full instead of guessing from partial context.
- Scenes in Build Settings, in order: `Tutorial` (0), then `SampleScene` (1). `SecondPuzzleScene` exists but is **not** in the build. After the last level, `GameManager.LoadNextScene` loops back to `"Tutorial"`.

## Game design

The player is a wind-up robot on a top-down grid. Its energy is a spring, shown as "bars": each action unwinds it, and charging stations (checkpoints) wind it back up. Puzzles are about moving and pushing efficiently before the spring runs out.

- **Walking:** costs 1 bar every 3 steps on normal floor, or every 2 steps on high-friction terrain (carpet). Tapping a direction the robot isn't facing only turns it in place, for free; holding the key walks.
- **Pushing:** costs `weight × energyCostPerWeight` bars right away and doesn't count toward the step counter. The cost is scaled up when the player ends the push on carpet.
- **Attacking:** costs 3 bars per attack (`energyCost`), even if that empties the spring and kills the player.
- **Tall objects:** a target one level above the player (on a raised tile, or standing on furniture like the desk lamp) can only be hit at full stretch. Two or more levels up can never be hit.
- **Checkpoints:** touching one refills the bars, resets the step counter and saves the respawn point.
- **Death:** at 0 bars the scene reloads and the player respawns at the last checkpoint.
- **Keys and doors:** a Key opens every Door with the same `keyId`. Keys are kept on death and cleared by a full level reset.
- **Stretching:** hold Space while standing still. The robot grows taller (mode 1 → 2 → 3) and stays at full height while Space is held; releasing contracts it (3 → 2 → 1). Costs 1 bar to start. While stretched there's no walking or turning; only the attack works.

**Terms:** *bars / spring* (energy), *checkpoint* (charging station), *high-friction* (carpet tiles), *weight* (how much energy a pushable costs), *keyId* (what pairs keys with doors), *stretch* (the body growing taller; separate from the attack's *arm stretch*).

## Mechanics

Each line gives the mechanic, a short description and its main scripts. **This list is also the regression checklist** (see "Verification"). When a mechanic is added or changed, propose the update to this list in the end-of-session review.

- **Grid movement:** WASD, one cell per step, no diagonals. The logical move is instant and the visual slide follows. From standing still, a tap in a new direction only turns the robot (no energy); it walks once the key is held past `turnHoldSeconds`. Pressing the faced direction, or turning mid-walk, steps at once. No turning while stretched or stunned. *(GridEntity, CharacterController)*
- **Walls:** cells painted on the collision tilemap can't be entered. *(GridManager.IsWalkable)*
- **Pushing:** one object at a time, never a chain. If anything is behind the pushed object, the push fails. Costs weight-based energy. *(GridEntity.TryStep / TryBePushed)*
- **Height levels:** the level comes from a stack of tilemap layers. Nothing can step or be pushed between levels. *(GridManager, GridEntity)*
- **High-friction terrain (carpet):** costs more energy per step and gives a slower, dragging slide. *(GridManager.IsHighFriction, CharacterController)*
- **Spring energy:** steps, pushes, stretches and attacks drain bars. Reaching exactly 1 bar is logged as a "last bar" moment (steps, pushes and stretches, all through `CharacterController.ChargeBars`; not attacks). *(SpringManager, CharacterController, PlayerAttack)*
- **Death & respawn:** 0 bars reloads the scene, and the player respawns at the last checkpoint. *(CharacterController, GameManager.ReloadScene)*
- **Checkpoints:** refill bars and reset the step counter; only one checkpoint is active at a time. *(Checkpoint, GameManager)*
- **Keys & doors:** a door opens only for a key with a matching `keyId`. A door can span several cells. Keys are not used up. *(Key, Door, GameManager)*
- **Attack:** a mouse click charges energy and stretches both arms (stage 1 → 2 → 3 → 2 → 1 over `attackDuration`). The hitbox follows the drawn arms sprite and damages any `IAttackable` it touches. A target's cell is its own grid cell, or the cell of the grid object it rides on. Targets one level up need full stretch. *(PlayerAttack, ArmsVisual, AttackHitbox, HeightOffset)*
- **Arms visual:** the arms sprite is picked by facing (front/back or profile) and attack stretch stage, with per-facing offset and scale, and is raised while walking and while the body is stretched. *(ArmsVisual)*
- **Body stretch:** hold-to-stretch (see Game design). The body sprite switches to the stretch frames, and the arms and key rise with it. No walking or turning until fully contracted. *(CharacterController, StretchVisual, ArmsVisual, SpringKeyAnimator)*
- **Push-off objects:** a stand-in on a shelf or table. A spinning desk lamp knocks it away: it flies to its `sweptAwayReplacement` (e.g. a real Key placed on the floor, switched off), switches that on and itself off. A stand-in whose replacement key is already collected switches itself off on load, so after a death it doesn't return; a knocked-down key that wasn't picked up is back on the table after a death. The reach route (`Interact` → `fallenVariant`) still has no trigger. *(PushOffObject, ISweepable, DeskLamp, Key)*
- **Desk lamp:** a lamp riding on a pushable table, one level tall. When the arms touch its head circle at full stretch, it spins once (4 frames, counter-clockwise) and its head circle swings with the frames, reporting what it sweeps into through `SweptInto` and calling `SweptBy` on anything `ISweepable` on its own level (e.g. a stand-in key on a neighbouring table). *(DeskLamp, HeightOffset, PlayerAttack, ISweepable)*
- **Furniture:** solid pieces of any size and shape. Every cell whose centre the footprint collider covers acts like a wall for walking, pushing, knockback, the Roomba and landing spots. Draw order comes from the footprint's lowest row. *(Furniture, GridManager.BlockCell, SortAbove)*
- **Stage goal & next level:** reaching the goal shows the stage-over screen and pauses the game. The next level comes from the Build Settings order. *(StageGoal, StageOverScreen, GameManager)*
- **Level reset:** a full restart that clears the checkpoint and keys, then reloads the scene. *(GameManager.ResetLevel, PauseMenu, StageOverScreen)*
- **Pause:** Esc toggles pause; time scale goes to 0 and player input is blocked. *(PauseMenu, GameManager)*
- **Tutorial checklist:** four tasks (move, checkpoint, reset, push) unlock the Start button, and progress survives a reset. *(TutorialManager)*
- **Spring UI & key visual:** the on-screen spring and the key on the robot's back follow the energy level; the key is raised while the body is stretched. *(UiManager, SpringKeyAnimator)*
- **Draw order:** entities are sorted by world Y so they overlap correctly. *(GridEntity, Furniture, SortAbove, SpringKeyAnimator, ArmsVisual)*

## How we work

- **Plan first, then approval.** For any code task: read the relevant code, propose a plan, ask clarifying questions **one at a time**, and write no code until Tsah says OK.
- **Explain shell commands** in plain words before running them.
- **Git is Tsah's job.** Never commit, push, create branches or open PRs.
- **Editor work.** When a change needs work in the Unity Editor (wiring Inspector fields, adding components, tilemaps, prefabs), give numbered step-by-step instructions: which GameObject, which component, which field, what to assign. Never edit `.unity` / `.prefab` / `.asset` YAML directly.
- **Language.** Reply in whichever language Tsah writes in. Code, comments and identifiers are always in English.
- **Furniture.** Whenever Tsah asks to add a piece of furniture, guide Tsah through the recipe below (numbered steps, adapted to that piece). Pushable furniture is the exception: it stays a one-cell `PushableBlock`.

### Editor steps: the recipe for any furniture

1. **Create the object:** a GameObject with a **SpriteRenderer** (the furniture art). Place it wherever it looks right; it doesn't need to line up with the grid.
2. **Add `Furniture`:** set **Sorting Sprite** to its own SpriteRenderer, and leave the two number fields at their defaults.
3. **Add a `BoxCollider2D`:**
   - Turn on **Is Trigger**.
   - Click **Edit Collider** and drag the box over the furniture's **floor footprint**. For a closet that's its base, not its tall back.
   - The red squares show what's solid: a cell turns red once the box covers its **centre**. Adjust until exactly the right cells are red.
   - For an L-shape, add more BoxCollider2Ds to the same GameObject, or use one PolygonCollider2D.
4. **Things on top** (a vase, a stand-in key): make them **child objects**. Give each one `SortAbove` with **Target** = the furniture's SpriteRenderer.

### Verification

- **Code-only change:** Claude checks first: re-reads the changed code, traces its callers, and looks for compile errors. Then Tsah plays it in the Editor.
- **Compile check without Unity:** Claude can compile `Assets/Scripts` with Unity's bundled compiler: `dotnet <Unity.app>/Contents/Resources/Scripting/DotNetSdkRoslyn/csc.dll @<scratchpad>/csc.rsp`. The Unity install path has spaces, so the references go in a response file. It lists `-nostdlib -target:library -langversion:9`, a `-r:` line for `Resources/Scripting/NetStandard/ref/2.1.0/netstandard.dll`, every `Resources/Scripting/Managed/UnityEngine/*.dll`, and `Library/ScriptAssemblies/*.dll` except `Assembly-CSharp*`, then every `Assets/Scripts/**/*.cs`, each path in quotes. Output goes to the scratchpad.
- **Change that needs Editor work:** Tsah does the Editor steps and tests first, then Claude reviews.
- **Regression check (every time Claude checks a code change):**
  1. Go through the Mechanics list. For each mechanic that touches the changed code (shared base classes like `GridEntity`, events, singletons, callers), trace whether the change can affect it.
  2. Report each one as "unaffected" or "possibly affected — why".
  3. End with a short play-test checklist of the mechanics Tsah should re-try in the Editor.

### End-of-session review

When Tsah says the session is over, propose edits to this file based on what happened in the session.
- Focus on lasting, big-picture items: new or changed mechanics, architecture, conventions, workflow rules.
- Leave out details that only mattered for this session.
- Present the proposal to Tsah as a very concise bullet list, one short line per edit. Only this summary is short; the text written into this file keeps its usual detail.
- Tsah approves, corrects, or says no update is needed. Edit this file only after approval.

## Code conventions

- **MonoBehaviour only:** every script inherits from `MonoBehaviour` or a subclass of it. No plain C# logic classes and no test assemblies. The existing interfaces (`IGridOccupant`, `IInteractable`, `IAttackable`, `ISweepable`) are the only exception.
- **Doc comments:** a short one-line `/// <summary>` on new classes and non-obvious members. Leave the existing longer comments as they are.
- **Namespaces:** match the other files in the same folder. `Grid/` uses `AnalogOverride.GridSystem`; `Entities/` and `Combat/` use their own `AnalogOverride.*` namespaces; `Managers/`, `Player/` and most of `UI/` have no namespace.
- **Input:** keep using the legacy `Input.GetKey` / `Input.GetMouseButtonDown` API for consistency, even though the Input System package is installed.
- **Analytics:** don't add or change `AnalyticsLogger` events unless asked.
- **Optional singletons:** some singletons only exist in some scenes (`TutorialManager`, `StageOverScreen`) or create themselves at startup (`AnalyticsLogger`). Call them as `X.Instance?.Method()`.
- **Pause guard:** gameplay `Update()`s return early when `GameManager.Instance.IsGamePaused` is true.
- **Grid position:** always read `CurrentCell`, never derive a cell from `transform.position` (it lags during the slide).
- **Extend by composition:** add new behavior as a separate component, rather than growing `PushableBlock` or `GridEntity`.
- **Furniture:** fixed pieces use `Furniture` (see the recipe under "How we work"); pushable pieces are a one-cell `PushableBlock`.
- **Objects on furniture:** make them a child of the piece they stand on, never a `GridEntity` themselves (a cell holds only one occupant), and give them `SortAbove`. On a pushable table their cell is the parent's `CurrentCell`; on fixed `Furniture` it's the cell under their pivot, so keep the pivot over the piece. Give them a `HeightOffset` (usually 1) so they count as being on top.
- **Stand-in keys:** a stand-in's identity comes from its replacement Key, so give that Key the same Key Id as the UI (`UiManager.keyId`) and the Door. Never put a `Key` component on the stand-in itself.
- **`TakeDamage` isn't a generic "hit":** `Key.TakeDamage` collects the key. For anything other than the player's attack (like the lamp's sweep), raise an event or use a dedicated hook instead.
- **Inspector wiring risk:** renaming or removing a `[SerializeField]` field, or changing a public method wired to a button `OnClick`, still compiles, but the Inspector loses the reference without any error. Inspector-wired methods include `PauseMenu.Pause/Resume/ResetLevel/QuitGame`, `StageOverScreen.RestartLevel/NextLevel` and `SceneNavigationButtons.LoadNextScene`. Flag this whenever such a change is proposed.
- **Don't edit generated files:** `*.csproj`, `Analog-Override.slnx`, `Library/`, `Logs/`, `UserSettings/`, `ProjectSettings/` (except for a deliberate project-settings change).

## Architecture

### Grid (`Assets/Scripts/Grid/`, `AnalogOverride.GridSystem`)

- **`GridManager`**: scene singleton and the single source of truth for the grid.
  - World↔cell conversion through Unity's `Grid` component. Snap to `CellToWorld` (the cell center).
  - Optional tilemaps: collision (walls), `heightLayers` (the topmost populated layer wins; default height 0), and friction. With none set, the grid is flat and open.
  - `CellSize` gives a cell's world size (for physics overlaps).
  - Occupancy: a 2D array indexed by `cell - origin`. Only access it through `TryPlaceOccupant` / `RemoveOccupant` / `TryMoveOccupant` / `GetOccupant` / `IsFree`.
  - Raises `CellOccupantChanged`.
  - Blocked cells: `BlockCell` / `UnblockCell` (counted, so overlapping pieces don't free each other's cells), used by `Furniture`. `IsWalkable` treats a blocked cell like a wall.
- **`GridEntity`** (`IGridOccupant`): base class for anything on the grid. It registers itself in `Start`, frees its cell in `OnDisable`, and moves with `TryStep(dir, out pushedWeight)`. Resolution order:
  1. Off-grid or wall → refused.
  2. A different height → refused, whether the cell is occupied or not.
  3. Occupied cell: pushable → `TryBePushed`, which only succeeds into a completely free cell on the same level (no chains); `IInteractable` → `Interact()` and the mover stays put; anything else → refused.
  4. Otherwise accepted: `CurrentCell` and occupancy update immediately, and a coroutine plays the slide.

  `TryReach(dir, maxLevelsUp, out pushedWeight)` acts on the adjacent cell 1..N levels up without moving: pushes a pushable along its level, interacts with an `IInteractable` occupant, or falls back to a physics overlap for non-grid objects like `Key`. It has **no caller**; the stretch + attack works through `IAttackable` instead.

  `IsMoving` blocks new steps until the slide finishes. Subclasses can set `MoveDuration` / `MoveCurve` **before** calling `TryStep` to change that one step's slide. The optional `sortingSprite` gets its order in layer from world Y.
- **`IGridOccupant`**: the minimal contract (`CurrentCell`, `IsPushable`).
- **`IInteractable`**: a bump hook for occupants that can't be pushed.
- **`HeightOffset`**: makes an object count as N levels above its cell's height. `LevelOf` gives an object's level (its own or parent `GridEntity`'s cell, else the cell under its pivot, plus the offset). Used by the attack's height rule and the lamp's sweep.

### Entities (`Assets/Scripts/Entities/`, `AnalogOverride.Entities`)

- **`PushableBlock`**: a `GridEntity` with no extra logic; `Reset()` defaults it to `pushable = true`.
- **`PushOffObject`**: not a `GridEntity` (it doesn't occupy its cell); reacts through `IInteractable`. When reached, it spawns `fallenVariant` at `landingSpot`, or else at the nearest lower cell in the push direction, then destroys itself.
  - Also `ISweepable`: `SweptBy` (only with a `sweptAwayReplacement` assigned) flies it to the replacement over `flySeconds`, then switches the replacement on and itself off. `Start` switches it off if the replacement is a Key that's already collected.
- **`DeskLamp`**: `IAttackable`, not a `GridEntity`; a child of the table it stands on. On a hit it plays its spin frames once, moving a trigger `CircleCollider2D` to each frame's `headOffsets` entry (shown as Scene-view circles when selected). Every frame it checks what the circle overlaps and raises `SweptInto` once per object per spin, skipping its own table and the player. Its draw order follows the table's sprite plus an offset.
- **`ISweepable`**: `SweptBy(DeskLamp)`, implemented by anything that reacts to a spinning lamp's head.
- **`Furniture`**: not a `GridEntity`. In `Start` it blocks every cell whose centre lies inside one of its own enabled colliders (not its children's), and frees them in `OnDisable`. It sets its `sortingSprite` order from the lowest footprint row, one step behind so the player wins a tie. `OnDrawGizmosSelected` shows the blocked cells as red squares.
- **`Door`**: a non-pushable `IInteractable` that opens when bumped, if `GameManager.HasKey(keyId)`. It can occupy extra cells through `extraCells`, which are claimed in `Start` and freed in `OnDisable`. `disableOnOpen` either deactivates the GameObject or just frees all of its cells.

### Player (`Assets/Scripts/Player/`)

- **`CharacterController`** (`GridEntity`):
  - Movement: reads WASD through `ReadHeldDirection()` (one direction at a time), updates `FacingDirection`, then calls `TryStep`. On spawn it teleports to `GameManager.RespawnCell` *before* `base.Start()`. Turn in place: `UpdateTurnInPlace` sets `FacingDirection` and starts `turnHoldTimer` when a new direction is pressed from still; `ResetTurnInput` clears it during stretch and stun.
  - Energy: a plain step increments `stepCounter`; at `stepsPerBar` (or `stepsPerBarHighFriction` on carpet) it charges 1 bar. A push charges `max(1, round(weight × energyCostPerWeight × frictionMultiplier))`, where the multiplier is `stepsPerBar / stepsPerBarHighFriction` on carpet. All its costs go through `ChargeBars`, which also logs `LAST_BAR`.
  - Stretch: `stretchStage` / `stretchTarget` / `stretchTimer`, driven by `BeginStretch()` and `TickStretch(held)`. Space is read with `GetKey`, so releasing it during pause is still noticed on resume. Exposes `IsStretching` (blocks walking and turning) and `StretchStage` (0/1/2) for visuals. Ohad's `ToggleStretch`, `HandleStretchInput` (WASD reach), `stretchReach` and Animator `Direction` 40–43 are commented out.
  - Carpet slide: a step that **starts** on carpet uses `highFrictionMoveDuration` / `highFrictionMoveCurve`.
  - Exposes `IsWalking` and `StepProgress` (0..1) for visuals.
  - Death: subscribes to `SpringManager.BarsReachedZero`, then logs the death and calls `GameManager.ReloadScene()`.
  - Animator int `Direction`: walking down/up/right/left = 0/1/2/3; idle = 5/10/20/30. The idle states also need **Any State** transitions (Direction = 5/10/20/30, no exit time, duration 0, Can Transition To Self off), so turning in place can switch from one idle to another without passing through a walk state.
- **`PlayerAttack`**: on left click, charges `energyCost` bars, turns on the `AttackHitbox` child, and steps `ArmsVisual.Stage` through out → hold → back over `attackDuration`. `fullStretchShare` is the share of that time spent at full stretch, and the duration is also the cooldown. Damage is applied through the hitbox's `TargetDetected` event.
  - It hits two ways: the **cell check** (`StrikeReachedCells`; only targets that belong to the checked cell) and the **arms hitbox** (filtered by `IsInReach`: facing up or down, only the player's own row).
  - `CellOf` gives a target's cell (its own `GridEntity`, the one it rides on, or the cell under its pivot); both filters and `CanReachHeight` use it.
- **`ArmsVisual`** (on `idleArms`, always active): picks the sprite from `FacingDirection` + `Stage`, so turning mid-attack keeps the stage. It sets the per-facing offset, scale and walk Y offset, copies the body's sorting order plus an offset, and fits the `AttackHitbox` collider (a child of `idleArms`) to the drawn sprite each frame.
  - Owns the arms' Transform position and scale every frame, so tune them through its fields, not the Transform.
  - `bodyStretchFrontBackYOffsets` / `bodyStretchSideYOffsets` (element 0 = half, 1 = full) are added on top of the walk offset while the body is stretched.
- **`StretchVisual`** (on `Player Sprite`): while `StretchStage > 0`, overrides the body sprite and `flipX` in `LateUpdate` from the front/back/side frame arrays. This works because the Animator (Write Defaults on) runs before `LateUpdate`; at stage 0 the Animator owns the body again. The stretch sprites keep a **Center pivot on purpose**, so the legs extend downward.
- **`SpringKeyAnimator`**: animates the key on the robot's back in 4-frame loops, driven by spent bars plus `StepProgress`.
  - Spending energy plays frames forward. A recharge spins the key backwards for `chargeLoops` loops. A large backlog of frames switches to a faster frame rate.
  - Hidden while facing down; flipped for left.
  - Raised by `bodyStretchBackYOffsets` / `bodyStretchSideYOffsets` while the body is stretched.
  - Copies the body sprite's sorting order each frame, plus an offset.

### Combat (`Assets/Scripts/Combat/`, `AnalogOverride.Combat`)

- **`IAttackable`**: `IsAlive`, `TakeDamage(int)`.
- **`AttackHitbox`**: only detects hits. Its trigger raises `TargetDetected(IAttackable)`, and the listener decides what to do.
- **`TestDummy`**: a temporary target that stands in until real enemies exist.

### Managers (`Assets/Scripts/Managers/`, no namespace)

- **`GameManager`**: persists across scene loads (`DontDestroyOnLoad`).
  - Checkpoints: `RespawnCell`, `HasCheckpoint`; it registers checkpoints and deactivates the others when one activates.
  - Keys: a `HashSet<string>` with `HasKey` / `CollectKey` / `ClearKeys`.
  - Pause: `SetPauseState` drives `Time.timeScale`.
  - Scene flow:
    - `ReloadScene()`: death respawn; keeps the checkpoint and keys.
    - `ResetLevel()`: full restart; logs it, clears the checkpoint and keys, then reloads.
    - `LoadNextScene()`: next scene in the build order.
  - Every scene load unpauses first.
- **`SpringManager`**: scene singleton for the bars.
  - `ReduceBars(n)` and `ResetBars()`.
  - Events: `BarsChanged(int)`, `BarsReachedZero`.
- **`TutorialManager`**: exists only in the Tutorial scene. Its 4 task flags are **static**, so they survive the reload on reset, and `Start()` re-applies them to the check images. The Start button becomes interactable when all four are done.
- **`AnalyticsLogger`**: creates itself at startup (`RuntimeInitializeOnLoadMethod`, `DontDestroyOnLoad`), so it is never placed in a scene.
  - Appends `DEATH` / `CHECKPOINT_VISIT` / `STAGE_RESET` / `STAGE_COMPLETE` / `LAST_BAR` / `QUIT` lines to `Application.persistentDataPath/analytics_log.txt`.
  - Writes a summary for each run on quit. The run number is stored in `PlayerPrefs`.

### UI (`Assets/Scripts/UI/`)

- **`PauseMenu`**: Esc toggles pause through `GameManager`. Its button methods are wired in the Inspector. `ResetLevel` notifies the tutorial before it calls `GameManager.ResetLevel()`.
- **`StageOverScreen`**: scene singleton. `Show()` logs the stage completion, shows the panel and pauses. `RestartLevel` / `NextLevel` are wired to buttons.
- **`SceneNavigationButtons`** (`AnalogOverride.UI`): a button hook for `LoadNextScene`.
- **`UiManager`**: spawns `MaxBars` spring segments and blends the layout spacing between compressed (full energy) and released (empty) on `BarsChanged`. It also shows the key icon through `Key.KeyCollected`, or on load if the key is already collected.

### Misc (`Assets/Scripts/`, no namespace)

- **`Checkpoint`**: trigger. On player contact it logs the visit, notifies the tutorial, resets the step counter and the bars, and activates itself (saving the respawn point and firing the `activate` animation) only if it isn't already active.
- **`Key`**: trigger, and also an `IInteractable`, so it can be collected by a reach (guarded against collecting twice). On pickup it calls `GameManager.CollectKey(keyId)` and raises the static `KeyCollected` event before destroying itself. In `Start` it destroys itself without raising the event if the key was already collected. Also `IAttackable` (an attack collects it). `KeyId` exposes its id.
- **`SortAbove`**: copies a target sprite's order in layer plus `offset` every `LateUpdate`, for things standing on furniture.
- **`StageGoal`**: trigger. The first time the player touches it, it calls `StageOverScreen.Instance.Show()`.

## Singleton / init order

`[DefaultExecutionOrder]` enforces: `GridManager` (-100) → `GameManager` (-60) → `SpringManager` (-50) → `TutorialManager` (-40) → default scripts. `AnalyticsLogger` creates itself before the first scene loads. A new manager that other `Start()`-time code reads through `Instance` needs an explicit execution order earlier than the code that uses it; don't rely on the order of objects in the scene.
