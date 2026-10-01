using UnityEngine;

/// <summary>Picks the idle arms sprite (front/back or profile) and offset from the player's facing and walking state.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ArmsVisual : MonoBehaviour
{
    [Header("Sprites")]
    [Tooltip("Arms shown while facing up or down (W/S).")]
    [SerializeField] private Sprite frontBackSprite;

    [Tooltip("Arms shown in profile (A/D). Same sprite for left and right.")]
    [SerializeField] private Sprite sideSprite;

    [Header("Placement")]
    [Tooltip("Local position of the arms while facing up or down.")]
    [SerializeField] private Vector2 frontBackOffset;

    [Tooltip("Local position of the arms while facing right. X is mirrored automatically for left.")]
    [SerializeField] private Vector2 sideOffset;

    [Tooltip("Extra Y added to Front Back Offset while the walk animation shows (the walk sprites are drawn higher).")]
    [SerializeField] private float walkFrontBackYOffset;

    [Tooltip("Extra Y added to Side Offset while the walk animation shows (the walk sprites are drawn higher).")]
    [SerializeField] private float walkSideYOffset;

    [Header("Draw Order")]
    [Tooltip("The player's body SpriteRenderer. The arms copy its sorting layer/order every frame.")]
    [SerializeField] private SpriteRenderer bodySprite;

    [Tooltip("Added to the body's Order in Layer while facing up or down (positive = in front of the body).")]
    [SerializeField] private int frontBackSortOffset = 1;

    [Tooltip("Added to the body's Order in Layer while in profile (positive = in front of the body).")]
    [SerializeField] private int sideSortOffset = 1;

    [Tooltip("Leave empty to find it on a parent.")]
    [SerializeField] private CharacterController player;

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

        armsRenderer.sprite = profile ? sideSprite : frontBackSprite;

        // Mirror the side offset by hand for left; the sprite itself needs no flip.
        var offset = profile ? new Vector2(facing.x < 0 ? -sideOffset.x : sideOffset.x, sideOffset.y) : frontBackOffset;
        if (player.IsWalking) offset.y += profile ? walkSideYOffset : walkFrontBackYOffset;
        transform.localPosition = new Vector3(offset.x, offset.y, transform.localPosition.z);

        // GridEntity rewrites the body's order on every move, so follow it each frame.
        if (bodySprite != null)
        {
            armsRenderer.sortingLayerID = bodySprite.sortingLayerID;
            armsRenderer.sortingOrder = bodySprite.sortingOrder + (profile ? sideSortOffset : frontBackSortOffset);
        }
    }
}
