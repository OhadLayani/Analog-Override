using AnalogOverride.GridSystem;
using UnityEngine;

namespace AnalogOverride.Entities
{
    /// <summary>
    /// A "push-off" object: something sitting on a raised level that, when a stretching player
    /// pushes it, doesn't slide along the shelf — it vanishes and a different version of itself
    /// appears on the level below, as if it had been knocked off. The classic use is a key on a
    /// shelf: shove it down, then walk over and pick up the fallen Key later.
    ///
    /// It always falls. Where it lands is never a reason to refuse the push, so something already
    /// standing there (a crate, say) doesn't stop it — the variant just appears on top of it. Where:
    ///   1. On the Landing Spot, if one is assigned — the tile you planned for it.
    ///   2. Otherwise, the nearest EMPTY cell on a lower level, searching outward in the direction
    ///      it was pushed (away from the player). If every lower cell on that line is occupied, the
    ///      nearest occupied one. If there's no lower cell at all, it appears where it was.
    ///
    /// Reacts through IInteractable, so it's triggered the same way as everything else a stretch
    /// can reach (GridEntity.TryReach). It deliberately isn't a GridEntity: it never moves and
    /// nothing can push it along, so there's nothing for the grid to track — it only needs a
    /// collider so a reach can find it.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PushOffObject : MonoBehaviour, IInteractable
    {
        [Tooltip("What appears where this lands — e.g. the normal walk-over Key prefab for a key knocked off a shelf. Spawned fresh at runtime, so a level reset brings back the original shelf object and drops this like anything else not placed in the scene.")]
        [SerializeField] private GameObject fallenVariant;

        [Tooltip("Optional. An empty GameObject placed on the tile you want the fallen variant to land on — the variant appears at the center of whichever grid cell this sits in, even if something is already standing there. Leave empty to let it find the nearest empty cell below, in the direction it was pushed.")]
        [SerializeField] private Transform landingSpot;

        private bool fallen;

        public void Interact(GridEntity source)
        {
            // Destroy() only lands at the end of the frame, so guard against being triggered
            // twice within one (e.g. a second collider on this object).
            if (fallen) return;

            var manager = GridManager.Instance;
            if (manager == null) return;

            if (fallenVariant == null)
            {
                Debug.LogWarning($"{name} has no Fallen Variant assigned, so there's nothing to drop when it's pushed off.", this);
                return;
            }

            var origin = manager.WorldToCell(transform.position);
            var landingCell = landingSpot != null
                ? manager.WorldToCell(landingSpot.position)
                : FindLandingCell(manager, origin, PushDirection(origin, source));

            fallen = true;
            Instantiate(fallenVariant, manager.CellToWorld(landingCell), Quaternion.identity);
            Destroy(gameObject);
        }

        /// <summary>
        /// The way the push went: from the pusher's cell to this object's. A reach always targets
        /// the neighbouring cell, so this is normally a unit vector already — the axis pick only
        /// guards against an object whose pivot sits a little outside its own cell.
        /// </summary>
        private static Vector2Int PushDirection(Vector2Int origin, GridEntity source)
        {
            return source == null
                ? Vector2Int.down
                : GridEntity.CardinalOf(origin - source.CurrentCell, Vector2Int.down);
        }

        /// <summary>
        /// Walks outward from `origin` in `direction` to the grid's edge and picks, in order of
        /// preference: the nearest empty cell below this object's level; failing that, the nearest
        /// occupied cell below it (it still falls — it just lands on whatever is there); failing
        /// that, `origin` itself. "Below" means a lower height level; if this object is already on
        /// level 0 there is no lower level, so any walkable cell counts. Walls are never landing spots.
        /// </summary>
        private static Vector2Int FindLandingCell(GridManager manager, Vector2Int origin, Vector2Int direction)
        {
            var level = manager.GetHeight(origin);
            Vector2Int? nearestOccupied = null;

            for (var cell = origin + direction; manager.InBounds(cell); cell += direction)
            {
                if (!manager.IsWalkable(cell)) continue;

                // Still on the shelf (or higher): that's sliding along it, not falling off it.
                if (level > 0 && manager.GetHeight(cell) >= level) continue;

                if (manager.GetOccupant(cell) == null) return cell;

                nearestOccupied ??= cell;
            }

            return nearestOccupied ?? origin;
        }
    }
}
