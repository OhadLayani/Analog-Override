# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

A 2D top-down grid puzzle game built in Unity 6000.3.21f1 (Universal Render Pipeline). This is a Unity Editor project: there is no CLI build, and there is no automated test suite, and none should be added (`com.unity.test-framework` is installed, but no test assemblies exist).

- All gameplay code lives under `Assets/Scripts/` (~25 files). The codebase is small enough to read the relevant files in full instead of guessing from partial context.
- Levels: the 1st level is the `Level1-Attic` scene and the 2nd level is the `Level2-Bedroom` scene, both played after `Tutorial`.
- Scenes in Build Settings, in order: `MainMenu` (0), `Tutorial` (1), `Level1-Attic` (2), `Level2-Bedroom` (3). The game opens on the Main Menu, and after the last level `GameManager.LoadNextScene` returns to it (build index 0).

## Game design

The player is a wind-up robot on a top-down grid. Its energy is a spring, shown as "bars": each action unwinds it, and charging stations (checkpoints) wind it back up. Puzzles are about moving and pushing efficiently before the spring runs out.

- **Walking:** costs 1 bar every 3 steps on normal floor, or every 2 steps on high-friction terrain (carpet). Tapping a direction the robot isn't facing only turns it in place, for free; holding the key walks.
- **Pushing:** costs `weight × energyCostPerWeight` bars right away and doesn't count toward the step counter. The cost is scaled up when the player ends the push on carpet.
- **Attacking:** costs 3 bars per attack (`energyCost`), even if that empties the spring and kills the player.
- **Tall objects:** a target one level above the player (on a raised tile, or standing on furniture like the desk lamp) can only be hit at full stretch. Two or more levels up can never be hit.
- **Checkpoints:** touching one refills the bars and resets the step counter. They are not respawn points.
- **Death:** at 0 bars the level fully resets, exactly like Reset Level from the pause menu (player back at the start, no keys, every object in its original place). The only difference is the log line: `DEATH` instead of `STAGE_RESET`. The Tutorial has no death: at 0 bars the spring stays empty, every action is free, and play goes on.
- **Keys and doors:** a Key opens every Door with the same `keyId`. Keys are cleared on death and on a level reset.
- **Fences:** hitting a fence with the attack opens it for good, freeing part of the cells it blocked. A level reset or death closes it again.
- **Stretching:** hold Space while standing still. The robot grows taller (mode 1 → 2 → 3) and stays at full height while Space is held; releasing contracts it (3 → 2 → 1). Costs 1 bar to start. While stretched there's no walking or turning; only the attack works.

