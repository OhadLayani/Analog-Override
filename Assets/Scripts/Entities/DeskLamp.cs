using System;
using System.Collections;
using System.Collections.Generic;
using AnalogOverride.Combat;
using AnalogOverride.GridSystem;
using UnityEngine;

namespace AnalogOverride.Entities
{
    /// <summary>
    /// A desk lamp standing on a pushable table. It's a child of the table, so it slides with every
    /// push, and it never takes a grid cell of its own: the table is the occupant, the lamp just rides
    /// on it. A HeightOffset on the lamp makes it count as one level above the table's cell, so hitting
    /// it needs full stretch (see PlayerAttack.CanReachHeight).
    ///
    /// Attacking it plays its spin frames once, counter-clockwise, then it rests on the first frame
    /// again. Hits while it's spinning are ignored.
    ///
    /// Its collider is a small circle on the lamp's head, moved to each frame's Head Offset so it swings
    /// with the drawing; it's also the only thing the attack can hit. On every frame it checks what it
    /// now overlaps and reports each thing once per spin via SweptInto (never its own table or the player, and only things on the lamp's own level, see
    /// HeightOffset.LevelOf), and tells anything that implements ISweepable
    /// (e.g. a PushOffObject on a shelf). It never calls TakeDamage on what it sweeps (that would collect a Key).
    ///
    /// The collider should be a trigger: nothing on the grid moves by physics, and the player's attack
    /// finds triggers anyway. The sweep is a query, so no Rigidbody2D is needed.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class DeskLamp : MonoBehaviour, IAttackable
    {
        [Tooltip("The lamp's sprite. Its sprite is swapped frame by frame during the spin.")]
        [SerializeField] private SpriteRenderer lampRenderer;

        [Tooltip("The spin frames in order, counter-clockwise. Element 0 is also the resting frame it returns to after a spin.")]
        [SerializeField] private Sprite[] spinFrames;

        [Tooltip("Seconds each frame is shown during the spin.")]
        [Min(0.01f)]
        [SerializeField] private float frameDuration = 0.08f;

        [Tooltip("Where the head circle sits on each spin frame, in the lamp's local space (same order as Spin Frames). Select the lamp to see all of them as circles in the Scene view.")]
        [SerializeField] private Vector2[] headOffsets;

        [Header("Draw Order")]
        [Tooltip("The table's sprite. The lamp copies its Order in Layer plus Sorting Offset every frame, so it always draws on top of the table.")]
        [SerializeField] private SpriteRenderer sortAbove;

        [SerializeField] private int sortingOffset = 1;

        /// <summary>Fired once per collider per spin when the turning lamp overlaps it (never for the lamp, its table or the player).</summary>
        public event Action<Collider2D> SweptInto;

        private CircleCollider2D headCollider;
        private Transform ignoreRoot; // the table it stands on (or the lamp itself): the sweep skips everything under it
        private bool spinning;
        private readonly HashSet<Collider2D> sweptThisSpin = new HashSet<Collider2D>();
        private readonly List<Collider2D> overlaps = new List<Collider2D>();

        /// <summary>True from a hit until it's back on its resting frame.</summary>
        public bool IsSpinning => spinning;

        /// <summary>Always alive: the lamp can be hit any number of times.</summary>
        public bool IsAlive => true;

        private void Awake()
        {
            headCollider = GetComponent<CircleCollider2D>();

            var table = GetComponentInParent<GridEntity>();
            ignoreRoot = table != null ? table.transform : transform;
        }

        private void Start()
        {
            ShowFrame(0);
        }

        private void LateUpdate()
        {
            if (sortAbove != null && lampRenderer != null)
            {
                lampRenderer.sortingOrder = sortAbove.sortingOrder + sortingOffset;
            }
        }

        public void TakeDamage(int amount)
        {
            if (spinning) return;

            if (spinFrames == null || spinFrames.Length == 0)
            {
                Debug.LogWarning($"{name} has no Spin Frames assigned, so it can't spin.", this);
                return;
            }

            StartCoroutine(Spin());
        }

        private IEnumerator Spin()
        {
            spinning = true;
            sweptThisSpin.Clear();

            for (var i = 0; i < spinFrames.Length; i++)
            {
                ShowFrame(i);
                SweepCheck();
                yield return new WaitForSeconds(frameDuration);
            }

            ShowFrame(0);
            spinning = false;
        }

        private void OnDisable()
        {
            // Disabling stops the coroutine, so don't leave it stuck mid-spin.
            spinning = false;
        }

        /// <summary>Shows frame `index` and moves the head circle to that frame's head.</summary>
        private void ShowFrame(int index)
        {
            if (spinFrames == null || index >= spinFrames.Length) return;

            var sprite = spinFrames[index];
            if (sprite == null) return;

            if (lampRenderer != null) lampRenderer.sprite = sprite;

            // A missing entry keeps the circle where it was.
            if (headOffsets != null && index < headOffsets.Length)
            {
                headCollider.offset = headOffsets[index];
            }
        }

        /// <summary>Reports everything the collider overlaps on the current frame that wasn't already reported this spin.</summary>
        private void SweepCheck()
        {
            headCollider.Overlap(ContactFilter2D.noFilter, overlaps);

            // The levels simulate height in a flat 2D scene, so the head only reaches things on the
            // lamp's own level, not the floor under the shelf. Worked out per sweep: the table can be pushed.
            var manager = GridManager.Instance;
            var lampLevel = manager != null ? HeightOffset.LevelOf(manager, this) : 0;

            foreach (var hit in overlaps)
            {
                if (hit.transform.IsChildOf(ignoreRoot)) continue;
                if (hit.GetComponentInParent<CharacterController>() != null) continue;
                if (manager != null && HeightOffset.LevelOf(manager, hit) != lampLevel) continue;
                if (!sweptThisSpin.Add(hit)) continue;

                // Placeholder until something reacts to SweptInto (e.g. the key falling off the table).
                Debug.Log($"{name} swept into {hit.name}", this);
                SweptInto?.Invoke(hit);

                // Only things that opt in react; the lamp itself still never damages what it sweeps.
                hit.GetComponentInParent<ISweepable>()?.SweptBy(this);
            }
        }

        /// <summary>Draws every frame's head circle while the lamp is selected, to line them up with the art.</summary>
        private void OnDrawGizmosSelected()
        {
            if (headOffsets == null || !TryGetComponent<CircleCollider2D>(out var circle)) return;

            var scale = transform.lossyScale;
            var radius = circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));

            Gizmos.color = Color.yellow;
            foreach (var offset in headOffsets)
            {
                Gizmos.DrawWireSphere(transform.TransformPoint(offset), radius);
            }
        }
    }
}
