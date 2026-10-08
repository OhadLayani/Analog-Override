using System.Collections.Generic;
using AnalogOverride.GridSystem;
using UnityEngine;

namespace AnalogOverride.Entities
{
    /// <summary>
    /// A solid piece of furniture of any size and shape (bed, closet, table...). It isn't a GridEntity:
    /// it never moves and can't be pushed or interacted with. Instead its colliders draw its floor
    /// footprint, and every grid cell whose centre lies inside them is blocked like a wall (see
    /// GridManager.BlockCell), so walking, pushing, knockback and the Roomba all treat it as solid.
    ///
    /// Only colliders on this GameObject count, not its children (a key standing on a table has its
    /// own collider). Make them triggers: they only describe the footprint. Select the object to see
    /// the blocked cells as red squares in the Scene view.
    ///
    /// Draw order comes from the footprint's lowest row, with the same formula as GridEntity, one
    /// step behind so the player draws in front when standing beside it in that row.
    /// </summary>
    public class Furniture : MonoBehaviour
    {
        [Header("Draw Order")]
        [Tooltip("The sprite whose Order in Layer is set from the footprint's lowest row, so the player draws in front of it when below it and behind it when above. Leave empty to set the order by hand.")]
        [SerializeField] private SpriteRenderer sortingSprite;

        [Tooltip("Keep equal to GridEntity's Sorting Order Precision (default 100), so furniture and grid entities sort on the same scale.")]
        [SerializeField] private int sortingOrderPrecision = 100;

        [Tooltip("Keep equal to GridEntity's Sorting Order Base (default 10000), so furniture and grid entities sort on the same scale.")]
        [SerializeField] private int sortingOrderBase = 10000;

        private readonly List<Vector2Int> blockedCells = new List<Vector2Int>();

        private void Start()
        {
            var manager = GridManager.Instance;
            if (manager == null)
            {
                Debug.LogError($"{nameof(Furniture)} on '{name}' found no GridManager in the scene.", this);
                enabled = false;
                return;
            }

            CollectFootprint(manager.GetComponent<UnityEngine.Grid>(), blockedCells);

            if (blockedCells.Count == 0)
            {
                Debug.LogWarning($"{name} covers no grid cell, so nothing blocks the player. Add a trigger BoxCollider2D over its floor footprint, covering the centres of the cells it should block.", this);
                return;
            }

            var lowestY = float.MaxValue;
            foreach (var cell in blockedCells)
            {
                manager.BlockCell(cell);
                lowestY = Mathf.Min(lowestY, manager.CellToWorld(cell).y);
            }

            if (sortingSprite != null)
            {
                sortingSprite.sortingOrder = sortingOrderBase - Mathf.RoundToInt(lowestY * sortingOrderPrecision) - 1;
            }
        }

        private void OnDisable()
        {
            var manager = GridManager.Instance;
            if (manager == null) return;

            foreach (var cell in blockedCells)
            {
                manager.UnblockCell(cell);
            }

            blockedCells.Clear();
        }

        /// <summary>Fills `cells` with every cell whose centre lies inside one of this object's own enabled colliders.</summary>
        private void CollectFootprint(UnityEngine.Grid grid, List<Vector2Int> cells)
        {
            cells.Clear();

            foreach (var footprint in GetComponents<Collider2D>())
            {
                if (!footprint.enabled) continue;

                GetWorldRect(footprint, out var min, out var max);
                var from = grid.WorldToCell(min);
                var to = grid.WorldToCell(max);

                for (var x = from.x; x <= to.x; x++)
                {
                    for (var y = from.y; y <= to.y; y++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (cells.Contains(cell)) continue;

                        if (Covers(footprint, grid.GetCellCenterWorld(new Vector3Int(x, y, 0))))
                        {
                            cells.Add(cell);
                        }
                    }
                }
            }
        }

        /// <summary>The world-space box around a collider. Worked out by hand for a BoxCollider2D, so it's also right in edit mode.</summary>
        private static void GetWorldRect(Collider2D footprint, out Vector3 min, out Vector3 max)
        {
            if (footprint is BoxCollider2D box)
            {
                var half = box.size * 0.5f;
                min = new Vector3(float.MaxValue, float.MaxValue);
                max = new Vector3(float.MinValue, float.MinValue);

                for (var i = 0; i < 4; i++)
                {
                    var corner = box.offset + new Vector2(i % 2 == 0 ? -half.x : half.x, i < 2 ? -half.y : half.y);
                    var world = box.transform.TransformPoint(corner);
                    min = Vector3.Min(min, world);
                    max = Vector3.Max(max, world);
                }

                return;
            }

            var bounds = footprint.bounds;
            min = bounds.min;
            max = bounds.max;
        }

        /// <summary>Whether `point` lies inside the collider. Exact by hand for a BoxCollider2D (rotation and scale included); other shapes ask physics.</summary>
        private static bool Covers(Collider2D footprint, Vector3 point)
        {
            if (footprint is BoxCollider2D box)
            {
                point.z = box.transform.position.z;
                var local = (Vector2)box.transform.InverseTransformPoint(point) - box.offset;
                var half = box.size * 0.5f;
                return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
            }

            return footprint.OverlapPoint(point);
        }

        /// <summary>Shows the cells this piece blocks while it's selected, so the footprint can be lined up in edit mode.</summary>
        private void OnDrawGizmosSelected()
        {
            var manager = GridManager.Instance != null ? GridManager.Instance : FindAnyObjectByType<GridManager>();
            if (manager == null || !manager.TryGetComponent<UnityEngine.Grid>(out var grid)) return;

            var cells = new List<Vector2Int>();
            CollectFootprint(grid, cells);

            var size = grid.cellSize;
            foreach (var cell in cells)
            {
                var centre = grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));

                Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
                Gizmos.DrawCube(centre, size);
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(centre, size);
            }
        }
    }
}
