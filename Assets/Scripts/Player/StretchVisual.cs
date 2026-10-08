using UnityEngine;

/// <summary>Draws the body's stretch sprites (half / full) over the Animator's output while the player is stretched.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class StretchVisual : MonoBehaviour
{
    [Header("Sprites (mode 2, mode 3)")]
    [Tooltip("Half and full stretch while facing down (toward the camera).")]
    [SerializeField] private Sprite[] frontFrames = new Sprite[2];

    [Tooltip("Half and full stretch while facing up (back to the camera).")]
    [SerializeField] private Sprite[] backFrames = new Sprite[2];

    [Tooltip("Half and full stretch in profile. Drawn facing RIGHT; flipped automatically for left.")]
    [SerializeField] private Sprite[] sideFrames = new Sprite[2];

    [Tooltip("Leave empty to find it on a parent.")]
    [SerializeField] private CharacterController player;

    private SpriteRenderer bodyRenderer;

    private void Awake()
    {
        bodyRenderer = GetComponent<SpriteRenderer>();
        if (player == null) player = GetComponentInParent<CharacterController>();
    }

    // LateUpdate runs after the Animator writes the sprite, so this override wins. At stage 0 the
    // Animator owns the sprite and flipX again (its states use Write Defaults).
    private void LateUpdate()
    {
        if (player == null) return;

        var stage = player.StretchStage;
        if (stage <= 0) return;

        var facing = player.FacingDirection;
        var frames = facing == Vector2Int.down ? frontFrames
            : facing == Vector2Int.up ? backFrames
            : sideFrames;

        // Element 0 = half stretch (stage 1), element 1 = full stretch (stage 2).
        var index = stage - 1;
        if (frames == null || index >= frames.Length || frames[index] == null) return; // missing frame: keep the Animator's sprite rather than show nothing
        var sprite = frames[index];

        bodyRenderer.sprite = sprite;
        bodyRenderer.flipX = facing.x < 0;
    }
}
