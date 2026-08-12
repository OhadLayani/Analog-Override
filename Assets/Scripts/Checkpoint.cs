using UnityEngine;
using AnalogOverride.GridSystem;

[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Animator anim;

    private bool isActive;
    private Vector2Int gridCell;

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
        }
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