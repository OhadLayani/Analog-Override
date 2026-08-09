using System;
using UnityEngine;

namespace AnalogOverride
{
    /// <summary>
    /// Shared spring bar manager for the project.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class SpringManager : MonoBehaviour
    {
        public static SpringManager Instance { get; private set; }

        [SerializeField] private int bars = 20;

        // Event for game over state
        public event Action BarsReachedZero;
        
        // Event to notify the UI whenever the bar count changes
        public event Action<int> BarsChanged;

        public int Bars
        {
            get => bars;
            set => ReduceBars(value); 
        }

        private void Awake()
        {
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

            if (bars <= 0)
            {
                BarsReachedZero?.Invoke();
                return;
            }

            // Reduce the bars
            bars = Mathf.Max(0, bars - amountToSubtract);
            
            // Fire the event to update the UI with the new count
            BarsChanged?.Invoke(bars);

            if (bars <= 0)
            {
                bars = 0;
                BarsReachedZero?.Invoke();
            }
        }
    }
}