**Terms:** *bars / spring* (energy), *checkpoint* (charging station), *high-friction* (carpet tiles), *weight* (how much energy a pushable costs), *keyId* (what pairs keys with doors), *stretch* (the body growing taller; separate from the attack's *arm stretch*).

## Mechanics

Each line gives the mechanic, a short description and its main scripts. **This list is also the regression checklist** (see "Verification"). When a mechanic is added or changed, propose the update to this list in the end-of-session review.

- **Grid movement:** WASD, one cell per step, no diagonals. The logical move is instant and the visual slide follows. From standing still, a tap in a new direction only turns the robot (no energy); it walks once the key is held past `turnHoldSeconds`. Pressing the faced direction, or turning mid-walk, steps at once. No turning while stretched or stunned. *(GridEntity, CharacterController)*
- **Walls:** cells painted on the collision tilemap can't be entered. *(GridManager.IsWalkable)*
- **Pushing:** one object at a time, never a chain. If anything is behind the pushed object, the push fails. Costs weight-based energy. Each successful push plays the push sound once (per cell, on top of the footsteps); a failed push is silent. *(GridEntity.TryStep / TryBePushed, CharacterController)*
- **Height levels:** the level comes from a stack of tilemap layers. Nothing can step or be pushed between levels. *(GridManager, GridEntity)*
- **High-friction terrain (carpet):** costs more energy per step and gives a slower, dragging slide. *(GridManager.IsHighFriction, CharacterController)*
- **Spring energy:** steps, pushes, stretches and attacks drain bars. *(SpringManager, CharacterController, PlayerAttack)*
- **Death:** 0 bars logs `DEATH` and fully resets the level (clears checkpoint, keys and room snapshot, then reloads). Turned off per scene with `SpringManager`'s **Death At Zero** (off in the Tutorial). *(CharacterController, GameManager.RestartLevel, SpringManager)*
- **Checkpoints:** refill bars and reset the step counter; only one checkpoint is active at a time (activation animation). Not a respawn point. Each station has limited charges (`maxUses`, shown by its full/half/empty sprites); an empty station does nothing (no refill, no log, no sound). Charges come back on every scene load. *(Checkpoint, GameManager)*
- **Keys & doors:** a door opens only for a key with a matching `keyId`. A door can span several cells. Keys are not used up. A bump with the key plays the door-open sound; the door stays visible and keeps blocking until the clip ends (`AudioManager.DoorOpenSeconds`), then disappears. Bumps during that wait do nothing; with no clip it opens at once. *(Key, Door, GameManager, AudioManager)*
- **Attack:** a mouse click charges energy and stretches both arms (stage 1 → 2 → 3 → 2 → 1 over `attackDuration`). The hitbox follows the drawn arms sprite and damages any `IAttackable` it touches. A target's cell is its own grid cell, or the cell of the grid object it rides on. Targets one level up need full stretch. *(PlayerAttack, ArmsVisual, AttackHitbox, HeightOffset)*
- **Arms visual:** the arms sprite is picked by facing (front/back or profile) and attack stretch stage, with per-facing offset and scale, and is raised while walking and while the body is stretched. *(ArmsVisual)*
- **Body stretch:** hold-to-stretch (see Game design). The body sprite switches to the stretch frames, and the arms and key rise with it. No walking or turning until fully contracted. *(CharacterController, StretchVisual, ArmsVisual, SpringKeyAnimator)*
- **Push-off objects:** a stand-in on a shelf or table. A spinning desk lamp knocks it away: it flies to its `sweptAwayReplacement` (e.g. a real Key placed on the floor, switched off), switches that on and itself off. A stand-in whose replacement key is already collected switches itself off on load; since death and reset both clear the keys, after either one the stand-in is always back on the table. The reach route (`Interact` → `fallenVariant`) still has no trigger. *(PushOffObject, ISweepable, DeskLamp, Key)*
- **Desk lamp:** a lamp riding on a pushable table, one level tall. When the arms touch its head circle at full stretch, it spins once (4 frames, counter-clockwise) and its head circle swings with the frames, reporting what it sweeps into through `SweptInto` and calling `SweptBy` on anything `ISweepable` on its own level (e.g. a stand-in key on a neighbouring table). *(DeskLamp, HeightOffset, PlayerAttack, ISweepable)*
- **Furniture:** solid pieces of any size and shape. Every cell whose centre the footprint collider covers acts like a wall for walking, pushing, knockback, the Roomba and landing spots. Draw order comes from the footprint's lowest row. *(Furniture, GridManager.BlockCell, SortAbove)*
- **Fence:** fixed furniture that the attack opens, one-shot. It plays closed → middle → open; after the last frame its closed footprint box is swapped for the smaller open box and the freed cells become walkable. Hit from one of its ends, in its row, facing it sideways. Reset and death reload it closed. *(Fence, Furniture.RefreshFootprint)*
- **Wall switch & timed door:** hitting the switch (from the side, in profile) plays the switch sound, flips it on and zooms the camera out (it stays out). If that zoom actually changed the view, the door waits `zoomWaitSeconds` (match the Cinemachine Brain's Default Blend, 0.5 in the levels) so it's in frame; the door is busy from the hit. Then it opens for `openSeconds`: the door plays its open sound and its frames open, frees its cells, then waits until nothing stands in them, turns solid again and plays its close sound and the frames back to closed. Once fully closed, the switch flips back off. Hits while the door is busy are ignored (the attack still costs energy). Reset and death reload it closed. *(WallSwitch, TimedDoor, Furniture)*
- **Roomba dock:** attacking the docking station calls its Roombas (they rush to it, sit flashing, then carry on; busy ones ignore the call). Every hit plays the dock sound and zooms the camera out (it stays out), so the player sees them coming. *(RoombaButton, Roomba, ZoomToggle)*
- **Zoom:** a HUD button switches between the follow camera and the fixed room overview (a higher-priority CinemachineCamera turned on and off). A hit on the wall switch or the Roomba dock zooms out automatically through `ZoomOut()`; that isn't logged as `ZOOM_OUT`. Every load and reset starts zoomed in. *(ZoomToggle)*
- **Stage goal & next level:** reaching the goal shows the stage-over screen and pauses the game. The next level comes from the Build Settings order. **Work in progress:** the stage-over screen isn't finished (its layout and RectTransform stretch values in `Main UI Prefab` aren't final), so don't treat its current setup as done or as a pattern to copy. *(StageGoal, StageOverScreen, GameManager)*
- **Main menu:** its own scene (build index 0) with a full-screen background (Aspect Ratio Fitter, Fit In Parent, so the whole 16:9 art stays visible and lines up with the buttons at any aspect; a dark brown `Backdrop` Image, `#5A3A30`, fills the bars around it) and centered buttons: Play (Level1-Attic), Tutorial, Controls (a hidden panel with an X button; Esc also closes it) and Quit. Loading a level from it clears keys and checkpoint first. The pause menu's and stage-over screen's "Main Menu" buttons, and the end of the last level, all return to it. *(MainMenu, PauseMenu.ReturnToMainMenu, GameManager.LoadMainMenu)*
- **Level reset:** a full restart that logs `STAGE_RESET`, clears the checkpoint and keys, then reloads the scene. Shares `RestartLevel` with death; only the log line differs. *(GameManager.ResetLevel, PauseMenu, StageOverScreen)*
- **Pause:** Esc toggles pause; time scale goes to 0 and player input is blocked. Both the levels' pause menu (`Main UI Prefab`) and the Tutorial's own pause menu have a Controls button that opens a controls image over the buttons, closed with its X or Esc (Esc closes only that screen; the game stays paused). *(PauseMenu, GameManager)*
- **Tutorial checklist:** six tasks (move, checkpoint, open and close the pause menu, push, hold the stretch key 1.5s, attack) unlock the Start button. Progress survives a reset, but is cleared when another scene loads (Main Menu, a level) or a new run starts. The pause-menu task counts when the menu is closed (Esc or Resume) or Reset Level is pressed. *(TutorialManager, PauseMenu)*
- **Spring UI & key visual:** the on-screen spring and the key on the robot's back follow the energy level; the key is raised while the body is stretched. *(UiManager, SpringKeyAnimator)*
- **Draw order:** entities are sorted by world Y so they overlap correctly. *(GridEntity, Furniture, SortAbove, SpringKeyAnimator, ArmsVisual)*
- **Audio:** each scene loops its own music track (by build index); it carries on without restarting through a reset or death. One-shot effects: button click, attack, stretch start, push (each successful push, one per cell), checkpoint charge (only when the station actually charges) and death (when the bars reach 0; it keeps playing through the reload, and there's none in the Tutorial or on Reset Level). Level objects: lamp hit (when the spin starts), key knocked down (when the stand-in's flight lands, not on a snapshot restore), key pickup (once, by any route), Roomba dock hit (every hit), key door open, switch hit, timed door open / close (when its frames start each way) and fence hit (the opening hit). Walking loops a footstep sound while the walk animation shows (not when only turning in place, stretched or stunned); on carpet the carpet sound is layered on top and the footsteps drop to `walkVolumeOnCarpet`. Both loops go quiet on pause and stop on a scene load. Music keeps playing while paused. Volumes are Inspector-only: master Music and SFX, times each clip's own volume. *(AudioManager, ButtonClickSound, CharacterController, PlayerAttack, Checkpoint, Key, Door, DeskLamp, PushOffObject, RoombaButton, WallSwitch, TimedDoor, Fence)*

## How we work

- **Plan first, then approval.** For any code task: read the relevant code, propose a plan, ask clarifying questions **one at a time**, and write no code until Tsah says OK.
- **Tsah works on a MacBook (macOS).** Give Editor and OS instructions the Mac way: Cmd instead of Ctrl (Cmd+C / Cmd+V / Cmd+D, Cmd+S to save), Option instead of Alt (e.g. Option+click for anchor presets), menu paths like **Unity → Settings** instead of Edit → Preferences, Finder instead of Explorer, and macOS paths and shell (zsh).
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

### Editor steps: audio

- The `AudioManager` prefab is placed in all 4 scenes, so every scene has sound when played directly. Change clips and volumes **only on the prefab** (double-click it in the Project window), never on a scene copy: during Play the copy from the first scene entered is the one that survives.
- Values tuned during Play are lost on stop: copy them back with the component's ⋮ → **Copy Component**, then on the prefab ⋮ → **Paste Component Values**.
- Every UI button gets a `ButtonClickSound` component (MainMenu, `Main UI Prefab`, the Tutorial's own pause menu); don't add the click to the button's OnClick list.
- Import settings: music `Streaming` + `Vorbis`; short effects `Decompress On Load` + `ADPCM`; a long effect `Compressed In Memory` + `Vorbis`. Unity can't trim clips: trim them in Audacity and overwrite the file with the same name and format, so its `.meta` and the Inspector reference stay.
- Each scene needs exactly one Audio Listener (on Main Camera).

### Editor steps: shared furniture prefab

Pieces placed from the shared `Prefab furniture` prefab: add any scene-specific components (like `Fence`) on the instance only, and never apply those overrides back to the prefab, or every piece built from it gets them.

### Verification

- **Code-only change:** Claude checks first: re-reads the changed code, traces its callers, and looks for compile errors. Then Tsah plays it in the Editor.
- **Compile check without Unity:** Claude can compile `Assets/Scripts` with Unity's bundled compiler: `dotnet <Unity.app>/Contents/Resources/Scripting/DotNetSdkRoslyn/csc.dll @<scratchpad>/csc.rsp`, where `<Unity.app>` is `/Applications/Unity/Hub/Editor/6000.3.21f1/Unity.app`. The Unity install path has spaces, so the references go in a response file. It lists `-nostdlib -target:library -langversion:9`, a `-r:` line for `Resources/Scripting/NetStandard/ref/2.1.0/netstandard.dll`, every `Resources/Scripting/Managed/UnityEngine/*.dll`, and `Library/ScriptAssemblies/*.dll` except `Assembly-CSharp*`, then every `Assets/Scripts/**/*.cs`, each path in quotes. Output goes to the scratchpad.
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

- **MonoBehaviour only:** every script inherits from `MonoBehaviour` or a subclass of it. No plain C# logic classes and no test assemblies. The exceptions are the existing interfaces (`IGridOccupant`, `IInteractable`, `IAttackable`, `ISweepable`) and `AudioManager.SoundClip`, a nested `[Serializable]` data class (clip + volume) with no logic.
- **Adding a sound:** one `SoundClip` field on `AudioManager` plus a one-line `PlayX()` that goes through `PlaySfx`, called as `AudioManager.Instance?.PlayX()` from the script where it happens. A looping sound needs its own AudioSource (a one-shot can't be stopped).
- **Doc comments:** a short one-line `/// <summary>` on new classes and non-obvious members. Leave the existing longer comments as they are.
- **Namespaces:** match the other files in the same folder. `Grid/` uses `AnalogOverride.GridSystem`; `Entities/` and `Combat/` use their own `AnalogOverride.*` namespaces; `Managers/`, `Player/` and most of `UI/` have no namespace.
- **Input:** keep using the legacy `Input.GetKey` / `Input.GetMouseButtonDown` API for consistency, even though the Input System package is installed.
- **Analytics:** don't add or change `AnalyticsLogger` events unless asked.
- **Scene names:** don't hardcode scene names in code; read them from the scene itself (`scene.name`, `SceneManager.GetActiveScene()`) or use the build index. The one exception is `"Tutorial"`, which `GameManager.LoadNextScene` uses once to detect `TUTORIAL_COMPLETE`; renaming the Tutorial scene means updating it. The Main Menu's target scenes are Inspector fields on `MainMenu`, not code.
- **Screen fit & UI layout:** the game is designed around a 16:9 frame that must stay fully visible on any screen. Wider screens get empty space at the sides; narrower ones zoom out (`ResponsiveCamera`).
  - Canvas Scaler on every canvas: *Scale With Screen Size*, Reference Resolution `924 × 520`, Screen Match Mode **Expand** (the Match slider is ignored in Expand mode). With Expand, a fixed-size, center-anchored panel up to 924 × 520 always fits on screen.
  - Full-screen background art (e.g. the Main Menu's `Background`) uses an Aspect Ratio Fitter set to **Fit In Parent**, never Envelope Parent: Envelope crops the art's edges and grows it relative to the UI on non-16:9 screens. Put a stretched (stretch/stretch anchors) solid-colour `Backdrop` Image behind it, with Raycast Target off, to fill the bars.
  - UI that relates to the world (tutorial notes pointing at the room) is anchored **middle-center**, so it stays locked to the world. Only true HUD elements are anchored to screen edges.
  - Lay out and check UI in the Game view at **16:9**, never Free Aspect. Test at ultrawide (2560×1080), 16:10 and 4:3 too.
- **Optional singletons:** some singletons only exist in some scenes (`TutorialManager`, `StageOverScreen`) or create themselves at startup (`AnalyticsLogger`). Call them as `X.Instance?.Method()`.
- **Pause guard:** gameplay `Update()`s return early when `GameManager.Instance.IsGamePaused` is true.
- **Grid position:** always read `CurrentCell`, never derive a cell from `transform.position` (it lags during the slide).
- **Extend by composition:** add new behavior as a separate component, rather than growing `PushableBlock` or `GridEntity`.
- **Furniture:** fixed pieces use `Furniture` (see the recipe under "How we work"); pushable pieces are a one-cell `PushableBlock`.
- **Objects on furniture:** make them a child of the piece they stand on, never a `GridEntity` themselves (a cell holds only one occupant), and give them `SortAbove`. On a pushable table their cell is the parent's `CurrentCell`; on fixed `Furniture` it's the cell under their pivot, so keep the pivot over the piece. Give them a `HeightOffset` (usually 1) so they count as being on top.
- **Stand-in keys:** a stand-in's identity comes from its replacement Key, so give that Key the same Key Id as the UI (`UiManager.keyId`) and the Door. Never put a `Key` component on the stand-in itself.
- **`TakeDamage` isn't a generic "hit":** `Key.TakeDamage` collects the key. For anything other than the player's attack (like the lamp's sweep), raise an event or use a dedicated hook instead.
- **Inspector wiring risk:** renaming or removing a `[SerializeField]` field, or changing a public method wired to a button `OnClick`, still compiles, but the Inspector loses the reference without any error. Inspector-wired methods include `PauseMenu.Pause/Resume/ResetLevel/NextLevel/ReturnToMainMenu/ShowControls/HideControls`, `StageOverScreen.RestartLevel/NextLevel`, `SceneNavigationButtons.LoadNextScene` and `MainMenu.Play/OpenTutorial/ShowControls/HideControls/QuitGame`. The `AudioManager` fields (`musicByBuildIndex`, `buttonClick`, `walk`, `walkOnCarpet`, `attack`, `stretch`, `push`, `checkpointCharge`, `death`, `lampHit`, `keyKnockedDown`, `roombaDockHit`, `doorOpen`, `switchHit`, `timedDoorOpen`, `timedDoorClose`, `fenceHit`, `keyPickup`, and the volume sliders) hold clips assigned on the prefab, so renaming one silently drops its clip. Flag this whenever such a change is proposed.
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
- **`Furniture`**: not a `GridEntity`. In `Start` it blocks every cell whose centre lies inside one of its own enabled colliders (not its children's), and frees them in `OnDisable`. It sets its `sortingSprite` order from the lowest footprint row, one step behind so the player wins a tie. `OnDrawGizmosSelected` shows the blocked cells as red squares. All of this lives in the public `RefreshFootprint()`, which `Start` calls; call it again after switching its colliders on or off, and it frees the old cells first. `BlockedCells` exposes the cells it currently blocks (empty while disabled).
- **`Fence`**: `IAttackable`, placed next to a `Furniture` component. It has two trigger BoxCollider2D footprints, closed and open; `Awake` turns the open one off before `Furniture.Start` reads the footprint. On a hit it plays `openFrames` (closed → middle → open) over `frameDuration` each, logs `INTERACT` "opened", then swaps the boxes and calls `Furniture.RefreshFootprint()`. `IsAlive` is false from the hit on, so it opens only once.
- **`TimedDoor`**: placed next to a `Furniture` component with one trigger BoxCollider2D footprint. `OpenBriefly(delaySeconds = 0)` (refused while `IsBusy`, which is true from the call, delay included) waits the delay, plays the open sound and `frames` closed → open, copies `Furniture.BlockedCells`, then turns off the footprint and disables `Furniture` to free the cells. After `openSeconds` it waits until no grid occupant stands in those cells, re-enables the footprint and `Furniture`, calls `RefreshFootprint()` (solid again before the closing frames), plays the close sound and the frames back to 0, and raises `Closed`.
- **`WallSwitch`**: `IAttackable` with a trigger Collider2D over its art; not a grid object (it sits on a wall cell). `IsAlive` is false while its `TimedDoor` is busy. A hit plays the switch sound, calls `ZoomToggle.Instance?.ZoomOut()`, then `door.OpenBriefly(zoomWaitSeconds)` if that zoom changed the view (else with no delay), logs `INTERACT` "pressed" and shows `onSprite`; the door's `Closed` event switches it back to `offSprite`.
- **`Door`**: a non-pushable `IInteractable` that opens when bumped, if `GameManager.HasKey(keyId)`. It can occupy extra cells through `extraCells`, which are claimed in `Start` and freed in `OnDisable`. `disableOnOpen` either deactivates the GameObject or just frees all of its cells. A keyed bump sets `opening`, plays the door-open sound and calls `Open()` only after `AudioManager.DoorOpenSeconds` (`OpenAfter` coroutine), so it blocks until then; the `INTERACT` log happens at the bump. `RestoreState` still opens instantly.

### Player (`Assets/Scripts/Player/`)

- **`CharacterController`** (`GridEntity`):
  - Movement: reads WASD through `ReadHeldDirection()` (one direction at a time), updates `FacingDirection`, then calls `TryStep`. Turn in place: `UpdateTurnInPlace` sets `FacingDirection` and starts `turnHoldTimer` when a new direction is pressed from still; `ResetTurnInput` clears it during stretch and stun.
  - Energy: a plain step increments `stepCounter`; at `stepsPerBar` (or `stepsPerBarHighFriction` on carpet) it charges 1 bar. A push charges `max(1, round(weight × energyCostPerWeight × frictionMultiplier))`, where the multiplier is `stepsPerBar / stepsPerBarHighFriction` on carpet. All its costs go through `ChargeBars`.
  - Stretch: `stretchStage` / `stretchTarget` / `stretchTimer`, driven by `BeginStretch()` and `TickStretch(held)`. Space is read with `GetKey`, so releasing it during pause is still noticed on resume. Exposes `IsStretching` (blocks walking and turning), `StretchStage` (0/1/2) for visuals, and `IsHoldingStretch` (`IsStretching && Input.GetKey(stretchKey)`, false during the release/contraction tail) for anything that needs to know the key is actively held right now, e.g. the tutorial's hold-timer. Ohad's `ToggleStretch`, `HandleStretchInput` (WASD reach), `stretchReach` and Animator `Direction` 40–43 are commented out.
  - Carpet slide: a step that **starts** on carpet uses `highFrictionMoveDuration` / `highFrictionMoveCurve`.
  - Exposes `IsWalking` and `StepProgress` (0..1) for visuals.
  - Death: subscribes to `SpringManager.BarsReachedZero`, then logs `DEATH` and calls `GameManager.RestartLevel()`.
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
    - `ReloadScene()`: plain reload that keeps the checkpoint and keys; no longer used by death.
    - `RestartLevel()`: full restart without logging; clears the checkpoint, keys and room snapshot, then reloads. Used by death.
    - `ResetLevel()`: logs `STAGE_RESET`, then `RestartLevel()`. Used by the pause menu and stage-over screen.
    - `LoadNextScene()`: next scene in the build order; after the last one, the main menu (index 0).
    - `LoadMainMenu()`: clears the checkpoint, keys and room snapshot, logs `LEVEL_CHANGE`, loads index 0. Used by `PauseMenu.ReturnToMainMenu`.
  - Every scene load unpauses first.
- **Commented out, kept to revert (checkpoint respawn):** death used to respawn at the last checkpoint with the room restored. That code is commented out with a "Disabled: death now fully resets the level" note, so it can be switched back on. It covers `GameManager.ActivateCheckpoint` (saving `RespawnCell` / `HasCheckpoint`), the respawn teleport in `CharacterController.Start`, `RoomSnapshot.Capture()` in `Checkpoint` and `Door`, and the door's "Saves Checkpoint When Opened" checkpoint (it still logs `DOOR_CHECKPOINT`). `RoomSnapshot` (on the GameManager object; saves the room at a checkpoint and restores it after a death) is untouched but inactive, since nothing captures a snapshot.
- **`AudioManager`**: persists across scene loads (`DontDestroyOnLoad`); a second copy (from the next scene's prefab) destroys itself in `Awake`.
  - Creates 4 AudioSources in `Awake` (2D, no Play On Awake): `musicSource` (loop), `sfxSource` (`PlayOneShot`, so effects overlap and survive scene loads), `loopSfxSource` (walking) and `carpetLoopSource` (the carpet layer).
  - Sounds are `SoundClip` slots (clip + `[Range]` volume). Final volume = `musicVolume` or `sfxVolume` × the clip's volume, re-applied every frame so Inspector changes during Play are heard at once.
  - Music: `musicByBuildIndex` (0 MainMenu, 1 Tutorial, 2 Attic, 3 Bedroom). Played on `SceneManager.sceneLoaded` (and in `Start` for the first scene); the same clip already playing carries on, an empty slot stops the music.
  - Walking: `SetWalking(walking, onCarpet)`, called every frame from `CharacterController.UpdateAnimation` and with false from its `OnDisable`. `Update` drives both loops: paused while `IsGamePaused`; the carpet layer only with a `walkOnCarpet` clip, and then the walk loop is scaled by `walkVolumeOnCarpet`. A scene load stops them.
  - One-shots: `PlayButtonClick`, `PlayAttack` (start of `AttackRoutine`), `PlayStretch` (when a stretch starts), `PlayPush` (`CharacterController`'s successful-push branch, before the energy charge so a fatal push still sounds before the death sound), `PlayCheckpointCharge` (`Checkpoint`, after the empty-station check), `PlayDeath` (`CharacterController.HandleBarsReachedZero`, before `RestartLevel`). Also `PlayMusic(SoundClip)` / `StopMusic()`.
  - Level-object one-shots (Inspector header "Level objects"): `PlayLampHit` (`DeskLamp.TakeDamage`), `PlayKeyKnockedDown` (`PushOffObject.SweptBy`'s flight callback, not `SwapForReplacement`), `PlayKeyPickup` (`Key.Collect`, after the double-collect guard), `PlayRoombaDockHit` (`RoombaButton.TakeDamage`), `PlayDoorOpen` (`Door.Interact`), `PlaySwitchHit` (`WallSwitch.TakeDamage`), `PlayTimedDoorOpen` / `PlayTimedDoorClose` (`TimedDoor.Cycle`), `PlayFenceHit` (`Fence.TakeDamage`).
  - `DoorOpenSeconds`: the door-open clip's length (0 with no clip), read by `Door` to time its disappearance.
- **`SpringManager`**: scene singleton for the bars.
  - `ReduceBars(n)` and `ResetBars()`.
  - Events: `BarsChanged(int)`, `BarsReachedZero`.
  - `deathAtZero` (Inspector **Death At Zero**, on by default): when off, the bars still clamp to 0 but `BarsReachedZero` is never raised. It's off on the Tutorial scene's SpringManager.
- **`TutorialManager`**: exists only in the Tutorial scene. Its 6 task flags are **static**, so they survive the reload on reset, and `Start()` re-applies them to the check images. A static `SceneManager.sceneLoaded` hook (registered by `RuntimeInitializeOnLoadMethod`) clears them whenever the loaded scene's name differs from the previous one, and at the start of each run. The Start button becomes interactable when all six are done. Its own `Update()` polls the player's `CharacterController.IsHoldingStretch` to time a continuous 1.5s hold for the stretch task (resets to 0 the instant the hold breaks); the other five tasks are notified directly by the scripts that perform them.
- **`AnalyticsLogger`**: creates itself at startup (`RuntimeInitializeOnLoadMethod`, `DontDestroyOnLoad`), so it is never placed in a scene.
  - Appends one line per event to `Analog Override/analytics_log.txt`, in a folder next to the game (the project folder in the Editor). A "run" is one game launch or one Play in the Editor.
  - Events: `SCENE_LOADED` (first load of each scene per run), `LEVEL_CHANGE`, `TUTORIAL_COMPLETE`, `STAGE_COMPLETE`, `STAGE_RESET`, `DEATH`, `CHECKPOINT_VISIT`, `DOOR_CHECKPOINT`, `KEY_PICKUP`, `INTERACT` (door opened, shelf object pushed off or knocked away, lamp spun, fence opened, switch pressed), `ROOMBA_DOCK_TRIGGERED`, `QUIT`.
  - Writes a summary at the end of each run: session length, play time per scene (pauses excluded), and a count of each event type, with a breakdown of interactions per object.

### UI (`Assets/Scripts/UI/`)

- **`PauseMenu`**: Esc toggles pause through `GameManager`. Its button methods are wired in the Inspector. It notifies the tutorial (`NotifyPauseMenuUsed`) from `Resume`, only if the menu was actually open, and from `ResetLevel` before it calls `GameManager.ResetLevel()`. The task's check image field is still named `resetCheck`. The optional `controlsPanel` (controls image + X) is toggled by `ShowControls` / `HideControls`, and hidden by `Awake`, `Pause` and `Resume`; while it's open, Esc only closes it. Assigned in both `Main UI Prefab` and the Tutorial.
- **`MainMenu`**: the Main Menu scene's button hooks. `playSceneName` / `tutorialSceneName` are set in the Inspector; loading one clears keys, checkpoint and snapshot (GameManager survives scene loads). `ShowControls` / `HideControls` toggle `controlsPanel`. `QuitGame` logs `QUIT` and exits.
- **`PauseMenu.ReturnToMainMenu`**: the in-game "Main Menu" button (pause menu and stage-over screen in `Main UI Prefab`, and the Tutorial's pause menu). It doesn't log `QUIT`, because that writes the run summary. `PauseMenu.QuitGame` is kept but no longer wired in-game.
- **`StageOverScreen`**: **not finished yet** (see Mechanics, Stage goal). Scene singleton. `Show()` logs the stage completion, shows the panel and pauses. `RestartLevel` / `NextLevel` are wired to buttons. In `Main UI Prefab`, though, the stage-over **next level button** is wired to `PauseMenu.NextLevel` (`SkipToNextScene`), so finishing a level that way doesn't log `LEVEL_CHANGE` and clears keys between levels.
- **`SceneNavigationButtons`** (`AnalogOverride.UI`): a button hook for `LoadNextScene`.
- **`ButtonClickSound`**: `[RequireComponent(typeof(Button))]`; in `Awake` it adds `AudioManager.Instance?.PlayButtonClick()` to the button's `onClick` in code. It isn't wired in the OnClick list, because a scene's own AudioManager copy destroys itself when another copy already survived, which would leave such a link dead.
- **`ZoomToggle`**: the HUD zoom button (in `Main UI Prefab`). It hooks its own button in code and enables/disables the overview `CinemachineCamera`, swapping the icon; `Awake` starts zoomed in. A static `Instance` (scene-local, cleared in `OnDestroy`) and a public `ZoomOut()` (returns true if it changed the view; doesn't log `ZOOM_OUT`) let gameplay objects zoom out. Call it as `ZoomToggle.Instance?.ZoomOut()`, since a scene may have no zoom button.
- **`UiManager`**: spawns `MaxBars` spring segments and blends the layout spacing between compressed (full energy) and released (empty) on `BarsChanged`. It also shows the key icon through `Key.KeyCollected`, or on load if the key is already collected.

### Misc (`Assets/Scripts/`, no namespace)

- **`Checkpoint`**: trigger with `usesLeft` charges (reset to `maxUses` in `Awake`, so on every scene load) and full/half/empty sprites. An empty station returns at once. Otherwise, on player contact it plays the charge sound, logs the visit, notifies the tutorial, resets the step counter and the bars, and activates itself (firing the `activate` animation and deactivating the other checkpoints) only if it isn't already active. It no longer saves a respawn point.
- **`Key`**: trigger, and also an `IInteractable`, so it can be collected by a reach (guarded against collecting twice). On pickup it calls `GameManager.CollectKey(keyId)` and raises the static `KeyCollected` event before destroying itself. In `Start` it destroys itself without raising the event if the key was already collected. Also `IAttackable` (an attack collects it). `KeyId` exposes its id.
- **`SortAbove`**: copies a target sprite's order in layer plus `offset` every `LateUpdate`, for things standing on furniture.
- **`StageGoal`**: trigger. The first time the player touches it, it calls `StageOverScreen.Instance.Show()`.
- **`ResponsiveCamera`**: on Main Camera in both scenes. Sets the orthographic size to `referenceOrthographicSize × Max(1, (16/9) / screenAspect)`, so screens narrower than 16:9 zoom out and wider ones are unchanged; re-applies when the screen size changes. With a `CinemachineCamera` assigned (Level2-Bedroom) it changes that lens, since the Brain overrides the plain Camera; with the field empty (Tutorial) it changes its own `Camera`. Reference size is 4.54679; the *Capture Current Size* context menu saves the current zoom (do it with the Game view at 16:9).

## Singleton / init order

`[DefaultExecutionOrder]` enforces: `GridManager` (-100) → `GameManager` (-60) → `AudioManager` (-55) → `SpringManager` (-50) → `TutorialManager` (-40) → default scripts. `AnalyticsLogger` creates itself before the first scene loads. A new manager that other `Start()`-time code reads through `Instance` needs an explicit execution order earlier than the code that uses it; don't rely on the order of objects in the scene.
