using System.Collections;
using System.Collections.Generic;
using AnalogOverride.Combat;
using AnalogOverride.GridSystem;
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

    // CharacterController.StretchStage while the body is at full stretch.
    private const int StretchFull = 2;

    private CharacterController characterController;
    private bool isAttacking;

    // Everything already struck this swing, so the hitbox and the grid-cell check below can't both
    // land on the same target (or the hitbox on it twice).
    private readonly HashSet<IAttackable> struckThisSwing = new HashSet<IAttackable>();

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
        struckThisSwing.Clear();
        SetAttacking(true);
        StrikeReachedCells();

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
        if (IsInReach(target))
        {
            Strike(target);
        }
    }

    /// <summary>
    /// Facing up or down the arms only reach out to the SIDES, but the hitbox is drawn low on the body
    /// and also overlaps the cell below. So for those facings only targets in the player's own row
    /// count, however far along it the hitbox reaches. Facing left or right everything the hitbox
    /// touches counts. Anything that isn't a component to take a position from is let through.
    /// A target riding on a grid object (a lamp on a table) is in that object's row, wherever it's drawn.
    /// </summary>
    private bool IsInReach(IAttackable target)
    {
        var manager = GridManager.Instance;
        if (manager == null || characterController == null) return true;
        if (characterController.FacingDirection.x != 0) return true;
        if (target is not Component component) return true;

        return CellOf(manager, component).y == characterController.CurrentCell.y;
    }

    /// <summary>
    /// Hits whatever sits in the grid cells the arms reach, regardless of where the arms are drawn.
    /// The hitbox follows the arms ART, which hangs low on the body, so on its own it can miss a
    /// target in the player's own row — it reached things below the player but not to the left or
    /// right. The grid is the source of truth for what's adjacent, so this checks those cells
    /// directly; the hitbox still catches anything the arms touch beyond them.
    ///
    /// Which cells matches what's drawn: facing left/right the arms stretch out in profile, so just
    /// the cell in front; facing up/down they stretch out to BOTH sides only, so the cells to the
    /// left and right (see IsInReach, which keeps the hitbox to the same rule).
    /// </summary>
    private void StrikeReachedCells()
    {
        var manager = GridManager.Instance;
        if (manager == null || characterController == null) return;

        var facing = characterController.FacingDirection;
        var origin = characterController.CurrentCell;

        if (facing.x != 0)
        {
            StrikeCell(manager, origin + facing);
        }
        else
        {
            StrikeCell(manager, origin + Vector2Int.left);
            StrikeCell(manager, origin + Vector2Int.right);
        }
    }

    private void StrikeCell(GridManager manager, Vector2Int cell)
    {
        // A little smaller than the cell so a target in a neighbouring cell isn't clipped.
        var hits = Physics2D.OverlapBoxAll(manager.CellToWorld(cell), manager.CellSize * 0.8f, 0f);
        foreach (var hit in hits)
        {
            // Only targets that belong to this cell: a tall lamp's head can be drawn over the cell
            // above its table, but it's only hit there by the arms actually touching it.
            if (hit.TryGetComponent<IAttackable>(out var target) && target.IsAlive
                && CellOf(manager, hit) == cell)
            {
                Strike(target);
            }
        }
    }

    private void Strike(IAttackable target)
    {
        // Refused targets stay out of struckThisSwing, so a later frame of the same swing can still land.
        if (!CanReachHeight(target)) return;

        if (struckThisSwing.Add(target))
        {
            target.TakeDamage(attackDamage);
        }
    }

    /// <summary>
    /// Targets on a higher level than the player need the body stretched: one level up only at full
    /// stretch, two or more never. Same-level and lower targets are always reachable. A target's level
    /// is its cell's height plus its HeightOffset, if it has one (e.g. a lamp standing on a table).
    /// </summary>
    private bool CanReachHeight(IAttackable target)
    {
        var manager = GridManager.Instance;
        if (manager == null || characterController == null) return true;
        if (target is not Component component) return true;

        var levelsUp = HeightOffset.LevelOf(manager, component) - manager.GetHeight(characterController.CurrentCell);
        if (levelsUp <= 0) return true;

        return levelsUp == 1 && characterController.StretchStage == StretchFull;
    }

    /// <summary>The cell a target belongs to: its own GridEntity's cell, or that of the one it rides on (a lamp on a pushable table); otherwise the cell under its pivot.</summary>
    private static Vector2Int CellOf(GridManager manager, Component component)
    {
        var gridEntity = component.GetComponentInParent<GridEntity>();
        return gridEntity != null
            ? gridEntity.CurrentCell
            : manager.WorldToCell(component.transform.position);
    }
}
