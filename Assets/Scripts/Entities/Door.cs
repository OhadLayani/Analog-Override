using AnalogOverride.GridSystem;
using UnityEngine;

namespace AnalogOverride.Entities
{
    /// <summary>
    /// A grid entity that blocks movement like a wall until bumped by something carrying
    /// a matching key (see GameManager.HasKey/CollectKey), at which point it opens. Not
    /// pushable (see GridEntity's default) — the only way past it is unlocking it.
    /// Opening doesn't consume the key: the same key opens every Door sharing its keyId.
    /// </summary>
    public class Door : GridEntity, IInteractable
    {
        [Tooltip("Must match a Key's keyId for that key to open this door.")]
        [SerializeField] private string keyId = "default";

        [Header("Visuals")]
        [SerializeField] private Animator anim;

        [Tooltip("If true, the door GameObject is deactivated once opened (disappears entirely, stops registering as a grid occupant via GridEntity.OnDisable). If false, it stays active - e.g. to keep an 'open door' sprite visible - but is explicitly removed from GridManager so it stops blocking.")]
        [SerializeField] private bool disableOnOpen = true;

        private bool isOpen;

        public void Interact(GridEntity source)
        {
            if (isOpen) return;

            if (GameManager.Instance == null || !GameManager.Instance.HasKey(keyId))
            {
                // Locked and no matching key - hook a "locked"/rattle animation or sound here if wanted.
                return;
            }

            Open();
        }

        private void Open()
        {
            isOpen = true;

            if (anim != null)
                anim.SetTrigger("open");

            if (disableOnOpen)
            {
                // Deactivating fires GridEntity.OnDisable(), which already frees this
                // door's cell in GridManager - no need to remove the occupant by hand.
                gameObject.SetActive(false);
            }
            else
            {
                Manager.RemoveOccupant(CurrentCell, this);
            }
        }
    }
}
