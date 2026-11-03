using AnalogOverride.GridSystem;
using UnityEngine;

namespace AnalogOverride.Entities
{
    /// <summary>
    /// A grid entity that blocks movement like a wall until bumped by something carrying
    /// a matching key (see GameManager.HasKey/CollectKey), at which point it opens. Not
    /// pushable (see GridEntity's default) — the only way past it is unlocking it.
    /// Opening doesn't consume the key: the same key opens every Door sharing its keyId.
    ///
    /// Doors are the one grid object allowed to span more than one cell (see extraCells)
    /// since they're static — GridEntity itself stays strictly single-cell because
    /// multi-cell MOVEMENT (checking/claiming several cells at once mid-step) is a much
    /// harder problem nothing here needs yet. A multi-cell Door still has exactly one
    /// CurrentCell (its "anchor"), it just ALSO registers itself in GridManager at each
    /// offset in extraCells — so bumping any of them finds this same Door instance and
    /// opening it clears every cell at once, not just the one you happened to touch.
    /// </summary>
    public class Door : GridEntity, IInteractable, ISaveable
    {
        [Tooltip("Must match a Key's keyId for that key to open this door.")]
        [SerializeField] private string keyId = "default";

        [Tooltip("Extra cells this door also occupies/blocks, relative to its own CurrentCell (e.g. {(1,0)} for a door that's two tiles wide). Leave empty for a plain single-cell door.")]
        [SerializeField] private Vector2Int[] extraCells = System.Array.Empty<Vector2Int>();

        [Header("Visuals")]
        [SerializeField] private Animator anim;

        [Tooltip("If true, the door GameObject is deactivated once opened (disappears entirely, stops registering as a grid occupant via OnDisable). If false, it stays active - e.g. to keep an 'open door' sprite visible - but is explicitly removed from every cell it occupies so it stops blocking.")]
        [SerializeField] private bool disableOnOpen = true;

        [Header("Checkpoint")]
        [Tooltip("For a door that leads to a new area. When it's opened it also acts as a soft checkpoint: it saves the room as it is and makes the cell the player opened it from the respawn point. Unlike a charging station it doesn't refill the spring or count as a visit, and the next charging station touched replaces it.")]
        [SerializeField] private bool savesCheckpointWhenOpened;

        private bool isOpen;

        protected override void Start()
        {
            base.Start(); // Claims CurrentCell via the normal single-cell GridEntity flow

            if (Manager == null) return; // base.Start() already disabled us and logged why

            foreach (var offset in extraCells)
            {
                Manager.TryPlaceOccupant(this, CurrentCell + offset);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable(); // Frees CurrentCell

            if (Manager == null) return;

            foreach (var offset in extraCells)
            {
                Manager.RemoveOccupant(CurrentCell + offset, this);
            }
        }

        public void Interact(GridEntity source)
        {
            if (isOpen) return;

            if (GameManager.Instance == null || !GameManager.Instance.HasKey(keyId))
            {
                // Locked and no matching key - hook a "locked"/rattle animation or sound here if wanted.
                return;
            }

            Open();
            AnalyticsLogger.Instance?.LogInteraction(name, "opened");

            if (savesCheckpointWhenOpened && source != null)
            {
                // After Open(), so the snapshot has this door open. No checkpoint object to activate:
                // null just means "deactivate every charging station's active marker".
                // Disabled: death now fully resets the level, so the door no longer saves a respawn cell or the room.
                // GameManager.Instance.ActivateCheckpoint(null, source.CurrentCell);
                // RoomSnapshot.Instance?.Capture();
                AnalyticsLogger.Instance?.LogDoorCheckpoint(name);
            }
        }

        /// <summary>"open" or "closed", for the room snapshot.</summary>
        public string CaptureState() => isOpen ? "open" : "closed";

        /// <summary>Re-opens a door that was open when the room was saved. A closed one is already as the scene made it.</summary>
        public void RestoreState(string state)
        {
            if (state == "open" && !isOpen)
            {
                Open();
            }
        }

        private void Open()
        {
            isOpen = true;

            if (anim != null)
                anim.SetTrigger("open");

            if (disableOnOpen)
            {
                // Deactivating fires OnDisable() above, which already frees every cell
                // this door occupies (CurrentCell + extraCells) - nothing more to do here.
                gameObject.SetActive(false);
            }
            else
            {
                Manager.RemoveOccupant(CurrentCell, this);
                foreach (var offset in extraCells)
                {
                    Manager.RemoveOccupant(CurrentCell + offset, this);
                }
            }
        }
    }
}
