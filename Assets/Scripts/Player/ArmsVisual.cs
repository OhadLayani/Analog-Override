using UnityEngine;

/// <summary>Picks the arms sprite (front/back or profile, by stretch stage) and offset from the player's facing and walking state, and fits the attack hitbox to it.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ArmsVisual : MonoBehaviour
{
    [Header("Sprites")]
    [Tooltip("Arms shown while facing up or down (W/S). Also stretch stage 1.")]
    [SerializeField] private Sprite frontBackSprite;

    [Tooltip("Arms shown in profile (A/D). Same sprite for left and right. Also stretch stage 1.")]
    [SerializeField] private Sprite sideSprite;

    [Tooltip("Stretch stages 2 and 3 (in order) while facing up or down.")]
    [SerializeField] private Sprite[] frontBackStretchFrames = new Sprite[2];

    [Tooltip("Stretch stages 2 and 3 (in order) in profile.")]
    [SerializeField] private Sprite[] sideStretchFrames = new Sprite[2];

    [Header("Placement")]
    [Tooltip("Local position of the arms while facing up or down.")]
    [SerializeField] private Vector2 frontBackOffset;

    [Tooltip("Local position of the arms while facing right. X is mirrored automatically for left.")]
    [SerializeField] private Vector2 sideOffset;

    [Tooltip("Extra Y added to Front Back Offset while the walk animation shows (the walk sprites are drawn higher).")]
    [SerializeField] private float walkFrontBackYOffset;

    [Tooltip("Extra Y added to Side Offset while the walk animation shows (the walk sprites are drawn higher).")]
    [SerializeField] private float walkSideYOffset;

    [Tooltip("Local scale of the arms while facing up or down. Overrides the Transform's scale.")]
    [SerializeField] private Vector2 frontBackScale = Vector2.one;

    [Tooltip("Local scale of the arms in profile. Overrides the Transform's scale.")]
    [SerializeField] private Vector2 sideScale = Vector2.one;

    [Header("Draw Order")]
    [Tooltip("The player's body SpriteRenderer. The arms copy its sorting layer/order every frame.")]
    [SerializeField] private SpriteRenderer bodySprite;

    [Tooltip("Added to the body's Order in Layer while facing up or down (positive = in front of the body).")]
    [SerializeField] private int frontBackSortOffset = 1;

    [Tooltip("Added to the body's Order in Layer while in profile (positive = in front of the body).")]
    [SerializeField] private int sideSortOffset = 1;

    [Header("Hitbox")]
    [Tooltip("Optional. Resized every frame to match the drawn arms sprite. Should be a child of this object at local position 0.")]
    [SerializeField] private BoxCollider2D hitbox;

    [Tooltip("Leave empty to find it on a parent.")]
    [SerializeField] private CharacterController player;

    /// <summary>Stretch stage: 0 = idle (stage 1), 1 = stage 2, 2 = stage 3 (full stretch).</summary>
    public int Stage { get; set; }

    private SpriteRenderer armsRenderer;

    private void Awake()
    {
        armsRenderer = GetComponent<SpriteRenderer>();
        if (player == null) player = GetComponentInParent<CharacterController>();
    }

    private void LateUpdate()
    {
        if (player == null) return;

        var facing = player.FacingDirection;
        var profile = facing.x != 0;

        armsRenderer.sprite = GetSprite(profile);

        // Mirror the side offset by hand for left; the sprite itself needs no flip.
        var offset = profile ? new Vector2(facing.x < 0 ? -sideOffset.x : sideOffset.x, sideOffset.y) : frontBackOffset;
        if (player.IsWalking) offset.y += profile ? walkSideYOffset : walkFrontBackYOffset;
        transform.localPosition = new Vector3(offset.x, offset.y, transform.localPosition.z);

        var scale = profile ? sideScale : frontBackScale;
        transform.localScale = new Vector3(scale.x, scale.y, transform.localScale.z);

        // GridEntity rewrites the body's order on every move, so follow it each frame.
        if (bodySprite != null)
        {
            armsRenderer.sortingLayerID = bodySprite.sortingLayerID;
            armsRenderer.sortingOrder = bodySprite.sortingOrder + (profile ? sideSortOffset : frontBackSortOffset);
        }

        FitHitbox();
    }

    /// <summary>Returns the sprite for the current stage, falling back to the idle sprite if a stretch frame is missing.</summary>
    private Sprite GetSprite(bool profile)
    {
        var idle = profile ? sideSprite : frontBackSprite;
        if (Stage <= 0) return idle;

        var frames = profile ? sideStretchFrames : frontBackStretchFrames;
        var index = Stage - 1;
        return frames != null && index < frames.Length && frames[index] != null ? frames[index] : idle;
    }

    /// <summary>Sizes the hitbox to the sprite as drawn on screen, whatever scale sits on this object, its parents or the hitbox itself.</summary>
    private void FitHitbox()
    {
        if (hitbox == null || armsRenderer.sprite == null) return;

        var bounds = armsRenderer.sprite.bounds;
        var armsScale = transform.lossyScale;
        var hitboxScale = hitbox.transform.lossyScale;
        if (Mathf.Approximately(hitboxScale.x, 0f) || Mathf.Approximately(hitboxScale.y, 0f)) return;

        hitbox.size = new Vector2(
            Mathf.Abs(bounds.size.x * armsScale.x / hitboxScale.x),
            Mathf.Abs(bounds.size.y * armsScale.y / hitboxScale.y));
        hitbox.offset = new Vector2(
            bounds.center.x * armsScale.x / hitboxScale.x,
            bounds.center.y * armsScale.y / hitboxScale.y);
    }
}
