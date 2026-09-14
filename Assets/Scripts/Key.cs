using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Key : MonoBehaviour
{
    [Tooltip("Which Door(s) this key unlocks - a Door only opens for a key whose keyId matches its own.")]
    [SerializeField] private string keyId = "default";

    [Header("Visuals")]
    [SerializeField] private Animator anim;

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

            if (anim != null)
                anim.SetTrigger("pickup");

            Destroy(gameObject);
        }
    }
}
