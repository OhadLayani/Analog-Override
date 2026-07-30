using UnityEngine;

namespace AnalogOverride.GridSystem
{
    /// <summary>
    /// Anything that can occupy a single grid cell and block/be-found-in it.
    /// Implement this (in practice, by extending <see cref="GridEntity"/>) for
    /// players, enemies, pushable blocks, or any other object GridManager needs
    /// to track occupancy for.
    /// </summary>
    public interface IGridOccupant
    {
        /// <summary>The cell this occupant is currently registered at in GridManager. Source of truth for game logic — updates instantly on move, independent of any visual tween.</summary>
        Vector2Int CurrentCell { get; }

        /// <summary>If true, another GridEntity stepping into this occupant's cell will attempt to push it forward instead of being blocked.</summary>
        bool IsPushable { get; }
    }
}
