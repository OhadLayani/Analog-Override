using System;
using UnityEngine;



/// <summary>
/// Shared spring bar manager for the project.
/// </summary>
[DefaultExecutionOrder(-50)]
public class SpringManager : MonoBehaviour
{
    public static SpringManager Instance { get; private set; }

    public int bars;
    [SerializeField] private int maxBars = 20;

    // Event for game over state
    public event Action BarsReachedZero;

    // Event to notify the UI whenever the bar count changes
    public event Action<int> BarsChanged;

    public int Bars
    {
        get => bars;
        set => ReduceBars(value);
    }
    public int MaxBars => maxBars;

    private void Awake()
    {
        bars = maxBars; // Initialize bars to maxBars on Awake
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ReduceBars(int amountToSubtract)
    {
        if (amountToSubtract <= 0)
            return;

        // Already out: the death was announced when the bars first hit 0, and the scene reload it
        // triggers only lands at the end of the frame. Announcing it again for a second deduction in
        // that window would log the death (and start the reload) twice.
        if (bars <= 0) return;

        // Reduce the bars
        bars = Mathf.Max(0, bars - amountToSubtract);

        // Fire the event to update the UI with the new count
        BarsChanged?.Invoke(bars);

        // Every deduction passes through here (steps, pushes, stretches, hits, attacks), so this is the
        // one place that can't miss a close call. It must run right after a deduction and only then:
        // bars can equal 1 only immediately after the deduction that lands on it, whereas checking each
        // step would re-log the same close call for every step spent at 1 bar.
        if (bars == 1)
        {
            AnalyticsLogger.Instance?.LogLastBar();
        }

        if (bars <= 0)
        {
            bars = 0;
            BarsReachedZero?.Invoke();
        }
    }
    public void ResetBars()
    {
        bars = maxBars;
        BarsChanged?.Invoke(bars);
    }
}
