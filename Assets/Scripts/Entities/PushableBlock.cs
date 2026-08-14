using AnalogOverride.GridSystem;

namespace AnalogOverride.Entities
{
    /// <summary>
    /// A grid entity that does nothing on its own but can be shoved around by anything
    /// that pushes into it (see GridEntity.TryStep). Intentionally has zero extra logic —
    /// if you need a block that also does something (e.g. lands on a pressure plate,
    /// breaks, leaves a trail), add that behaviour in a separate component on the same
    /// GameObject rather than growing this class, so plain movement stays trivial to reason about.
    /// </summary>
    public class PushableBlock : GridEntity
    {
        // Reset() only sets inspector defaults when this component is first added (or via
        // the right-click Reset menu) — both can still be changed by hand afterwards.
        private void Reset()
        {
            pushable = true;

            // Default a shoved block to NOT climb ledges on its own — without this it'd
            // inherit GridEntity's climbHeight of 1, so pushing a crate towards a climbable
            // cliff edge would silently shove it up the ledge. If you want a specific block
            // that CAN be pushed up/down a ledge, raise ClimbHeight on that instance/prefab.
            climbHeight = 0;
        }
    }
}
