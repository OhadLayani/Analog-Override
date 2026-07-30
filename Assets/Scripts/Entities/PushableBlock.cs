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
        // Reset() only sets the inspector default when this component is first added
        // (or via the right-click Reset menu) — it can still be unchecked by hand afterwards.
        private void Reset()
        {
            pushable = true;
        }
    }
}
