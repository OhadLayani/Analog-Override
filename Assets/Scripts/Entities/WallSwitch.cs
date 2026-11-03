using AnalogOverride.Combat;
using UnityEngine;

namespace AnalogOverride.Entities
{
    /// <summary>
    /// A switch on the wall that the player's attack flips on. It opens its TimedDoor for a short
    /// window and flips back off once the door has fully closed. Hits while the door is busy are
    /// ignored. Needs a trigger Collider2D over its art so the attack can find it.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class WallSwitch : MonoBehaviour, IAttackable
    {
        [SerializeField] private SpriteRenderer switchRenderer;
        [SerializeField] private Sprite offSprite;
        [SerializeField] private Sprite onSprite;

        [Tooltip("The door this switch opens.")]
        [SerializeField] private TimedDoor door;

        /// <summary>Hittable only while the door is closed and idle.</summary>
        public bool IsAlive => door != null && !door.IsBusy;

        private void OnEnable()
        {
            if (door != null) door.Closed += HandleDoorClosed;
        }

        private void OnDisable()
        {
            if (door != null) door.Closed -= HandleDoorClosed;
        }

        private void Start()
        {
            if (door == null)
            {
                Debug.LogWarning($"{name} has no Door assigned, so hitting it won't open anything.", this);
            }

            ShowSprite(offSprite);
        }

        public void TakeDamage(int amount)
        {
            if (door != null && door.OpenBriefly())
            {
                AnalyticsLogger.Instance?.LogInteraction(name, "pressed");
                ShowSprite(onSprite);
            }
        }

        private void HandleDoorClosed()
        {
            ShowSprite(offSprite);
        }

        private void ShowSprite(Sprite sprite)
        {
            if (switchRenderer != null && sprite != null) switchRenderer.sprite = sprite;
        }
    }
}
