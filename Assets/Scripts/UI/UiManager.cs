using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AnalogOverride;

public class UiManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The parent RectTransform containing the HorizontalLayoutGroup")]
    [SerializeField] private Transform barsContainer;

    [Tooltip("The UI Image Prefab representing a single vertical bar")]
    [SerializeField] private GameObject barPrefab;

    [Header("Colors")]
    [SerializeField] private Color activeColor = Color.green;
    [SerializeField] private Color depletedColor = Color.black;

    // Cache the spawned images to avoid calling GetComponent later
    private List<Image> spawnedBars = new List<Image>();
    private SpringManager springManager;

    private void Start()
    {
        // Cache the reference on Start to avoid Update allocations
        springManager = SpringManager.Instance;

        if (springManager == null)
        {
            Debug.LogError("SpringManager instance not found in scene!", this);
            return;
        }

        // Spawn the initial UI based on the starting value
        InitializeBars(springManager.Bars);
        
        // Subscribe to the event so we only update when values actually change
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
        spawnedBars.Clear();

        for (int i = 0; i < totalBars; i++)
        {
            // THE FIX: Passing 'false' prevents Unity from crushing the UI scale
            GameObject newBar = Instantiate(barPrefab, barsContainer);
            
            if (newBar.TryGetComponent<Image>(out var barImage))
            {
                barImage.color = activeColor;
                spawnedBars.Add(barImage);
            }
        }
    }

    private void UpdateBarsUI(int currentBars)
    {
        // Loop through all our cached UI images
        for (int i = 0; i < spawnedBars.Count; i++)
        {
            // If the index is less than our current bars, it is active. Otherwise, it is lost.
            if (i < currentBars)
            {
                spawnedBars[i].color = activeColor;
            }
            else
            {
                spawnedBars[i].color = depletedColor;
            }
        }
    }
}