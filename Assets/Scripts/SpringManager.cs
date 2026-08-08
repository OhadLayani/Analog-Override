using System;
using UnityEngine;

namespace AnalogOverride
{
    /// <summary>
    /// Shared spring bar manager for the project.
    /// </summary>
    public class SpringManager : MonoBehaviour
    {
        // Singleton instance
        public static SpringManager Instance { get; private set; }

        // Removing 'static' allows this to appear in the Unity Inspector
        [SerializeField] private int bars = 20;

        // The event is now instance-based
        public event Action BarsReachedZero;

        public int Bars
        {
            get => bars;
            set => ReduceBars(value);
        }

        private void Awake()
        {
            // Standard Singleton enforcement
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

            bars = Mathf.Max(0, bars - amountToSubtract);

            if (bars <= 0)
            {
                bars = 0;
                BarsReachedZero?.Invoke();
            }
        }
    }
}