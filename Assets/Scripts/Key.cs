using AnalogOverride.Combat;
using AnalogOverride.GridSystem;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Key : MonoBehaviour, IInteractable, IAttackable
{
    [Tooltip("Which Door(s) this key unlocks - a Door only opens for a key whose keyId matches its own.")]
    [SerializeField] private string keyId = "default";

    /// <summary>The id that pairs this key with its Door(s).</summary>
    public string KeyId => keyId;

    [Header("Visuals")]
    [SerializeField] private Animator anim;

    /// <summary>
    /// Fired the moment a key is actually picked up (not on the silent self-destruct in
    /// Start() when respawning with the key already collected), carrying its keyId, so UI
    /// can react without polling GameManager.HasKey. Static since the Key instance is
    /// destroyed right after firing - subscribers (e.g. UiManager) manage their own
    /// subscribe/unsubscribe lifecycle in Start()/OnDisable().
    /// </summary>
    public static event System.Action<string> KeyCollected;

    private bool collected;

    private void Start()
    {
        // If this key was already collected before this scene load (e.g. respawning
        // after death), don't let it be picked up again - just remove it.
        if (GameManager.Instance != null && GameManager.Instance.HasKey(keyId))
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the colliding object is the player
        if (collision.TryGetComponent<CharacterController>(out _))
        {
            Collect();
        }
    }

    /// <summary>
    /// Picks the key up by reaching for it instead of walking over it — the route for a key
    /// sitting on a raised level the player can't step onto (see GridEntity.TryReach).
    /// </summary>
    public void Interact(GridEntity source)
    {
        Collect();
    }

    /// <summary>False once collected, so the attack hitbox doesn't report a key that's already been picked up (it lingers until the end of the frame).</summary>
    public bool IsAlive => !collected;

    /// <summary>
    /// Picks the key up by striking it (see PlayerAttack / AttackHitbox) — a third route besides
    /// walking over it and reaching for it. The damage amount is irrelevant; any hit collects it.
    /// </summary>
    public void TakeDamage(int amount)
    {
        Collect();
    }

    private void Collect()
    {
        // Destroy() only takes effect at the end of the frame, so without this a walk-over and a
        // reach landing in the same frame (or a second collider on this key) could collect twice.
        if (collected) return;
        collected = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CollectKey(keyId);
        }

        KeyCollected?.Invoke(keyId);

        if (anim != null)
            anim.SetTrigger("pickup");

        Destroy(gameObject);
    }
}
