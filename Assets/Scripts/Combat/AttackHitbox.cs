using System;
using UnityEngine;

namespace AnalogOverride.Combat
{
    /// <summary>
    /// Lives on the "attack" child GameObject that gets activated for the duration of a
    /// swing (see PlayerAttack). While active, its trigger collider reports every alive
    /// IAttackable it touches via TargetDetected — this script only detects, it doesn't
    /// decide what happens next (that's the "listener on the parent object" from the design).
    ///
    /// Purely trigger-driven: OnTriggerEnter2D also fires for a collider that was ALREADY
    /// overlapping this one at the moment it gets enabled (the normal case here, since
    /// PlayerAttack turns this whole GameObject on right as the swing starts) — no extra
    /// "check what's already inside" logic needed.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class AttackHitbox : MonoBehaviour
    {
        /// <summary>Fired once per IAttackable this hitbox touches while active and alive. Can fire more than once in one swing if multiple targets overlap it.</summary>
        public event Action<IAttackable> TargetDetected;

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.TryGetComponent<IAttackable>(out var target) && target.IsAlive)
            {
                TargetDetected?.Invoke(target);
            }
        }
    }
}
