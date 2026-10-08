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
    ///
    /// A light shows its state: green while a roomba is rushing to it or docked at it, red otherwise.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class RoombaButton : GridEntity, IAttackable
    {
        [Tooltip("The roombas this station calls.")]
        [SerializeField] private Roomba[] roombas;

        [Header("Visuals")]
        [SerializeField] private GameObject greenLight;
        [SerializeField] private GameObject redLight;

        [Tooltip("The station's body sprite; the lights draw just above it.")]
        [SerializeField] private SpriteRenderer stationSprite;

        [SerializeField] private int lightSortOffset = 1;

        private bool lightOn;

        /// <summary>On (green light) while any of its roombas is rushing to it or docked at it.</summary>
        private bool isActive
        {
            get
            {
                if (roombas == null) return false;

                foreach (var roomba in roombas)
                {
                    if (roomba != null && roomba.IsRespondingTo(CurrentCell)) return true;
                }

                return false;
            }
        }

        /// <summary>Always alive: the station can't be destroyed, only hit. (Alive targets are the ones the attack hitbox reports.)</summary>
        public bool IsAlive => true;

        protected override void Start()
        {
            base.Start();

            if (roombas == null || roombas.Length == 0)
            {
                Debug.LogWarning($"{name} has no Roombas assigned, so attacking it won't call anything.", this);
            }

            // base.Start() has given the body its runtime (Y-based) order; the station never moves, so once is enough.
            SortAboveStation(greenLight);
            SortAboveStation(redLight);

            ShowLight(false);
        }

        /// <summary>Draws a light just above the station's body sprite.</summary>
        private void SortAboveStation(GameObject lightObject)
        {
            if (stationSprite == null || lightObject == null) return;

            if (lightObject.TryGetComponent(out SpriteRenderer lightRenderer))
            {
                lightRenderer.sortingOrder = stationSprite.sortingOrder + lightSortOffset;
            }
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsGamePaused) return;

            var active = isActive;
            if (active != lightOn)
            {
                ShowLight(active);
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
        }

        /// <summary>Shows the green light when on, the red one when off.</summary>
        private void ShowLight(bool on)
        {
            lightOn = on;

            if (greenLight != null)
                greenLight.SetActive(on);

            if (redLight != null)
                redLight.SetActive(!on);
        }
    }
}
