using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AnalogOverride.GridSystem
{
    /// <summary>
    /// Single source of truth for the grid. Every other system (movement, pushing,
    /// interaction, placement, AI pathing, etc.) should query/mutate grid state
    /// through this class rather than tracking its own copy of it.
    ///
    /// Responsibilities:
    /// - World-space &lt;-&gt; cell-space conversion (delegates to Unity's own Grid component).
    /// - Static walkability, read from an optional collision Tilemap (walls, pits, ...).
    /// - Dynamic occupancy: which IGridOccupant, if any, currently sits in each cell.
    /// - Height: which elevation "level" a cell is on (read from a stack of Tilemaps) and
    ///   whether a cell can be climbed into/out of across a height difference (read from an
    ///   optional Climbable Tilemap). GridManager only exposes this data — the actual climb
    ///   rules (how big a difference is climbable) live on GridEntity, since that's a
    ///   per-entity capability, not a world property.
    ///
    /// This is a scene-scoped singleton — exactly one should exist per scene. If your
    /// game ever needs more than one active grid (e.g. two arenas loaded additively),
    /// promote Instance to a lookup keyed by grid identity instead of extending this class.
    /// </summary>
    [DefaultExecutionOrder(-100)] // Must run before any GridEntity.Start(), which reads Instance to self-register. Awake already runs before Start scene-wide, but this also guarantees GridManager wins if something else needs Instance from its own Awake().
    [RequireComponent(typeof(UnityEngine.Grid))]
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        [Header("Bounds")]
        [Tooltip("Width/height of the grid in cells.")]
        [SerializeField] private Vector2Int size = new Vector2Int(20, 20);
        [Tooltip("Cell coordinate of the grid's bottom-left corner. Change this if you want the grid to extend into negative cell coordinates instead of starting at (0,0).")]
        [SerializeField] private Vector2Int origin = Vector2Int.zero;

        [Header("Collision")]
        [Tooltip("Optional tilemap whose populated cells are treated as non-walkable (walls, pits, etc). Leave empty if walkability is driven entirely by occupancy.")]
        [SerializeField] private Tilemap collisionTilemap;

        [Header("Height")]
        [Tooltip("Tilemaps ordered lowest to highest (index 0 = height 0, index 1 = height 1, ...) — paint each floor/platform level on its own layer. A cell's height is the index of the TOPMOST layer that has a tile there; a cell with no tile on any layer defaults to height 0. Leave empty entirely to keep the whole grid flat (height 0 everywhere) — existing scenes with no height layers configured behave exactly as before this feature existed.")]
        [SerializeField] private List<Tilemap> heightLayers = new List<Tilemap>();

        [Tooltip("Optional tilemap marking cells that can be climbed into/out of across a height difference (ladders, cliff edges, ledges). A cell with no tile here can only be entered/exited at the same height as the mover's current cell. Leave empty to disable climbing entirely.")]
        [SerializeField] private Tilemap climbableTilemap;

        /// <summary>
        /// Raised whenever a cell's occupant changes — after TryPlaceOccupant or RemoveOccupant.
        /// Occupant is null when the cell became empty. Useful for e.g. a minimap, a fog-of-war
        /// system, or AI that needs to react to something entering/leaving a cell, without those
        /// systems having to poll GridManager every frame.
        /// </summary>
        public event Action<Vector2Int, IGridOccupant> CellOccupantChanged;

        // The occupant grid is indexed by LOCAL coordinates (cell - origin), so it always
        // starts at [0,0] regardless of where `origin` is in world/cell space. Never index
        // _occupants with a raw cell coordinate — always go through the public methods below.
        private UnityEngine.Grid _grid;
        private IGridOccupant[,] _occupants;

        public Vector2Int Size => size;
        public Vector2Int Origin => origin;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Duplicate GridManager on '{name}' — destroying it. Only one GridManager may exist per scene.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _grid = GetComponent<UnityEngine.Grid>();
            _occupants = new IGridOccupant[size.x, size.y];
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>True if the cell coordinate falls within Size/Origin. Does not consider walls or occupancy.</summary>
        public bool InBounds(Vector2Int cell)
        {
            var local = cell - origin;
            return local.x >= 0 && local.x < size.x && local.y >= 0 && local.y < size.y;
        }

        public Vector2Int WorldToCell(Vector3 worldPos)
        {
            var c = _grid.WorldToCell(worldPos);
            return new Vector2Int(c.x, c.y);
        }

        /// <summary>Returns the world-space center of a cell — always snap entities to this, never to the cell's corner.</summary>
        public Vector3 CellToWorld(Vector2Int cell)
        {
            return _grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
        }

        /// <summary>True if the cell is inside the grid and not blocked by static geometry (walls). Ignores occupancy — see IsFree for that.</summary>
        public bool IsWalkable(Vector2Int cell)
        {
            if (!InBounds(cell)) return false;
            if (collisionTilemap != null && collisionTilemap.HasTile(new Vector3Int(cell.x, cell.y, 0))) return false;
            return true;
        }

        /// <summary>
        /// The elevation "level" of a cell, per the heightLayers stack (topmost populated
        /// layer wins). Cells not covered by any height layer default to 0 — with no
        /// heightLayers configured at all, every cell is height 0, so climbing never
        /// triggers and movement behaves exactly as it did before height existed.
        /// </summary>
        public int GetHeight(Vector2Int cell)
        {
            var cellPos = new Vector3Int(cell.x, cell.y, 0);
            for (var i = heightLayers.Count - 1; i >= 0; i--)
            {
                var layer = heightLayers[i];
                if (layer != null && layer.HasTile(cellPos)) return i;
            }
            return 0;
        }

        /// <summary>True if this cell can be entered/exited across a height difference (a ladder, cliff edge, etc). Irrelevant for two cells at the same height, which never need to "climb" anything.</summary>
        public bool IsClimbable(Vector2Int cell)
        {
            return climbableTilemap != null && climbableTilemap.HasTile(new Vector3Int(cell.x, cell.y, 0));
        }

        public IGridOccupant GetOccupant(Vector2Int cell)
        {
            if (!InBounds(cell)) return null;
            var local = cell - origin;
            return _occupants[local.x, local.y];
        }

        /// <summary>True if the cell is walkable AND has no occupant — the check to use before moving something into a cell.</summary>
        public bool IsFree(Vector2Int cell) => IsWalkable(cell) && GetOccupant(cell) == null;

        /// <summary>Registers an occupant at a cell. Fails without side effects if the cell isn't free — always check the return value.</summary>
        public bool TryPlaceOccupant(IGridOccupant occupant, Vector2Int cell)
        {
            if (occupant == null || !IsFree(cell)) return false;

            var local = cell - origin;
            _occupants[local.x, local.y] = occupant;
            CellOccupantChanged?.Invoke(cell, occupant);
            return true;
        }

        /// <summary>
        /// Clears an occupant from a cell. No-ops silently if `occupant` isn't actually the
        /// one registered there (e.g. it already moved elsewhere) — this makes it safe to call
        /// from OnDisable/OnDestroy without first re-checking current state.
        /// </summary>
        public void RemoveOccupant(Vector2Int cell, IGridOccupant occupant)
        {
            if (!InBounds(cell)) return;

            var local = cell - origin;
            if (_occupants[local.x, local.y] != occupant) return;

            _occupants[local.x, local.y] = null;
            CellOccupantChanged?.Invoke(cell, null);
        }

        /// <summary>
        /// Atomically moves an occupant from one cell to another: fails (returning false, no
        /// side effects) if the destination isn't free, so callers never need to remove-then-fail
        /// to re-add. This only updates GridManager's bookkeeping — it does not move the occupant's
        /// Transform. Pair it with a visual move (see GridEntity.TryStep) on the caller's side.
        /// </summary>
        public bool TryMoveOccupant(IGridOccupant occupant, Vector2Int from, Vector2Int to)
        {
            if (!IsFree(to)) return false;

            RemoveOccupant(from, occupant);
            TryPlaceOccupant(occupant, to);
            return true;
        }

#if UNITY_EDITOR
        // Editor-only debug visualization of grid bounds. Never runs in a player build.
        private void OnDrawGizmos()
        {
            var grid = GetComponent<UnityEngine.Grid>();
            if (grid == null) return;

            Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
            for (var x = 0; x <= size.x; x++)
            {
                var from = grid.CellToWorld(new Vector3Int(origin.x + x, origin.y, 0));
                var to = grid.CellToWorld(new Vector3Int(origin.x + x, origin.y + size.y, 0));
                Gizmos.DrawLine(from, to);
            }
            for (var y = 0; y <= size.y; y++)
            {
                var from = grid.CellToWorld(new Vector3Int(origin.x, origin.y + y, 0));
                var to = grid.CellToWorld(new Vector3Int(origin.x + size.x, origin.y + y, 0));
                Gizmos.DrawLine(from, to);
            }
        }
#endif

        private void OnValidate()
        {
            size.x = Mathf.Max(1, size.x);
            size.y = Mathf.Max(1, size.y);
        }
    }
}
