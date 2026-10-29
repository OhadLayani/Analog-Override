using System.Collections;
using AnalogOverride.Combat;
using UnityEngine;

namespace AnalogOverride.Entities
{
    /// <summary>
    /// A fence that opens when the player's attack hits it. It sits next to a Furniture component,
    /// which makes its footprint solid. A hit plays the open frames once (closed → middle → open);
    /// after the last frame the closed footprint box is swapped for the smaller open one and
    /// Furniture recalculates its blocked cells, so the freed cells become walkable.
    ///
    /// One-shot: once it starts opening it can't be hit again, and it never closes. Death and
    /// Reset Level reload the scene, so it starts closed again.
    ///
    /// Both footprint boxes are trigger BoxCollider2Ds on this GameObject. To preview the open box's
    /// red squares in the Scene view, tick it on temporarily; Awake turns it off again in play.
    /// </summary>
    [RequireComponent(typeof(Furniture))]
    public class Fence : MonoBehaviour, IAttackable
    {
        [Tooltip("The fence's sprite. Its sprite is swapped frame by frame while it opens.")]
        [SerializeField] private SpriteRenderer fenceRenderer;

        [Tooltip("The open frames in order: closed, middle, open. Element 0 is shown until it's hit; the last one stays once it's open.")]
        [SerializeField] private Sprite[] openFrames;

        [Tooltip("Seconds each frame is shown while opening.")]
        [Min(0.01f)]
        [SerializeField] private float frameDuration = 0.08f;

        [Tooltip("The footprint box while closed (the whole fence).")]
        [SerializeField] private BoxCollider2D closedFootprint;

        [Tooltip("The footprint box once open (only the cells the open fence still blocks).")]
        [SerializeField] private BoxCollider2D openFootprint;

        private Furniture furniture;
        private bool opened; // true from the hit on, so further swings ignore it

        /// <summary>Hittable only while closed.</summary>
        public bool IsAlive => !opened;

        private void Awake()
        {
            furniture = GetComponent<Furniture>();

            // In Awake, before Furniture.Start collects the footprint, so only the closed box counts.
            if (closedFootprint != null) closedFootprint.enabled = true;
            if (openFootprint != null) openFootprint.enabled = false;
        }

        private void Start()
        {
            ShowFrame(0);
        }

        public void TakeDamage(int amount)
        {
            if (opened) return;

            opened = true;
            AnalyticsLogger.Instance?.LogInteraction(name, "opened");
            StartCoroutine(Open());
        }

        private IEnumerator Open()
        {
            var frameCount = openFrames != null ? openFrames.Length : 0;
            for (var i = 1; i < frameCount; i++)
            {
                yield return new WaitForSeconds(frameDuration);
                ShowFrame(i);
            }

            if (closedFootprint != null) closedFootprint.enabled = false;
            if (openFootprint != null) openFootprint.enabled = true;
            furniture.RefreshFootprint();
        }

        private void ShowFrame(int index)
        {
            if (fenceRenderer == null || openFrames == null || index >= openFrames.Length) return;
            if (openFrames[index] != null) fenceRenderer.sprite = openFrames[index];
        }
    }
}
