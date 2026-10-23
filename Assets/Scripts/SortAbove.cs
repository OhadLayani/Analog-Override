using UnityEngine;

/// <summary>Keeps this object's sprite drawn just above another sprite (e.g. a key standing on a table), by copying its Order in Layer plus an offset every frame.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SortAbove : MonoBehaviour
{
    [Tooltip("The sprite to draw above, e.g. the furniture this object stands on.")]
    [SerializeField] private SpriteRenderer target;

    [Tooltip("How many steps above the target's Order in Layer.")]
    [SerializeField] private int offset = 1;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            spriteRenderer.sortingOrder = target.sortingOrder + offset;
        }
    }
}
