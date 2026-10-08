using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows the UI spring's key on the same frame as the player's key (SpringKeyAnimator), using the larger UI frames.</summary>
[RequireComponent(typeof(Image))]
public class UiSpringKeyAnimator : MonoBehaviour
{
    private const int FramesPerLoop = 4;

    [Tooltip("UI key frames (positions 1-4), in the same order as the player's side frames.")]
    [SerializeField] private Sprite[] frames = new Sprite[FramesPerLoop];

    [Tooltip("The player's key animator to mirror. Leave empty to find it in the scene.")]
    [SerializeField] private SpringKeyAnimator source;

    private Image keyImage;

    private void Awake()
    {
        keyImage = GetComponent<Image>();
    }

    private void Start()
    {
        if (source == null) source = FindFirstObjectByType<SpringKeyAnimator>();
        if (source == null) Debug.LogWarning("UiSpringKeyAnimator found no SpringKeyAnimator in the scene.", this);
    }

    // LateUpdate, so the player's key has already advanced its frame in Update this frame.
    private void LateUpdate()
    {
        if (source == null) return;

        var frame = source.CurrentFrame;
        if (frames == null || frame >= frames.Length || frames[frame] == null) return; // missing frame: keep the current sprite

        keyImage.sprite = frames[frame];
    }
}
