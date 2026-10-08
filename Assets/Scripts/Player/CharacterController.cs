using UnityEngine;
using AnalogOverride.GridSystem;

public class CharacterController : GridEntity
{
    /// <summary>The last non-zero direction the player pressed — i.e. which way the character is currently facing/animated to face, even while standing still. Defaults to down, matching the animator's own default Direction (0). Other systems (e.g. PlayerAttack) read this instead of re-deriving facing from input themselves.</summary>
    public Vector2Int FacingDirection { get; private set; } = Vector2Int.down;

    /// <summary>True while the walk animation is showing (a movement key is held or a step is sliding).</summary>
    public bool IsWalking { get; private set; }

    /// <summary>True from the start of a stretch until fully contracted again: planted in place, no walking or turning (attack still works).</summary>
    public bool IsStretching => stretchStage != StretchIdle || stretchTarget != StretchIdle;

    /// <summary>Body stretch stage for visuals: 0 = normal (mode 1), 1 = half (mode 2), 2 = full (mode 3).</summary>
    public int StretchStage => stretchStage;

    /// <summary>True while stunned after a hit (see ReceiveHit): no walking, stretching or attacking, and shown idle. See IsInvulnerable for how long further hits are ignored.</summary>
    public bool IsStunned => stunTimer > 0f;

    /// <summary>True from the moment of a hit until the stun AND the post-hit immunity have both run out (the player flashes throughout). Hits are ignored while this is true.</summary>
    public bool IsInvulnerable => immunityTimer > 0f || IsStunned;

    /// <summary>0..1 progress of plain steps toward the next bar cost (stepCounter / threshold for the current cell).</summary>
    public float StepProgress
    {
        get
        {
            var onHighFriction = GridManager.Instance != null && GridManager.Instance.IsHighFriction(CurrentCell);
            var threshold = onHighFriction ? stepsPerBarHighFriction : stepsPerBar;
            return threshold > 0 ? (float)stepCounter / threshold : 0f;
        }
    }

    private Animator animator;
    private SpringManager springManager;
    private int stepCounter;

    // Body stretch state: stretchStage is what's shown, stretchTarget is where it's heading
    // (Full while stretching/holding, Idle while contracting).
    private const int StretchIdle = 0;
    private const int StretchHalf = 1;
    private const int StretchFull = 2;
    private int stretchStage;
    private int stretchTarget;
    private float stretchTimer;

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

    [Header("Stretch")]
    [Tooltip("Press while standing still to stretch (1 → 2 → 3); hold to stay stretched; release to contract (3 → 2 → 1). No walking or turning while stretched; attacking still works.")]
    [SerializeField] private KeyCode stretchKey = KeyCode.Space;

    [Tooltip("Energy bars charged each time a stretch STARTS — not for staying stretched, and contracting is free.")]
    [Min(0)]
    [SerializeField] private int stretchEnergyCost = 1;

    [Tooltip("Seconds the half-stretch frame (mode 2) shows, both when stretching and when contracting.")]
    [Min(0f)]
    [SerializeField] private float stretchFrameDuration = 0.08f;

    // Disabled: how many levels up a stretch could reach with WASD. Unused until reaching returns (planned: stretch + attack).
    // [Tooltip("How many levels above the player's own a stretch can reach. 1 = a player on level 0 can act on level 1.")]
    // [Min(1)]
    // [SerializeField] private int stretchReach = 1;

    [Header("Taking hits")]
    [Tooltip("Seconds the knockback slide takes — far quicker than a walking step, so it reads as being thrown rather than walking backwards.")]
    [Min(0f)]
    [SerializeField] private float knockbackSlideSeconds = 0.25f;

