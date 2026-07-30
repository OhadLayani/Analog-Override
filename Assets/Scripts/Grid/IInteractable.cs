namespace AnalogOverride.GridSystem
{
    /// <summary>
    /// Implement on a non-pushable occupant (door, NPC, sign, chest...) to react when
    /// another GridEntity bumps into its cell. Bumping never moves the source into the
    /// occupant's cell — Interact is a "reacted, but stayed put" hook, not a teleport.
    /// </summary>
    public interface IInteractable
    {
        void Interact(GridEntity source);
    }
}
