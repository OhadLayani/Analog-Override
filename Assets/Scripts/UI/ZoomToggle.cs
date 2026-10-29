using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>HUD button that switches between the follow camera and the fixed room overview, swapping its icon.</summary>
public class ZoomToggle : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;

    [Tooltip("The fixed overview CinemachineCamera. Its Priority must be higher than the follow camera's.")]
    [SerializeField] private CinemachineCamera overviewCamera;

    [Tooltip("Shown while zoomed in (click to zoom out).")]
    [SerializeField] private Sprite zoomOutSprite;

    [Tooltip("Shown while zoomed out (click to zoom back in).")]
    [SerializeField] private Sprite zoomInSprite;

    private bool isZoomedOut;

    private void Awake()
    {
        if (button != null)
            button.onClick.AddListener(Toggle);

        // Every load and reset starts zoomed in.
        SetZoomedOut(false);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(Toggle);
    }

    private void Toggle()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGamePaused)
            return;

        SetZoomedOut(!isZoomedOut);

        if (isZoomedOut)
            AnalyticsLogger.Instance?.LogZoomOut();
    }

    private void SetZoomedOut(bool zoomedOut)
    {
        isZoomedOut = zoomedOut;

        if (overviewCamera != null)
            overviewCamera.enabled = zoomedOut;

        if (icon != null)
            icon.sprite = zoomedOut ? zoomInSprite : zoomOutSprite;
    }
}
