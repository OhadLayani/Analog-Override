using UnityEngine;
using UnityEngine.UI;

public class UiManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The root RectTransform that contains the start, the bars container, and the key (SpringItemsContainer)")]
    [SerializeField] private RectTransform rootLayoutRect;

    [Tooltip("The HorizontalLayoutGroup component on the SpringBarsContainer")]
    [SerializeField] private HorizontalLayoutGroup springLayoutGroup;

    [Tooltip("The UI Image Prefab representing a single middle coil of the spring")]
    [SerializeField] private GameObject barPrefab;

    [Header("Spring Tension Settings")]
    [Tooltip("Spacing when energy is full (spring is compressed/dense)")]
    [SerializeField] private float compressedSpacing = -7f;

    [Tooltip("Spacing when energy is empty (spring is released/loose)")]
    [SerializeField] private float releasedSpacing = -0.65f;

    [Header("Key UI")]
    [Tooltip("Must match the Key component's keyId this icon represents.")]
    [SerializeField] private string keyId = "default";

    [Tooltip("The key icon Image, shown once the matching key is collected. Should start inactive in the scene.")]
    [SerializeField] private GameObject keyIcon;

    private SpringManager springManager;
    
    // Cached to avoid GetComponent allocations during gameplay
    private RectTransform springBarsRect; 

    private void Start()
    {
        // Cache references
        springManager = SpringManager.Instance;
        springBarsRect = springLayoutGroup.GetComponent<RectTransform>();

        if (springManager == null)
        {
            Debug.LogError("SpringManager instance not found in scene!", this);
            return;
        }

        // Spawn the physical pieces of the spring based on the maximum capacity
        InitializeBars(springManager.MaxBars);
        
        // Set initial tension based on current bars
        UpdateBarsUI(springManager.Bars);

        // Subscribe to the event so we only update spacing when values change
        springManager.BarsChanged += UpdateBarsUI;

        // Subscribe to key pickups so the icon appears the moment it's collected
        Key.KeyCollected += OnKeyCollected;

        // Cover the respawn case: Key.KeyCollected only fires on an actual pickup, but a
        // key already collected before this scene loaded (returning to a checkpoint) makes
        // the Key self-destruct silently without firing it - so check persisted state directly.
        if (GameManager.Instance != null && GameManager.Instance.HasKey(keyId) && keyIcon != null)
        {
            keyIcon.SetActive(true);
        }
    }

    private void OnDisable()
    {
        // Always unsubscribe from events to prevent memory leaks
        if (springManager != null)
        {
            springManager.BarsChanged -= UpdateBarsUI;
        }

        Key.KeyCollected -= OnKeyCollected;
    }

    private void OnKeyCollected(string collectedKeyId)
    {
        if (collectedKeyId == keyId && keyIcon != null)
        {
            keyIcon.SetActive(true);
        }
    }

    private void InitializeBars(int totalBars)
    {
        // Clear any placeholder coils that might have been left in the editor
        foreach (Transform child in springLayoutGroup.transform)
        {
            Destroy(child.gameObject);
        }

        // Instantiate the coils into the layout group
        for (int i = 0; i < totalBars; i++)
        {
            Instantiate(barPrefab, springLayoutGroup.transform);
        }
    }

    private void UpdateBarsUI(int currentBars)
    {
        // Calculate the relative energy remaining from 0.0 to 1.0
        float energyPercentage = (float)currentBars / springManager.MaxBars;

        // Apply the new spacing
        springLayoutGroup.spacing = Mathf.Lerp(releasedSpacing, compressedSpacing, energyPercentage);

        // 1. Force the ContentSizeFitter on the coils container to recalculate its width immediately
        if (springBarsRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(springBarsRect);
        }
        
        // 2. Force the parent container to immediately reposition the key based on the new width
        if (rootLayoutRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootLayoutRect);
        }
    }
}