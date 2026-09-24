namespace AnalogOverride.Combat
{
    /// <summary>
    /// Implement on anything that can be hit by an attack (see AttackHitbox). Deliberately
    /// minimal — health totals, death behavior, damage types, etc. belong to whatever
    /// implements this, not to the attack system itself.
    /// </summary>
    public interface IAttackable
    {
        bool IsAlive { get; }
        void TakeDamage(int amount);
    }
}
