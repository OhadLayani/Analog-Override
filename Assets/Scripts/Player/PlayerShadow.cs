using UnityEngine;

/// <summary>Shadow under the player: loops its frames while walking and freezes on the current frame when stopped.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerShadow : MonoBehaviour
{
    [Header("Animation")]
    [Tooltip("Shadow frames, looped in order while the player walks.")]
    [SerializeField] private Sprite[] frames = new Sprite[3];

    [Tooltip("Frames per second while walking (same on floor and carpet).")]
    [Min(0.1f)]
    [SerializeField] private float framesPerSecond = 8f;

    [Header("Placement")]
    [Tooltip("Extra Y added while the walk animation shows, if the feet are drawn higher when walking.")]
    [SerializeField] private float walkYOffset;

    [Tooltip("Extra Y added while the body is stretched (Space): element 0 = half stretch, element 1 = full stretch.")]
    [SerializeField] private float[] bodyStretchYOffsets = new float[2];

    [Header("Draw Order")]
    [Tooltip("The player's body SpriteRenderer. The shadow copies its sorting layer/order every frame.")]
    [SerializeField] private SpriteRenderer bodySprite;

    [Tooltip("Added to the body's Order in Layer (negative = behind the body).")]
    [SerializeField] private int sortOffset = -1;

    [Tooltip("Leave empty to find it on a parent.")]
    [SerializeField] private CharacterController player;

    private SpriteRenderer shadowRenderer;
    private float baseY;
    private int frameIndex;
    private float frameTimer;

    private void Awake()
    {
        shadowRenderer = GetComponent<SpriteRenderer>();
        if (player == null) player = GetComponentInParent<CharacterController>();
        baseY = transform.localPosition.y;
    }

    private void Update()
    {
        if (player == null || !player.IsWalking || frames == null || frames.Length == 0) return;

        // Time.deltaTime is 0 while paused, so the loop freezes then too.
        frameTimer += Time.deltaTime;
        var frameSeconds = 1f / framesPerSecond;
        while (frameTimer >= frameSeconds)
        {
            frameTimer -= frameSeconds;
            frameIndex = (frameIndex + 1) % frames.Length;
        }
    }

    private void LateUpdate()
    {
        if (frames != null && frameIndex < frames.Length) shadowRenderer.sprite = frames[frameIndex];

        if (player != null)
        {
            var y = baseY;
            if (player.IsWalking) y += walkYOffset;
            var stage = player.StretchStage;
            if (stage > 0 && bodyStretchYOffsets != null && stage <= bodyStretchYOffsets.Length) y += bodyStretchYOffsets[stage - 1];
            transform.localPosition = new Vector3(transform.localPosition.x, y, transform.localPosition.z);
        }

        // GridEntity rewrites the body's order on every move, so follow it each frame.
        if (bodySprite != null)
        {
            shadowRenderer.sortingLayerID = bodySprite.sortingLayerID;
            shadowRenderer.sortingOrder = bodySprite.sortingOrder + sortOffset;
        }
    }
}
