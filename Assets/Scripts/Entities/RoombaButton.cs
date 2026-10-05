using AnalogOverride.Combat;
using AnalogOverride.GridSystem;
using UnityEngine;

namespace AnalogOverride.Entities
{
    /// <summary>
    /// The roombas' docking station. Attacking it calls every assigned roomba over: they drop what
    /// they're doing, rush to the station, then sit flashing there for a moment before carrying on
    /// (see Roomba.CallTo).
    ///
    /// It reacts to ATTACKS, not to being walked into: it's an IAttackable, which is all the player's
    /// attack hitbox looks for, so it needs a Collider2D for the hitbox to detect (a plain,
    /// non-trigger BoxCollider2D on the prefab does it). Like Door and the crates it's a GridEntity, so
    /// it still occupies its cell and blocks movement through it.
    ///
    /// Roombas already rushing or docked ignore further calls, so hitting it repeatedly can't keep
    /// them docked forever.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class RoombaButton : GridEntity, IAttackable
    {
        [Tooltip("The roombas this station calls.")]
        [SerializeField] private Roomba[] roombas;

        [Header("Visuals")]
        [SerializeField] private Animator anim;

        /// <summary>Always alive: the station can't be destroyed, only hit. (Alive targets are the ones the attack hitbox reports.)</summary>
        public bool IsAlive => true;

        protected override void Start()
        {
            base.Start();

            if (roombas == null || roombas.Length == 0)
            {
                Debug.LogWarning($"{name} has no Roombas assigned, so attacking it won't call anything.", this);
            }
        }

        public void TakeDamage(int amount)
        {
            foreach (var roomba in roombas)
            {
                if (roomba != null)
                {
                    roomba.CallTo(CurrentCell);
                }
            }

            if (anim != null)
                anim.SetTrigger("hit");
        }
    }
}
