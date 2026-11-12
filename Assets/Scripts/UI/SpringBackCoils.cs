using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws a back coil in each gap between the HUD spring's front coils, shown once the spring has expanded enough.
/// </summary>
public class SpringBackCoils : MonoBehaviour
{
    [Tooltip("The SpringBarsContainer that holds the front coils spawned by UiManager.")]
    [SerializeField] private RectTransform springBarsContainer;

    [Tooltip("UI Image prefab of a single back coil.")]
    [SerializeField] private GameObject backPrefab;

    [Tooltip("Expansion (0 = full energy, 1 = empty) from which the back coils are visible.")]
    [Range(0f, 1f)]
    [SerializeField] private float visibleFromExpansion = 0.25f;

    [Tooltip("Shifts every back coil from the centre of its gap, in this object's local units.")]
    [SerializeField] private Vector2 backOffset = Vector2.zero;

    private readonly List<Transform> backs = new List<Transform>();
    private SpringManager springManager;
    private bool visible = true;

    private void Start()
    {
        springManager = SpringManager.Instance;
        if (springManager == null || springBarsContainer == null || backPrefab == null) return;

        // One back coil per gap between neighbouring front coils
        for (int i = 0; i < springManager.MaxBars - 1; i++)
        {
            backs.Add(Instantiate(backPrefab, transform).transform);
        }

        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (springManager == null || backs.Count == 0) return;

        float expansion = 1f - (float)springManager.Bars / springManager.MaxBars;
        SetVisible(expansion >= visibleFromExpansion);
        if (!visible) return;

        // Runs after UiManager has rebuilt the layout, so the front coils are already in place
        int gaps = Mathf.Min(backs.Count, springBarsContainer.childCount - 1);
        Vector3 offset = transform.TransformVector(backOffset);
        for (int i = 0; i < gaps; i++)
        {
            Vector3 left = springBarsContainer.GetChild(i).position;
            Vector3 right = springBarsContainer.GetChild(i + 1).position;
            backs[i].position = (left + right) * 0.5f + offset;
        }
    }

    /// <summary>Shows or hides all back coils, only touching them when the state changes.</summary>
    private void SetVisible(bool show)
    {
        if (show == visible) return;
        visible = show;
        foreach (Transform back in backs) back.gameObject.SetActive(show);
    }
}
