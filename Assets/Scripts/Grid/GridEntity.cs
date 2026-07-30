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
    /// TryStep() from your own logic the way PlayerController drives it from input).
    /// </summary>
    [DisallowMultipleComponent]
    public class GridEntity : MonoBehaviour, IGridOccupant
    {
        [Tooltip("Seconds it takes to visually slide one cell. Purely cosmetic — grid logic (occupancy, blocking) updates instantly regardless of this value. 0 = snap instantly.")]
        [Min(0f)]
        [SerializeField] private float moveDuration = 0.12f;

        [Tooltip("Whether other GridEntities can push this one out of their way instead of being blocked by it.")]
        [SerializeField] protected bool pushable;

        /// <summary>
        /// The cell this entity is logically standing in. This updates the instant a
        /// move is accepted (inside TryStep), NOT when the visual slide finishes — grid
        /// logic and gameplay code should always read this rather than deriving a cell
        /// from transform.position, since the latter is mid-tween "lie" for moveDuration
        /// seconds after every step.
        /// </summary>
        public Vector2Int CurrentCell { get; private set; }

        public bool IsPushable => pushable;

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
        }

        /// <summary>
        /// Attempts to move one cell in the given direction (expects a unit vector like
        /// Vector2Int.up — diagonals aren't meaningful on this grid). Resolution order:
        ///   1. Off-grid or a wall (per GridManager.IsWalkable) -> refused.
        ///   2. Cell occupied by a pushable GridEntity -> recursively try to push it first;
        ///      only proceed if that push succeeds. This is what makes block-chains work:
        ///      pushing A into B into C only succeeds if C also has somewhere to go.
        ///   3. Cell occupied by a non-pushable IInteractable -> Interact() fires, but this
        ///      entity does NOT move (a "bump" — see IInteractable for the contract).
        ///   4. Cell occupied by anything else non-pushable -> refused, nothing happens.
        ///   5. Otherwise -> accepted: grid state (CurrentCell, GridManager occupancy) updates
        ///      immediately and synchronously; only the visual slide is animated over time.
        /// </summary>
        public bool TryStep(Vector2Int direction)
        {
            if (Manager == null || IsMoving || direction == Vector2Int.zero) return false;

            var targetCell = CurrentCell + direction;
            if (!Manager.IsWalkable(targetCell)) return false;

            var occupant = Manager.GetOccupant(targetCell);
            if (occupant != null)
            {
                if (occupant is GridEntity other && other.IsPushable)
                {
                    if (!other.TryStep(direction)) return false;
                    // `other` has now vacated targetCell (its own TryStep already updated
                    // GridManager occupancy synchronously), so we fall through and claim it.
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
                yield return null;
            }

            transform.position = toWorld;
            IsMoving = false;
        }
    }
}
