using UnityEngine;
using AnalogOverride.GridSystem;

public class CharacterController : GridEntity
{
    private Animator animator;
    private SpringManager springManager;
    private int stepCounter;

    // The entity's own configured move duration/curve (GridEntity's Inspector-set values),
    // cached once so they can be restored after a high-friction step.
    private float baseMoveDuration;
    private AnimationCurve baseMoveCurve;
    [SerializeField] private int stepsPerBar = 3;

    [Tooltip("Steps per energy bar when standing on a high-friction cell (e.g. carpet, per GridManager.IsHighFriction) instead of the normal stepsPerBar above. Lower than stepsPerBar means friction drains energy faster.")]
    [SerializeField] private int stepsPerBarHighFriction = 2;

    [Tooltip("Seconds the visual slide takes while stepping onto a high-friction cell (e.g. carpet, per GridManager.IsHighFriction), overriding this entity's normal move duration for that one step. Higher than the base value makes the slow-down feel heavy/sluggish, visually matching the extra energy cost already charged for the same terrain via stepsPerBarHighFriction.")]
    [Min(0f)]
    [SerializeField] private float highFrictionMoveDuration = 1.2f;

    [Tooltip("Motion shape for a high-friction step, overriding the base Move Curve for that one step so slow doesn't just mean 'the same smooth glide, stretched out'. Default is a lurch-drag-lurch shape (quick initial movement, a stall in the middle, a quick final snap) meant to read as pushing through resistance rather than sliding on ice. Tune the curve directly in the Inspector to taste — this shape is a starting point, not a precisely dialed-in one.")]
    [SerializeField]
    private AnimationCurve highFrictionMoveCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3f),
        new Keyframe(0.3f, 0.55f, 0.3f, 0.3f),
        new Keyframe(0.7f, 0.65f, 0.3f, 0.3f),
        new Keyframe(1f, 1f, 3f, 0f));

    [Tooltip("Energy bars charged per unit of Weight pushed, on top of (not counted towards) the normal per-step cost above. E.g. pushing a Weight-3 crate at 1 bar/weight costs 3 bars immediately, and doesn't advance stepCounter. Scaled up further by the friction ratio (stepsPerBar / stepsPerBarHighFriction) when the pusher ends up standing on a high-friction cell.")]
    [SerializeField] private float energyCostPerWeight = 1f;

    private void Awake()
    {
        springManager = SpringManager.Instance;
    }

    protected override void Start()
    {
        // If a checkpoint is saved, teleport to it BEFORE snapping to the grid
        if (GameManager.Instance != null && GameManager.Instance.HasCheckpoint)
        {
            transform.position = GridManager.Instance.CellToWorld(GameManager.Instance.RespawnCell);
        }

        base.Start(); // Snaps the player to the grid's center on spawn and claims the cell
        baseMoveDuration = MoveDuration; // Remember the Inspector-configured slide speed/shape before we ever override them
        baseMoveCurve = MoveCurve;
        animator = GetComponentInChildren<Animator>();
        springManager ??= SpringManager.Instance;
    }

    private void OnEnable()
    {
        springManager ??= SpringManager.Instance;
        if (springManager != null)
        {
            springManager.BarsReachedZero += HandleBarsReachedZero;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (springManager != null)
        {
            springManager.BarsReachedZero -= HandleBarsReachedZero;
        }
    }

    private void Update()
    {
        // GUARD CLAUSE: Read the centralized state from GameManager
        if (GameManager.Instance != null && GameManager.Instance.IsGamePaused)
        {
            return;
        }

        Vector2Int dir = Vector2Int.zero;

        // Determine discrete grid direction based on input
        if (Input.GetKey(KeyCode.A))
        {
            dir = Vector2Int.left;
            animator.SetInteger("Direction", 3);
        }
        else if (Input.GetKey(KeyCode.D))
        {
            dir = Vector2Int.right;
            animator.SetInteger("Direction", 2);
        }
        else if (Input.GetKey(KeyCode.W))
        {
            dir = Vector2Int.up;
            animator.SetInteger("Direction", 1);
        }
        else if (Input.GetKey(KeyCode.S))
        {
            dir = Vector2Int.down;
            animator.SetInteger("Direction", 0);
        }

        // If a directional key is pressed, attempt to step on the grid
        if (dir != Vector2Int.zero)
        {
            // "Currently standing on carpet": read BEFORE calling TryStep, i.e. the cell the player
            // occupies right now (the step's origin), not the destination. The step that ENTERS carpet
            // from normal ground therefore plays at normal speed; only a step taken while already
            // resting on carpet — including the step that leaves it — is slow. Intentionally a
            // different cell than the post-step check below (which drives energy cost from the
            // destination cell instead), so the two are independent reads, not shared.
            bool startingOnCarpet = GridManager.Instance != null && GridManager.Instance.IsHighFriction(CurrentCell);
            MoveDuration = startingOnCarpet ? highFrictionMoveDuration : baseMoveDuration;
            MoveCurve = startingOnCarpet ? highFrictionMoveCurve : baseMoveCurve;

            if (TryStep(dir, out var pushedWeight))
            {
                bool onHighFriction = GridManager.Instance != null && GridManager.Instance.IsHighFriction(CurrentCell);

                if (pushedWeight > 0f)
                {
                    // Friction slows pushing too: scale the weight-based cost by the same
                    // ratio that governs plain-step friction (e.g. 3/2 = 1.5x by default),
                    // so the two costs stay derived from one pair of tunable numbers.
                    var frictionMultiplier = onHighFriction ? (float)stepsPerBar / stepsPerBarHighFriction : 1f;
                    var cost = Mathf.Max(1, Mathf.RoundToInt(pushedWeight * energyCostPerWeight * frictionMultiplier));
                    springManager?.ReduceBars(cost);
                }
                else
                {
                    stepCounter++;

                    var threshold = onHighFriction ? stepsPerBarHighFriction : stepsPerBar;
                    if (stepCounter >= threshold)
                    {
                        springManager?.ReduceBars(1);
                        stepCounter = 0;
                    }
                }

                // Fires exactly once per "close call": Bars can only equal 1 right after the
                // single ReduceBars call that lands on it — any reduction taken FROM 1 goes
                // straight to 0 (death) instead, so this can't double-log while lingering at 1.
                if (springManager != null && springManager.Bars == 1)
                {
                    AnalyticsLogger.Instance?.LogLastBar();
                }
            }
        }
    }
    
    private void HandleBarsReachedZero()
    {
        Debug.Log("GAME OVER");
        AnalyticsLogger.Instance?.LogDeath();
        // Reload the scene when the player dies
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReloadScene();
        }
    }
    
    public void ResetStepCounter()
    {
        stepCounter = 0;
    }
}