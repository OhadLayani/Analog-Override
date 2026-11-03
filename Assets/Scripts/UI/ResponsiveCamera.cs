using UnityEngine;
using Unity.Cinemachine;

/// <summary>Zooms the camera out on screens narrower than the reference aspect so the full reference width stays visible.</summary>
[RequireComponent(typeof(Camera))]
public class ResponsiveCamera : MonoBehaviour
{
    [Header("Reference ratio - where the game looks correct")]
    [SerializeField] private float referenceWidth = 16f;
    [SerializeField] private float referenceHeight = 9f;

    [Header("Camera")]
    [Tooltip("Optional. If set, its lens is changed (the Brain overrides the plain Camera). If empty, this GameObject's Camera is changed.")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Tooltip("Orthographic Size that looks correct at the reference ratio.")]
    [SerializeField] private float referenceOrthographicSize = 4.54679f;

    [Header("Overview (zoom out)")]
    [Tooltip("Optional. The fixed zoom-out camera. Centred on Overview Bounds and sized to fit it on the current screen.")]
    [SerializeField] private CinemachineCamera overviewCamera;

    [Tooltip("A renderer covering the whole room (e.g. the walls TilemapRenderer). The overview fits its bounds.")]
    [SerializeField] private Renderer overviewBounds;

    [Tooltip("Extra space around the room in overview, as a share of its size.")]
    [Min(0f)]
    [SerializeField] private float overviewMargin = 0.05f;

    private Camera cam;
    private int lastScreenWidth;
    private int lastScreenHeight;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        ApplySize();
    }

    private void LateUpdate()
    {
        if (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight)
            return;

        ApplySize();
    }

    private void ApplySize()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        float referenceAspect = referenceWidth / referenceHeight;
        float currentAspect = (float)Screen.width / Screen.height;
        float size = referenceOrthographicSize * Mathf.Max(1f, referenceAspect / currentAspect);

        if (cinemachineCamera != null)
        {
            LensSettings lens = cinemachineCamera.Lens;
            lens.OrthographicSize = size;
            cinemachineCamera.Lens = lens;
        }
        else
        {
            cam.orthographicSize = size;
        }

        ApplyOverview(currentAspect);
    }

    /// <summary>Centres the overview camera on the room and sizes it so the whole room fits at this aspect.</summary>
    private void ApplyOverview(float currentAspect)
    {
        if (overviewCamera == null || overviewBounds == null)
            return;

        Bounds room = overviewBounds.bounds;
        Vector3 pos = overviewCamera.transform.position;
        overviewCamera.transform.position = new Vector3(room.center.x, room.center.y, pos.z);

        LensSettings lens = overviewCamera.Lens;
        lens.OrthographicSize = Mathf.Max(room.extents.y, room.extents.x / currentAspect) * (1f + overviewMargin);
        overviewCamera.Lens = lens;
    }

    [ContextMenu("Capture Current Size")]
    private void CaptureCurrentSize()
    {
        referenceOrthographicSize = cinemachineCamera != null
            ? cinemachineCamera.Lens.OrthographicSize
            : GetComponent<Camera>().orthographicSize;
    }
}
