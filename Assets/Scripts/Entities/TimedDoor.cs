using System;
using System.Collections;
using System.Collections.Generic;
using AnalogOverride.GridSystem;
using UnityEngine;

namespace AnalogOverride.Entities
{
    /// <summary>
    /// A door that a WallSwitch opens for a short window. It sits next to a Furniture component,
    /// which makes its footprint solid while closed. Opening plays the frames (closed → open), then
    /// frees its cells; after Open Seconds it waits until nothing stands in its cells, blocks them
    /// again and plays the frames back to closed.
    ///
    /// Death and Reset Level reload the scene, so it always starts closed.
    /// </summary>
    [RequireComponent(typeof(Furniture))]
    public class TimedDoor : MonoBehaviour
    {
        [Tooltip("The door's sprite. Its sprite is swapped frame by frame while it opens and closes.")]
        [SerializeField] private SpriteRenderer doorRenderer;

        [Tooltip("The frames in order, closed to open. Element 0 is shown while closed; closing plays them backwards.")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("Seconds each frame is shown while opening or closing.")]
        [Min(0.01f)]
        [SerializeField] private float frameDuration = 0.08f;

        [Tooltip("Seconds it stays fully open before closing. If anything is still in its cells then, it waits until they're clear.")]
        [Min(0f)]
        [SerializeField] private float openSeconds = 2f;

        [Tooltip("The footprint box that makes the closed door solid. Turned off while open.")]
        [SerializeField] private BoxCollider2D footprint;

        private Furniture furniture;
        private readonly List<Vector2Int> doorCells = new List<Vector2Int>();

        /// <summary>True from the moment it starts opening until it has fully closed again.</summary>
        public bool IsBusy { get; private set; }

        /// <summary>Raised once the door has fully closed again.</summary>
        public event Action Closed;

        private void Awake()
        {
            furniture = GetComponent<Furniture>();
        }

        private void Start()
        {
            ShowFrame(0);
        }

        /// <summary>Opens the door for its window. Returns false (and does nothing) if it's already opening, open or closing.</summary>
        public bool OpenBriefly()
        {
            if (IsBusy) return false;

            IsBusy = true;
            StartCoroutine(Cycle());
            return true;
        }

        private IEnumerator Cycle()
        {
            var lastFrame = frames != null ? frames.Length - 1 : 0;

            for (var i = 1; i <= lastFrame; i++)
            {
                yield return new WaitForSeconds(frameDuration);
                ShowFrame(i);
            }

            // Copied before disabling, since Furniture.OnDisable clears its list.
            doorCells.Clear();
            doorCells.AddRange(furniture.BlockedCells);

            if (footprint != null) footprint.enabled = false;
            furniture.enabled = false;

            yield return new WaitForSeconds(openSeconds);
            yield return new WaitUntil(CellsAreClear);

            // Solid again before the closing frames, so nothing slips in while they play.
            if (footprint != null) footprint.enabled = true;
            furniture.enabled = true;
            furniture.RefreshFootprint();

            for (var i = lastFrame - 1; i >= 0; i--)
            {
                yield return new WaitForSeconds(frameDuration);
                ShowFrame(i);
            }

            IsBusy = false;
            Closed?.Invoke();
        }

        /// <summary>True when no grid occupant (player, crate, Roomba) stands in any of the door's cells.</summary>
        private bool CellsAreClear()
        {
            var manager = GridManager.Instance;
            if (manager == null) return true;

            foreach (var cell in doorCells)
            {
                if (manager.GetOccupant(cell) != null) return false;
            }

            return true;
        }

        private void ShowFrame(int index)
        {
            if (doorRenderer == null || frames == null || index < 0 || index >= frames.Length) return;
            if (frames[index] != null) doorRenderer.sprite = frames[index];
        }
    }
}
