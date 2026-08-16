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
    }

    private void OnDisable()
    {
        // Always unsubscribe from events to prevent memory leaks
        if (springManager != null)
        {
            springManager.BarsChanged -= UpdateBarsUI;
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