using UnityEngine;
using AnalogOverride.GridSystem;

[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Animator anim;

    /// <summary>How many times this station can recharge the player before it stops working.</summary>
    [Header("Charges")]
    [SerializeField] private int maxUses = 2;

    [Header("Charge Sprites")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite fullSprite;
    [SerializeField] private Sprite halfSprite;
    [SerializeField] private Sprite emptySprite;

    private bool isActive;
    private Vector2Int gridCell;
    /// <summary>Charges left; resets to maxUses on every scene load (death and reset reload the scene).</summary>
    private int usesLeft;

    private void Awake()
    {
        usesLeft = maxUses;
        UpdateSprite();
    }

    private void Start()
    {
        // Calculate and cache exactly which grid cell this checkpoint sits in
        if (GridManager.Instance != null)
        {
            gridCell = GridManager.Instance.WorldToCell(transform.position);
        }
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterCheckpoint(this);
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.DeregisterCheckpoint(this);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the colliding object is the player
        CharacterController player = collision.GetComponent<CharacterController>();
        
        if (player != null)
        {
            // An empty station does nothing: no refill, no step-counter reset, no log
            if (usesLeft <= 0) return;

            AnalyticsLogger.Instance?.LogCheckpointVisit();

            // Tutorial task: mark "checkpoint" complete. No-op outside the Tutorial scene.
            TutorialManager.Instance?.NotifyCheckpointVisited();

            player.ResetStepCounter();
            // 1. ALWAYS reset the bars when the player touches the checkpoint
            if (SpringManager.Instance != null)
            {
                SpringManager.Instance.ResetBars();
            }

            // 2. ONLY activate (save location, animate) if it isn't already active
            if (!isActive)
            {
                ActivateCheckpoint();
            }

            // Disabled: death now fully resets the level, so the room isn't saved here.
            // 3. ALWAYS save the room as it is now, replacing the previous snapshot, so a death
            // brings it back to this state instead of resetting it
            // RoomSnapshot.Instance?.Capture();

            usesLeft--;
            UpdateSprite();
        }
    }

    /// <summary>Shows the full, half or empty sprite to match the charges left.</summary>
    private void UpdateSprite()
    {
        if (spriteRenderer == null) return;

        Sprite sprite = usesLeft >= maxUses ? fullSprite
                      : usesLeft <= 0 ? emptySprite
                      : halfSprite;

        if (sprite != null)
            spriteRenderer.sprite = sprite;
    }

    private void ActivateCheckpoint()
    {
        isActive = true;

        if (anim != null)
            anim.SetTrigger("activate");

        GameManager.Instance.ActivateCheckpoint(this, gridCell);
    }

    public void SetActiveState(bool state)
    {
        isActive = state;

        // Optional: animation when a checkpoint is deactivated by another one
        // if (anim != null && !state)
        //     anim.SetTrigger("deactivate"); 
    }
}