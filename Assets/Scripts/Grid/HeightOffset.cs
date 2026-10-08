using UnityEngine;

namespace AnalogOverride.GridSystem
{
    /// <summary>Raises this object N levels above its cell's height, for height checks such as the attack's reach (e.g. a lamp standing on a table).</summary>
    public class HeightOffset : MonoBehaviour
    {
        [Tooltip("How many levels above its cell's height this object counts as standing.")]
        [Min(0)]
        [SerializeField] private int levels = 1;

        public int Levels => levels;

        /// <summary>
        /// The level something counts as being on: the height of its cell, plus its HeightOffset if it has
        /// one. The cell is its own GridEntity's, or that of the GridEntity it rides on (a lamp on a
        /// pushable table); otherwise the cell under its pivot.
        /// </summary>
        public static int LevelOf(GridManager manager, Component component)
        {
            var gridEntity = component.GetComponentInParent<GridEntity>();
            var cell = gridEntity != null
                ? gridEntity.CurrentCell
                : manager.WorldToCell(component.transform.position);

            var level = manager.GetHeight(cell);
            if (component.TryGetComponent<HeightOffset>(out var offset))
            {
                level += offset.Levels;
            }

            return level;
        }
    }
}