    [Tooltip("Motion shape of the knockback slide. Default is a fast start that eases into the landing.")]
    [SerializeField]
    private AnimationCurve knockbackCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2f),
        new Keyframe(1f, 1f, 0f, 0f));

    [Tooltip("Seconds after the stun ends during which the player can't be hit again, so a hazard that keeps coming back can't chain stuns together. The player flashes for the whole stun plus this time.")]
    [Min(0f)]
    [SerializeField] private float postHitImmunitySeconds = 0.5f;

    [Tooltip("How many times per second the player flashes after a hit.")]
    [Min(0.1f)]
    [SerializeField] private float hitFlashesPerSecond = 6f;

    private float stunTimer;
    private float immunityTimer;
    private SpriteFlasher flasher;

    private void Awake()
    {
        springManager = SpringManager.Instance;

        if (!TryGetComponent(out flasher))
        {
            flasher = gameObject.AddComponent<SpriteFlasher>();
        }
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

        if (immunityTimer > 0f)
        {
            immunityTimer -= Time.deltaTime;
        }

        // Stunned: frozen and shown idle. A hit already cancelled any stretch (see ReceiveHit),
        // so skipping TickStretch here is safe.
        if (IsStunned)
        {
            stunTimer -= Time.deltaTime;
            UpdateAnimation(false);
            return;
        }

        // GetKey (not GetKeyUp), so a release that happened while paused is still noticed on resume.
        TickStretch(Input.GetKey(stretchKey));

        Vector2Int dir = ReadHeldDirection();

        // A stretch only starts while standing still: fully contracted, not sliding, no movement key held.
        if (Input.GetKeyDown(stretchKey) && !IsStretching && !IsMoving && dir == Vector2Int.zero)
        {
            ChargeBars(stretchEnergyCost);
            BeginStretch();
        }

        // Planted while stretched: no walking, no turning — only PlayerAttack still acts.
        if (IsStretching)
        {
            UpdateAnimation(false);
            return;
        }

        if (dir != Vector2Int.zero)
        {
            FacingDirection = dir;
        }

        // If a directional key is pressed, attempt to step on the grid
        if (dir != Vector2Int.zero)
        {
           
            TutorialManager.Instance?.NotifyPlayerMoved();

            
            bool startingOnCarpet = GridManager.Instance != null && GridManager.Instance.IsHighFriction(CurrentCell);
            MoveDuration = startingOnCarpet ? highFrictionMoveDuration : baseMoveDuration;
            MoveCurve = startingOnCarpet ? highFrictionMoveCurve : baseMoveCurve;

            if (TryStep(dir, out var pushedWeight))
            {
                bool onHighFriction = GridManager.Instance != null && GridManager.Instance.IsHighFriction(CurrentCell);

                if (pushedWeight > 0f)
                {
                    // Tutorial task: mark "push" complete. No-op outside the Tutorial scene.
                    TutorialManager.Instance?.NotifyBlockPushed();

                    ChargePushEnergy(pushedWeight, onHighFriction);
                }
                else
                {
                    stepCounter++;

                    var threshold = onHighFriction ? stepsPerBarHighFriction : stepsPerBar;
                    if (stepCounter >= threshold)
                    {
                        ChargeBars(1);
                        stepCounter = 0;
                    }
                }
            }
        }

        UpdateAnimation(dir != Vector2Int.zero);
    }

    /// <summary>
    /// Weight-based energy cost of a push (walking into a block). Friction slows pushing too: the
    /// cost is scaled by the same ratio that governs
    /// plain-step friction (e.g. 3/2 = 1.5x by default), so the two stay derived from one pair
    /// of tunable numbers.
    /// </summary>
    private void ChargePushEnergy(float pushedWeight, bool onHighFriction)
    {
        var frictionMultiplier = onHighFriction ? (float)stepsPerBar / stepsPerBarHighFriction : 1f;
        ChargeBars(Mathf.Max(1, Mathf.RoundToInt(pushedWeight * energyCostPerWeight * frictionMultiplier)));
    }

    /// <summary>
    /// Every energy deduction goes through here, so the "close call" analytics check lives in one
    /// place. It has to run right after a deduction and only then: Bars can equal 1 only immediately
    /// after the single deduction that lands on it (any deduction FROM 1 goes straight to 0 / death).
    /// Checking after every step instead would re-log the same close call on each step spent at 1 bar.
    /// </summary>
    private void ChargeBars(int amount)
    {
        if (springManager == null || amount <= 0) return;

        springManager.ReduceBars(amount);

        if (springManager.Bars == 1)
        {
            AnalyticsLogger.Instance?.LogLastBar();
        }
    }

    /// <summary>Starts the stretch action (1 → 2 → 3): shows the half frame now, full after stretchFrameDuration.</summary>
    private void BeginStretch()
    {
        stretchStage = StretchHalf;
        stretchTarget = StretchFull;
        stretchTimer = 0f;
    }

    /// <summary>Ends a stretch instantly (no 3 → 2 → 1 contraction), e.g. when hit.</summary>
    private void CancelStretch()
    {
        stretchStage = StretchIdle;
        stretchTarget = StretchIdle;
        stretchTimer = 0f;
    }

    /// <summary>
    /// Advances the stretch one stage per stretchFrameDuration. A release only starts the contraction
    /// (3 → 2 → 1) once already at full, checked before advancing, so full always shows for at least
    /// one frame and one frame never both rises and contracts.
    /// </summary>
    private void TickStretch(bool held)
    {
        if (stretchStage == StretchFull && stretchTarget == StretchFull)
        {
            if (!held)
            {
                stretchStage = StretchHalf;
                stretchTarget = StretchIdle;
                stretchTimer = 0f;
            }
            return;
        }

        if (stretchStage == stretchTarget) return;

        stretchTimer += Time.deltaTime;
        if (stretchTimer < stretchFrameDuration) return;

        // Half is always one step from either target, so one step per frame never overshoots.
        stretchTimer = 0f;
        stretchStage += stretchTarget > stretchStage ? 1 : -1;
    }

    /// <summary>The held WASD direction (one at a time, A/D before W/S), or zero if none is held.</summary>
    private static Vector2Int ReadHeldDirection()
    {
        if (Input.GetKey(KeyCode.A)) return Vector2Int.left;
        if (Input.GetKey(KeyCode.D)) return Vector2Int.right;
        if (Input.GetKey(KeyCode.W)) return Vector2Int.up;
        if (Input.GetKey(KeyCode.S)) return Vector2Int.down;
        return Vector2Int.zero;
    }

    // Disabled: old press-to-toggle stretch (Space on/off). Replaced by hold-to-stretch (BeginStretch / TickStretch).
    // /// <summary>
    // /// Starts or ends a stretch. Starting costs energy up front and is refused mid-slide (the pose
    // /// would play over a character still travelling between cells); ending is always allowed.
    // /// </summary>
    // private void ToggleStretch()
    // {
    //     if (IsStretching)
    //     {
    //         IsStretching = false;
    //         return;
    //     }
    //
    //     if (IsMoving) return;
    //
    //     ChargeBars(stretchEnergyCost);
    //     IsStretching = true;
    // }

    // Disabled: WASD reach while stretched (turn + GridEntity.TryReach on the cell one level up).
    // Tall objects will be reached via stretch + attack instead.
    // /// <summary>
    // /// While stretched the direction keys don't walk, they reach: one press is one reach toward the
    // /// adjacent cell (GetKeyDown, unlike walking's held-key repeat, so holding a key can't re-shove
    // /// or re-trigger something every frame). Pressing a direction also turns the player to face it,
    // /// which is what selects the matching directional stretch pose in UpdateAnimation.
    // /// </summary>
    // private void HandleStretchInput()
    // {
    //     Vector2Int dir = Vector2Int.zero;
    //
    //     if (Input.GetKeyDown(KeyCode.A)) dir = Vector2Int.left;
    //     else if (Input.GetKeyDown(KeyCode.D)) dir = Vector2Int.right;
    //     else if (Input.GetKeyDown(KeyCode.W)) dir = Vector2Int.up;
    //     else if (Input.GetKeyDown(KeyCode.S)) dir = Vector2Int.down;
    //
    //     if (dir == Vector2Int.zero) return;
    //
    //     FacingDirection = dir;
    //
    //     if (TryReach(dir, stretchReach, out var pushedWeight) && pushedWeight > 0f)
    //     {
    //         var onHighFriction = GridManager.Instance != null && GridManager.Instance.IsHighFriction(CurrentCell);
    //         ChargePushEnergy(pushedWeight, onHighFriction);
    //     }
    // }

    /// <summary>
    /// Takes a hit from something harmful (see Roomba): thrown up to knockbackCells cells along
    /// knockbackDirection, charged energyLoss bars, and stunned for stunSeconds. Being hit ends a
    /// stretch. The player then flashes until the stun plus postHitImmunitySeconds are over, and
    /// can't be hit at all in that time. Returns false and does nothing while stunned or still
    /// immune — that's what stops a hazard that keeps coming back (or a second one) from chaining
    /// stuns together.
    /// </summary>
    public bool ReceiveHit(Vector2Int knockbackDirection, int knockbackCells, int energyLoss, float stunSeconds)
    {
        if (IsInvulnerable) return false;

        CancelStretch();
        stunTimer = stunSeconds;
        immunityTimer = stunSeconds + postHitImmunitySeconds;
        flasher.Flash(immunityTimer, hitFlashesPerSecond);

        ChargeBars(energyLoss);
        Knockback(knockbackDirection, knockbackCells, knockbackSlideSeconds, knockbackCurve);
        return true;
    }

    /// <summary>
    /// Picks the animator's Direction value, keyed off FacingDirection so idle keeps looking
    /// the way the player last moved/pressed. Walking values (0-3) play while a movement key
    /// is held — even if TryStep was refused (walking in place against a wall/door) — or while
    /// the last step's visual slide is still playing (GridEntity.IsMoving), so releasing a key
    /// mid-step doesn't cut the walk off early. Otherwise idle values (5/10/20/30).
    /// While stretching the idle value is kept; StretchVisual draws the stretch sprites over it.
    /// </summary>
    private void UpdateAnimation(bool hasInput)
    {
        // Stunned reads as idle even while the knockback slide is still moving the character.
        IsWalking = !IsStretching && !IsStunned && (hasInput || IsMoving);

        if (animator == null) return;

        int value;
        // Disabled: Animator Direction 40-43 for stretch poses. Stretch sprites are now drawn by StretchVisual.
        // if (IsStretching)
        // {
        //     if (FacingDirection == Vector2Int.left) value = 43;
        //     else if (FacingDirection == Vector2Int.right) value = 42;
        //     else if (FacingDirection == Vector2Int.up) value = 41;
        //     else value = 40;
        // }
        // else
        if (IsWalking)
        {
            if (FacingDirection == Vector2Int.left) value = 3;
            else if (FacingDirection == Vector2Int.right) value = 2;
            else if (FacingDirection == Vector2Int.up) value = 1;
            else value = 0;
        }
        else
        {
            if (FacingDirection == Vector2Int.left) value = 30;
            else if (FacingDirection == Vector2Int.right) value = 20;
            else if (FacingDirection == Vector2Int.up) value = 10;
            else value = 5;
        }

        animator.SetInteger("Direction", value);
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