using System.Collections;
using AnalogOverride.Combat;
using UnityEngine;

/// <summary>
/// Drives the attack: on click, charges energy, turns on the attack-hitbox child and plays
/// the arms stretch (stage 1 → 2 → 3 → 2 → 1) over Attack Duration, then turns the hitbox off.
/// ArmsVisual draws the stage and keeps the hitbox fitted to the arms sprite. Listens to the
/// hitbox child's TargetDetected event to actually apply damage.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerAttack : MonoBehaviour
{
    [Tooltip("The arms visual (on idleArms). Shows the stretch stage during an attack.")]
    [SerializeField] private ArmsVisual arms;

    [Tooltip("Activated for Attack Duration seconds on click. Must have an AttackHitbox component with a trigger Collider2D on it.")]
    [SerializeField] private AttackHitbox attackHitbox;

    [Tooltip("Seconds the whole attack takes (stretch out, hold, pull back). Also doubles as the attack's cooldown — a click is ignored while this is running.")]
    [Min(0f)]
    [SerializeField] private float attackDuration = 0.2f;

    [Tooltip("Share of Attack Duration spent at full stretch (stage 3). The rest is split evenly between stretching out and pulling back (stage 2).")]
    [Range(0f, 1f)]
    [SerializeField] private float fullStretchShare = 0.5f;

    [Tooltip("Energy bars charged per attack. Can drain the last bars and kill the player.")]
    [Min(0)]
    [SerializeField] private int energyCost = 3;

    [SerializeField] private int attackDamage = 1;

    private const int StageIdle = 0;
    private const int StageMid = 1;
    private const int StageFull = 2;

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

        // Disabling stops the coroutine, so don't leave the arms stretched.
        if (arms != null) arms.Stage = StageIdle;
        isAttacking = false;
    }

    private void Update()
    {
        // GUARD CLAUSE: Read the centralized state from GameManager
        if (GameManager.Instance != null && GameManager.Instance.IsGamePaused)
        {
            return;
        }

        if (isAttacking) return;

        // A stunned player can't swing (see CharacterController.ReceiveHit).
        if (characterController != null && characterController.IsStunned) return;

        if (Input.GetMouseButtonDown(0))
        {
            StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator AttackRoutine()
    {
        SetAttacking(true);

        // Charged after the hitbox is on: if this kills the player, the scene reloads anyway.
        SpringManager.Instance?.ReduceBars(energyCost);

        var holdTime = attackDuration * fullStretchShare;
        var moveTime = (attackDuration - holdTime) * 0.5f;

        SetStage(StageMid);
        yield return new WaitForSeconds(moveTime);
        SetStage(StageFull);
        yield return new WaitForSeconds(holdTime);
        SetStage(StageMid);
        yield return new WaitForSeconds(moveTime);

        SetAttacking(false);
    }

    private void SetAttacking(bool attacking)
    {
        isAttacking = attacking;
        if (!attacking) SetStage(StageIdle);

        if (attackHitbox != null)
            attackHitbox.gameObject.SetActive(attacking);
    }

    private void SetStage(int stage)
    {
        if (arms != null) arms.Stage = stage;
    }

    private void HandleTargetDetected(IAttackable target)
    {
        target.TakeDamage(attackDamage);
    }
}
