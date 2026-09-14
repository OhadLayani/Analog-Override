using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Key : MonoBehaviour
{
    [Tooltip("Which Door(s) this key unlocks - a Door only opens for a key whose keyId matches its own.")]
    [SerializeField] private string keyId = "default";

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
        
        if (collision.TryGetComponent<CharacterController>(out var player))
        {
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
}
