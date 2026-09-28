using UnityEngine;

/// <summary>Frame-steps the wind-up key sprite from the player's energy state (bars spent + step progress).</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpringKeyAnimator : MonoBehaviour
{
    private const int FramesPerLoop = 4;

    [Header("Sprites (positions 1-4, in order)")]
    [Tooltip("Key frames shown while the player faces up (W).")]
    [SerializeField] private Sprite[] backFrames = new Sprite[FramesPerLoop];

    [Tooltip("Key frames shown in profile (A/D). Drawn facing RIGHT; flipped automatically for left.")]
    [SerializeField] private Sprite[] sideFrames = new Sprite[FramesPerLoop];

    [Header("Placement")]
    [Tooltip("Local position of the key while facing up.")]
    [SerializeField] private Vector2 backOffset;

    [Tooltip("Local position of the key while facing right. X is mirrored automatically for left.")]
    [SerializeField] private Vector2 sideOffset;

    [Header("Draw Order")]
    [Tooltip("The player's body SpriteRenderer. The key copies its sorting layer/order every frame.")]
    [SerializeField] private SpriteRenderer bodySprite;

    [Tooltip("Added to the body's Order in Layer while facing up (positive = in front of the body).")]
    [SerializeField] private int backSortOffset = 1;

    [Tooltip("Added to the body's Order in Layer while in profile (positive = in front of the body).")]
    [SerializeField] private int sideSortOffset = 1;

    [Header("Pace")]
    [Tooltip("Frames per second for normal turning (a step).")]
    [Min(0.1f)]
    [SerializeField] private float framesPerSecond = 10f;

    [Tooltip("Frames per second for a big burst (push/attack costing several bars).")]
    [Min(0.1f)]
    [SerializeField] private float fastFramesPerSecond = 24f;

    [Tooltip("A backlog bigger than this many frames switches to the fast speed until caught up.")]
    [Min(0)]
    [SerializeField] private int fastBacklogFrames = FramesPerLoop;

    [Tooltip("Leave empty to find it on a parent.")]
    [SerializeField] private CharacterController player;

    private SpriteRenderer keyRenderer;
    private SpringManager springManager;

    // All frame counters only ever grow; the visible position is shownFrame % FramesPerLoop.
    private int loops;
    private int targetFrame;
    private int shownFrame;
    private int lastBars;
    private int lastPhase;
    private float frameTimer;
    private bool fastMode;

    private void Awake()
    {
        keyRenderer = GetComponent<SpriteRenderer>();
        if (player == null) player = GetComponentInParent<CharacterController>();
    }

    private void Start()
    {
        springManager = SpringManager.Instance;
        if (springManager != null) lastBars = springManager.Bars;
        lastPhase = CurrentPhase();
    }

    private void Update()
    {
        if (springManager == null || player == null) return;

        UpdateTarget();
        AdvanceShownFrame();
    }

    private void LateUpdate()
    {
        if (player == null) return;

        var facing = player.FacingDirection;

        // Facing the camera: the key is behind the robot.
        if (facing == Vector2Int.down)
        {
            keyRenderer.enabled = false;
            return;
        }

        keyRenderer.enabled = true;

        var frame = shownFrame % FramesPerLoop;
        var facingUp = facing == Vector2Int.up;
        var facingLeft = facing == Vector2Int.left;

        keyRenderer.sprite = GetFrame(facingUp ? backFrames : sideFrames, frame);
        keyRenderer.flipX = facingLeft;

        // flipX only mirrors around the pivot, so the side offset is mirrored by hand.
        var offset = facingUp ? backOffset : new Vector2(facingLeft ? -sideOffset.x : sideOffset.x, sideOffset.y);
        transform.localPosition = new Vector3(offset.x, offset.y, transform.localPosition.z);

        // GridEntity rewrites the body's order on every move, so follow it each frame.
        if (bodySprite != null)
        {
            keyRenderer.sortingLayerID = bodySprite.sortingLayerID;
            keyRenderer.sortingOrder = bodySprite.sortingOrder + (facingUp ? backSortOffset : sideSortOffset);
        }
    }

    /// <summary>Moves targetFrame forward from the latest bars/step-progress state.</summary>
    private void UpdateTarget()
    {
        var bars = springManager.Bars;
        var phase = CurrentPhase();

        if (bars < lastBars)
        {
            // Each bar spent = one full loop (step cost closes the loop; push/attack keep the position).
            loops += lastBars - bars;
        }
        else if ((bars > lastBars || phase < lastPhase) && lastPhase > 0)
        {
            // Checkpoint refill / step counter reset: finish the current loop so bar losses stay on loop ends.
            loops++;
        }

        lastBars = bars;
        lastPhase = phase;

        targetFrame = Mathf.Max(targetFrame, loops * FramesPerLoop + phase);
    }

    /// <summary>Steps shownFrame toward targetFrame one frame at a time at a steady pace.</summary>
    private void AdvanceShownFrame()
    {
        if (shownFrame >= targetFrame)
        {
            fastMode = false;
            frameTimer = 0f; // next advance shows immediately
            return;
        }

        if (targetFrame - shownFrame > fastBacklogFrames) fastMode = true;

        frameTimer -= Time.deltaTime; // Time.timeScale = 0 while paused freezes the key too
        if (frameTimer > 0f) return;

        shownFrame++;
        frameTimer += 1f / (fastMode ? fastFramesPerSecond : framesPerSecond);
    }

    private int CurrentPhase()
    {
        return player != null ? Mathf.Clamp(Mathf.FloorToInt(player.StepProgress * FramesPerLoop), 0, FramesPerLoop - 1) : 0;
    }

    private static Sprite GetFrame(Sprite[] frames, int index)
    {
        return frames != null && index < frames.Length ? frames[index] : null;
    }
}
