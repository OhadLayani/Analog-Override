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

        [Tooltip("When the hit zooms the camera out, seconds the door waits before opening, so it's in view. Match the Cinemachine Brain's Default Blend time. No wait if already zoomed out.")]
        [Min(0f)]
        [SerializeField] private float zoomWaitSeconds = 0.5f;

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
            if (door == null || door.IsBusy) return;

            AudioManager.Instance?.PlaySwitchHit();

            // Zoom out (and stay out) so the door is in view; give the camera time to get there.
            var zoomedNow = ZoomToggle.Instance != null && ZoomToggle.Instance.ZoomOut();

            if (door.OpenBriefly(zoomedNow ? zoomWaitSeconds : 0f))
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
