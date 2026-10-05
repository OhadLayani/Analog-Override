using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnalogOverride.GridSystem
{
    /// <summary>
    /// Base class for anything that lives on the grid: the player, pushable blocks,
    /// enemies, NPCs, etc. Owns its own cell registration with GridManager and knows
    /// how to attempt a single-cell step — including pushing a pushable occupant out
    /// of the way (single-object only, never cascades into a chain — see TryBePushed)
    /// and bump-interacting with an occupant that implements IInteractable. Entities can't
    /// step between height levels (GridManager.GetHeight) but can stretch to act on a cell
    /// one level up without moving — see TryReach.
    ///
    /// To add a new kind of grid object: extend this class. You get registration,
    /// movement, pushing and bump-interaction for free — you only need to add your
    /// own behaviour on top (see PushableBlock for the minimal example, or drive
    /// TryStep() from your own logic the way CharacterController drives it from input).
    ///
    /// Optionally also owns draw order between grid entities (see sortingSprite) by
    /// writing its Order in Layer directly from this entity's world Y every time it
    /// moves, rather than trusting the render pipeline's own distance-based sort.
    /// </summary>
    [DisallowMultipleComponent]
    public class GridEntity : MonoBehaviour, IGridOccupant
    {
        [Tooltip("Seconds it takes to visually slide one cell. Purely cosmetic — grid logic (occupancy, blocking) updates instantly regardless of this value. 0 = snap instantly.")]
        [Min(0f)]
        [SerializeField] private float moveDuration = 0.12f;

        [Tooltip("Shapes the visual slide's motion over the step (X = normalized time 0→1, Y = normalized progress 0→1). Straight linear (a 45° line) is what produces a constant-speed 'sliding on ice' feel — the default here eases in/out instead, for a snappier step-like motion. Tune this directly if movement still feels too slow/floaty (flatten the start) or too sharp/mechanical (round it off more).")]
        [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Whether other GridEntities can push this one out of their way instead of being blocked by it.")]
        [SerializeField] protected bool pushable;

        [Tooltip("How 'heavy' this entity is if something pushes it. GridEntity itself does nothing with this number — it's only ever reported back to the pusher via TryStep's pushedWeight, so gameplay code (e.g. an energy/stamina system) can decide what a unit of weight costs. Irrelevant unless Pushable is true.")]
        [Min(0f)]
        [SerializeField] private float weight = 1f;

        [Header("Draw Order")]
        [Tooltip("The sprite whose Order in Layer this entity should keep in sync with its own world Y position, so entities on the grid always draw correctly in front of/behind each other and don't depend on the render pipeline's own distance-based sort (which is easy to accidentally break with a stray Order in Layer override elsewhere, or thrown off by a visual rig with an offset child sprite). Leave empty to opt out and let the renderer's own sorting handle this entity instead.")]
        [SerializeField] private SpriteRenderer sortingSprite;

        [Tooltip("Multiplier from world Y to Order-in-Layer units. Needs to be large enough that two entities half a cell apart in Y still land on different integer sorting orders — 100 comfortably covers sub-cell movement during the tween without overflowing Order in Layer's int range for any reasonably sized level.")]
        [SerializeField] private int sortingOrderPrecision = 100;

        [Tooltip("Constant added on top of the computed value, so this entity's Order in Layer never sinks below whatever static Order in Layer terrain/background sprites on the SAME Sorting Layer use — those are typically small numbers (0-10ish). Must stay bigger than sortingOrderPrecision * (the largest world Y this entity could ever reach), or a far-enough-back position could still underflow past terrain. 10000 gives huge headroom for any grid this size.")]
        [SerializeField] private int sortingOrderBase = 10000;

        /// <summary>
        /// The cell this entity is logically standing in. This updates the instant a
        /// move is accepted (inside TryStep), NOT when the visual slide finishes — grid
        /// logic and gameplay code should always read this rather than deriving a cell
        /// from transform.position, since the latter is mid-tween "lie" for moveDuration
        /// seconds after every step.
        /// </summary>
        public Vector2Int CurrentCell { get; private set; }

        /// <summary>
        /// Seconds the *next* accepted step's visual slide will take (see MoveRoutine). Exposed to
        /// subclasses that want to vary the cosmetic slide speed situationally — e.g. CharacterController
        /// slowing the slide while stepping onto high-friction terrain. Grid logic itself never reads
        /// this; only MoveRoutine does, and TryStep hands MoveRoutine whatever this is set to AT THE
        /// MOMENT TryStep is called — so a subclass MUST set this BEFORE calling TryStep for the change
        /// to apply to that step. Setting it after TryStep returns only affects the step after that.
        /// </summary>
        protected float MoveDuration
        {
            get => moveDuration;
            set => moveDuration = value;
        }

        /// <summary>Same override pattern as MoveDuration above, for the OTHER half of a step's feel — its motion shape rather than its length. Set this BEFORE calling TryStep for it to apply to that step.</summary>
        protected AnimationCurve MoveCurve
        {
            get => moveCurve;
            set => moveCurve = value;
        }

        public bool IsPushable => pushable;

        /// <summary>How much this entity should "count for" when something pushes it. See the tooltip above — this class assigns it no meaning of its own.</summary>
        public float Weight => weight;

        /// <summary>True while the visual slide from the last accepted step is still playing. TryStep refuses new moves while this is true — one step must finish visually before the next is accepted.</summary>
        public bool IsMoving { get; private set; }

        protected static GridManager Manager => GridManager.Instance;

        protected virtual void Start()
        {
            if (Manager == null)
            {
                Debug.LogError($"{nameof(GridEntity)} on '{name}' found no GridManager in the scene. Add a GameObject with a GridManager component before any GridEntity runs.", this);
                enabled = false;
                return;
            }

            SnapToCurrentPosition();
        }

        protected virtual void OnDisable()
        {
            // Safe even if Manager was never assigned (see Start's guard) or already destroyed on scene teardown.
            Manager?.RemoveOccupant(CurrentCell, this);
        }

        /// <summary>Registers this entity at whatever cell its current world position maps to, and snaps its Transform to that cell's exact center. Useful on spawn, or after teleporting an entity by setting transform.position directly.</summary>
        public void SnapToCurrentPosition()
        {
            CurrentCell = Manager.WorldToCell(transform.position);
            Manager.TryPlaceOccupant(this, CurrentCell);
            transform.position = Manager.CellToWorld(CurrentCell);
            UpdateSortingOrder(transform.position.y);
        }

        /// <summary>
        /// Keeps sortingSprite's Order in Layer tied to a world Y so entities always draw
        /// correctly relative to each other, independent of the render pipeline's own
        /// distance sort. Lower Y (visually lower on screen, closer to the viewer in a
        /// top-down game) must draw IN FRONT, i.e. get a HIGHER Order in Layer — hence the
        /// negation. No-ops if sortingSprite isn't assigned (opted out for this entity).
        /// </summary>
        private void UpdateSortingOrder(float worldY)
        {
            if (sortingSprite == null) return;
            sortingSprite.sortingOrder = sortingOrderBase - Mathf.RoundToInt(worldY * sortingOrderPrecision);
        }

        /// <summary>Convenience overload for callers that don't care what (if anything) got pushed — see the other overload for the actual resolution rules.</summary>
        public bool TryStep(Vector2Int direction) => TryStep(direction, out _);

        /// <summary>
        /// Attempts to move one cell in the given direction (expects a unit vector like
        /// Vector2Int.up — diagonals aren't meaningful on this grid). Resolution order:
        ///   1. Off-grid or a wall (per GridManager.IsWalkable) -> refused.
        ///   2. Cell on a different height level than this entity's current cell -> refused,
        ///      occupied or not. Levels are separate layers: you can't step or push across
        ///      them. The only way to touch another level is to reach for it (see TryReach).
        ///   3. Cell occupied by something:
        ///      a. A pushable GridEntity -> try to push it (see TryBePushed).
        ///         Pushing is single-object only and never cascades: if THAT object's own
        ///         destination cell is occupied by anything at all — pushable or not — the
        ///         push (and this whole step) is refused, full stop. Pushing A that has B
        ///         sitting right behind it does nothing; it does NOT shove both.
        ///      b. A non-pushable IInteractable -> Interact() fires, but this entity does
        ///         NOT move (a "bump" — see IInteractable for the contract).
        ///      c. Anything else non-pushable -> refused, nothing happens.
        ///   4. Otherwise -> accepted: grid state (CurrentCell, GridManager occupancy) updates
        ///      immediately and synchronously; only the visual slide is animated over time.
        ///
        /// `pushedWeight` is the pushed occupant's Weight — 0 if the step didn't push
        /// anything, or if the step was refused. Callers that want to charge a cost for
        /// pushing (e.g. an energy/stamina system) can use it directly.
        /// </summary>
        public bool TryStep(Vector2Int direction, out float pushedWeight)
        {
            pushedWeight = 0f;

            if (Manager == null || IsMoving || direction == Vector2Int.zero) return false;

            var targetCell = CurrentCell + direction;
            if (!Manager.IsWalkable(targetCell)) return false;

            if (Manager.GetHeight(targetCell) != Manager.GetHeight(CurrentCell)) return false;

            var occupant = Manager.GetOccupant(targetCell);
            if (occupant != null)
            {
                if (occupant is GridEntity other && other.IsPushable)
                {
                    if (!other.TryBePushed(direction)) return false;
                    pushedWeight = other.Weight;
                }
                else
                {
                    if (occupant is IInteractable interactable)
                    {
                        interactable.Interact(this);
                    }
                    return false;
                }
            }

            return CommitStep(targetCell);
        }

        /// <summary>
        /// Stretches toward the adjacent cell in `direction` WITHOUT moving, acting on whatever is
        /// there — but only if that cell is on a higher level than this entity's own, 1 to
        /// maxLevelsUp levels up (GridManager.GetHeight). It's the one way to touch another level,
        /// since TryStep refuses to step or push across levels. Cells on this entity's own level
        /// are deliberately ignored: acting on those can mean moving (a push drags the pusher into
        /// the vacated cell), and a stretch is exactly the state where you don't move.
        /// What happens depends on what's at the cell:
        ///   1. A pushable GridEntity -> slid one cell further along `direction`, under the usual
        ///      single-object rules (see TryBePushed) and staying on its own level.
        ///      `pushedWeight` is its Weight, same as TryStep reports.
        ///   2. Any other GridEntity that's an IInteractable (a Door, say) -> Interact().
        ///   3. Nothing registered on the grid there -> the first IInteractable whose collider
        ///      overlaps the cell. Things like a Key aren't grid occupants — that's what keeps them
        ///      non-blocking at ground level — so they're found by a physics overlap instead.
        /// Returns true if something reacted.
        /// </summary>
        public bool TryReach(Vector2Int direction, int maxLevelsUp, out float pushedWeight)
        {
            pushedWeight = 0f;

            if (Manager == null || IsMoving || direction == Vector2Int.zero) return false;

            var targetCell = CurrentCell + direction;
            if (!Manager.InBounds(targetCell)) return false;

            var levelsUp = Manager.GetHeight(targetCell) - Manager.GetHeight(CurrentCell);
            if (levelsUp < 1 || levelsUp > maxLevelsUp) return false;

            if (Manager.GetOccupant(targetCell) is GridEntity other)
            {
                if (other.IsPushable)
                {
                    if (!other.TryBePushed(direction)) return false;
                    pushedWeight = other.Weight;
                    return true;
                }

                if (other is IInteractable interactable)
                {
                    interactable.Interact(this);
                    return true;
                }

                return false;
            }

            return InteractWithOverlapping(targetCell);
        }

        // Reused across calls: reaching is rare, but there's no reason to allocate a fresh list each time.
        private static readonly List<Collider2D> OverlapBuffer = new List<Collider2D>();

        /// <summary>Interacts with the first IInteractable whose collider overlaps most of `cell` — the fallback for non-grid objects like Key (see TryReach).</summary>
        private bool InteractWithOverlapping(Vector2Int cell)
        {
            // Keys and the like are trigger colliders; asking for triggers explicitly means this
            // doesn't silently break if someone flips the project-wide "Queries Hit Triggers" setting.
            var filter = new ContactFilter2D { useTriggers = true };

            // 80% of the cell, so a collider sitting in a neighbouring cell doesn't count as being in this one.
            var count = Physics2D.OverlapBox(Manager.CellToWorld(cell), Manager.CellSize * 0.8f, 0f, filter, OverlapBuffer);
            for (var i = 0; i < count; i++)
            {
                var hit = OverlapBuffer[i];
                if (hit.transform.IsChildOf(transform)) continue; // never reach for ourselves (own collider, attack hitbox, ...)

                if (hit.TryGetComponent<IInteractable>(out var interactable))
                {
                    interactable.Interact(this);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Called on THIS entity by whoever is trying to push it — never call this to move
        /// yourself; use TryStep for that. Deliberately does not reuse TryStep's occupant
        /// handling: unlike a normal step, a push only succeeds into a cell that's entirely
        /// empty (Manager.IsFree), so pushing never cascades into pushing something else.
        /// That's what keeps pushing single-object-only rather than a domino chain.
        /// </summary>
        private bool TryBePushed(Vector2Int direction)
        {
            if (Manager == null || IsMoving) return false;

            var targetCell = CurrentCell + direction;
            if (!Manager.IsFree(targetCell)) return false;

            // A pushed object stays on its level. Sending something over an edge is a deliberate,
            // per-object thing (see PushOffObject), not something a plain slide can do by accident.
            if (Manager.GetHeight(targetCell) != Manager.GetHeight(CurrentCell)) return false;

            return CommitStep(targetCell);
        }

        /// <summary>Shared tail of TryStep/TryBePushed once a destination cell has been fully validated: claims it in GridManager and starts the visual slide.</summary>
        private bool CommitStep(Vector2Int targetCell)
        {
            var fromCell = CurrentCell;
            if (!Manager.TryMoveOccupant(this, fromCell, targetCell)) return false;

            CurrentCell = targetCell;
            StartCoroutine(MoveRoutine(Manager.CellToWorld(fromCell), Manager.CellToWorld(targetCell)));
            return true;
        }

        /// <summary>Purely cosmetic: slides the Transform between two world positions. Grid state (CurrentCell/occupancy) is already final by the time this runs — do not put gameplay logic in here, it won't run at a predictable time relative to other entities' moves.</summary>
        private IEnumerator MoveRoutine(Vector3 fromWorld, Vector3 toWorld)
        {
            IsMoving = true;

            var t = 0f;
            // Snapshot both at slide-start: this step's slide must not be retroactively changed by a
            // later Update() frame reassigning MoveDuration/MoveCurve in anticipation of a future step
            // while this one is still animating.
            var duration = moveDuration;
            var curve = moveCurve;
            while (t < duration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(fromWorld, toWorld, curve.Evaluate(Mathf.Clamp01(t / duration)));
                // Updated every frame, not just at the end, so draw order stays correct
                // WHILE sliding past another entity mid-tween, not only once at rest.
                UpdateSortingOrder(transform.position.y);
                yield return null;
            }

            transform.position = toWorld;
            UpdateSortingOrder(transform.position.y);
            IsMoving = false;
        }
    }
}
