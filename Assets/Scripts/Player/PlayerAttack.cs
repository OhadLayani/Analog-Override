using System.Collections;
using AnalogOverride.Combat;
using UnityEngine;

/// <summary>
/// Drives the attack toggle: on click, swap from the idle child to the attack-hitbox child
/// for a fixed duration, then swap back. Listens to the hitbox child's TargetDetected event
/// to actually apply damage — this is the "listener on the parent object" from the design.
/// No animation yet by design — the active-object swap IS the whole visual for now; a real
/// animation can replace/augment SetAttacking later without touching the detection/event flow.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerAttack : MonoBehaviour
{
    [Tooltip("Active while not attacking.")]
    [SerializeField] private GameObject idleChild;

    [Tooltip("Activated for Attack Duration seconds on click. Must have an AttackHitbox component with a trigger Collider2D on it.")]
    [SerializeField] private AttackHitbox attackHitbox;

    [Tooltip("Distance from the player's own position to the hitbox's center, along whichever way the player is currently facing (CharacterController.FacingDirection) — repositioned fresh at the start of every swing, so a fixed child-position/offset in the Inspector doesn't matter.")]
    [SerializeField] private float hitboxReach = 0.5f;

    [Tooltip("Seconds the attack hitbox stays active before automatically reverting to Idle Child. Also doubles as the attack's cooldown — a click is ignored while this is running.")]
    [Min(0f)]
    [SerializeField] private float attackDuration = 0.2f;

    [SerializeField] private int attackDamage = 1;

    private CharacterController characterController;
    private bool isAttacking;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        if (attackHitbox != null)
            attackHitbox.TargetDetected += HandleTargetDetected;

        SetAttacking(false);
    }

    private void OnDisable()
    {
        if (attackHitbox != null)
            attackHitbox.TargetDetected -= HandleTargetDetected;
    }

    private void Update()
    {
        // GUARD CLAUSE: Read the centralized state from GameManager
        if (GameManager.Instance != null && GameManager.Instance.IsGamePaused)
        {
            return;
        }

        if (isAttacking) return;

        if (Input.GetMouseButtonDown(0))
        {
            StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator AttackRoutine()
    {
        SetAttacking(true);
        yield return new WaitForSeconds(attackDuration);
        SetAttacking(false);
    }

    private void SetAttacking(bool attacking)
    {
        isAttacking = attacking;

        if (idleChild != null)
            idleChild.SetActive(!attacking);

        if (attackHitbox != null)
        {
            if (attacking)
            {
                // Reposition BEFORE activating: the character doesn't turn by rotating or
                // flipping its Transform (facing is done via SpriteRenderer.flipX on the
                // animation clips), so a fixed local offset would stay stuck on one side
                // regardless of which way the player is actually facing. Deriving the offset
                // fresh from FacingDirection every swing keeps the hitbox correctly placed
                // without depending on whatever position it happens to be left at in the Inspector.
                var facing = characterController != null ? characterController.FacingDirection : Vector2Int.down;
                var currentLocalPos = attackHitbox.transform.localPosition;
                attackHitbox.transform.localPosition = new Vector3(facing.x * hitboxReach, facing.y * hitboxReach, currentLocalPos.z);
            }

            attackHitbox.gameObject.SetActive(attacking);
        }
    }

    private void HandleTargetDetected(IAttackable target)
    {
        target.TakeDamage(attackDamage);
    }
}
