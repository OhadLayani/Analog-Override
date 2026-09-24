using UnityEngine;

namespace AnalogOverride.Combat
{
    /// <summary>
    /// Minimal stand-in for testing PlayerAttack/AttackHitbox before real enemies exist.
    /// Logs each hit and deactivates once its hit count is reached — nothing more. Delete
    /// this once actual enemy scripts exist; it's only here to give the attack system
    /// something to detect and react to.
    /// </summary>
    public class TestDummy : MonoBehaviour, IAttackable
    {
        [SerializeField] private int hitsToDefeat = 3;

        private int hitsTaken;

        public bool IsAlive => hitsTaken < hitsToDefeat;

        public void TakeDamage(int amount)
        {
            if (!IsAlive) return;

            hitsTaken += amount;
            Debug.Log($"{name} took {amount} damage ({hitsTaken}/{hitsToDefeat})", this);

            if (!IsAlive)
            {
                Debug.Log($"{name} defeated!", this);
                gameObject.SetActive(false);
            }
        }
    }
}
