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
    }
}
