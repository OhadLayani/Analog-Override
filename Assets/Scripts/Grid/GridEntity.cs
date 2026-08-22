using System.Collections;
using UnityEngine;

namespace AnalogOverride.GridSystem
{
    /// <summary>
    /// Base class for anything that lives on the grid: the player, pushable blocks,
    /// enemies, NPCs, etc. Owns its own cell registration with GridManager and knows
    /// how to attempt a single-cell step — including pushing a pushable occupant out
    /// of the way (recursively, so block-chains work) and bump-interacting with an
    /// occupant that implements IInteractable.
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

        [Tooltip("Whether other GridEntities can push this one out of their way instead of being blocked by it.")]
        [SerializeField] protected bool pushable;

        [Tooltip("How 'heavy' this entity is if something pushes it. GridEntity itself does nothing with this number — it's only ever reported back to the pusher via TryStep's pushedWeight, so gameplay code (e.g. an energy/stamina system) can decide what a unit of weight costs. Irrelevant unless Pushable is true.")]
        [Min(0f)]
        [SerializeField] private float weight = 1f;

        [Tooltip("Max height difference (in levels, see GridManager's height layers) this entity can climb in a single step. Only consulted when stepping into an EMPTY cell at a different height — pushing/interacting with an occupant always requires being at the same height regardless of this value.")]
        [Min(0)]
        [SerializeField] protected int climbHeight = 1;

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

        public bool IsPushable => pushable;

        /// <summary>How much this entity should "count for" when something pushes it. See the tooltip above — this class assigns it no meaning of its own.</summary>
        public float Weight => weight;

        /// <summary>Max height difference this entity can climb in one step. See the tooltip above.</summary>
        public int ClimbHeight => climbHeight;

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
        ///   2. Cell occupied by something:
        ///      a. At a different height than this entity's current cell -> refused.
        ///         Pushing and bump-interacting both require being at the SAME height as
        ///         the occupant — climbing (see below) never applies to occupied cells.
        ///      b. Occupied by a pushable GridEntity -> recursively try to push it first;
        ///         only proceed if that push succeeds. This is what makes block-chains
        ///         work: pushing A into B into C only succeeds if C also has somewhere to go.
        ///      c. Occupied by a non-pushable IInteractable -> Interact() fires, but this
        ///         entity does NOT move (a "bump" — see IInteractable for the contract).
        ///      d. Occupied by anything else non-pushable -> refused, nothing happens.
        ///   3. Cell is empty but at a different height -> only allowed within this entity's
        ///      ClimbHeight AND onto a cell GridManager.IsClimbable says has something to
        ///      climb (a ladder, cliff edge, ...). Otherwise refused.
        ///   4. Otherwise -> accepted: grid state (CurrentCell, GridManager occupancy) updates
        ///      immediately and synchronously; only the visual slide is animated over time.
        ///
        /// `pushedWeight` is the combined Weight of every occupant this step displaced —
        /// 0 if the step didn't push anything, or if the step was refused. For a chain
        /// (pushing A which pushes B), it's the sum of every link's Weight, so a caller
        /// that wants to charge a cost for pushing doesn't need to know chains exist.
        /// </summary>
        public bool TryStep(Vector2Int direction, out float pushedWeight)
        {
            pushedWeight = 0f;

            if (Manager == null || IsMoving || direction == Vector2Int.zero) return false;

            var targetCell = CurrentCell + direction;
            if (!Manager.IsWalkable(targetCell)) return false;

            var heightDiff = Manager.GetHeight(targetCell) - Manager.GetHeight(CurrentCell);
            var occupant = Manager.GetOccupant(targetCell);

            if (occupant != null)
            {
                // Same-height requirement for pushing/interacting — climbing only ever
                // applies when the destination cell is empty (see the else-branch below).
                if (heightDiff != 0) return false;

                if (occupant is GridEntity other && other.IsPushable)
                {
                    if (!other.TryStep(direction, out var restOfChainWeight)) return false;
                    // `other` has now vacated targetCell (its own TryStep already updated
                    // GridManager occupancy synchronously), so we fall through and claim it.
                    pushedWeight = other.Weight + restOfChainWeight;
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
            else if (heightDiff != 0)
            {
                if (Mathf.Abs(heightDiff) > climbHeight) return false;
                if (!Manager.IsClimbable(targetCell)) return false;
            }

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
            while (t < moveDuration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(fromWorld, toWorld, Mathf.Clamp01(t / moveDuration));
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
