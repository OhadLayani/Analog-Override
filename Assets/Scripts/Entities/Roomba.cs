using System.Collections;
using System.Collections.Generic;
using AnalogOverride.GridSystem;
using UnityEngine;

namespace AnalogOverride.Entities
{
    /// <summary>
    /// A slow autonomous hazard. It rolls from cell to cell at a constant speed without stopping at
    /// each one (unlike the player, who hops a cell at a time). It's a GridEntity, so it follows the
    /// same rules as everything else: it blocks other things, can't change height level, and — unlike
    /// the player — never PUSHES anything.
    ///
    /// How it moves depends on how it's set up:
    ///   - WANDER (no patrol points, the default): it drives straight until something blocks it, visibly
    ///     bumps into it, bounces back, then spins to face a new direction and carries on.
    ///   - PATROL (patrol points assigned): it follows those points strictly, in order, looping back to
    ///     the first, taking the shortest way round anything in the road.
    ///   - RUSH: whenever the docking station is attacked (see RoombaButton) it drops what it's doing
    ///     and hurries to the station at a faster speed. On arrival it faces the station and sits
    ///     flashing for a couple of seconds, then goes back to whichever of the above it was doing.
    ///
    /// The player is a wall as far as its movement goes: running into them is just a bump. The bump is
    /// also what hurts — it throws the player back, costs them energy and stuns them (see
    /// CharacterController.ReceiveHit) — but the roomba isn't affected by it and just carries on. The
    /// player can't be hit again while still flashing from the last one, which is what stops a roomba
    /// that keeps coming back from stunlocking them.
    ///
    /// The bump and the turn are purely cosmetic: the grid never sees the roomba leave its cell
    /// (CurrentCell and occupancy don't change), they just play out on the Transform.
    /// </summary>
    public class Roomba : GridEntity, IInteractable
    {
        private static readonly Vector2Int[] Headings =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
        };

        [Header("Movement")]
        [Tooltip("Extra seconds it waits between finishing one cell and starting the next. Keep this at 0 for the smooth, continuous roll — any value above it makes the roomba stop at every cell. Speed is set by Move Duration (seconds per cell); raise that to slow it down.")]
        [Min(0f)]
        [SerializeField] private float pauseBetweenSteps = 0f;

        [Tooltip("Which way it first heads off when wandering. Cardinal directions only (a diagonal is snapped to its stronger axis).")]
        [SerializeField] private Vector2Int startDirection = Vector2Int.right;

        [Tooltip("Optional patrol route. Leave EMPTY and it wanders, bumping into things and turning (the default). Fill it and it follows these points strictly, in order, looping back to the first, going the shortest way round anything in the road. Put an empty GameObject on each floor tile you want it to visit and drag them in, in order. A point it can't reach right now is skipped for that lap.")]
        [SerializeField] private Transform[] patrolPoints;

        [Header("Bumping and turning")]
        [Tooltip("How far it lunges into whatever blocks it, as a fraction of a cell, before bouncing back. 0 = no visible bump.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float bumpDistance = 0.3f;

        [Tooltip("Seconds the whole bump takes (lunge in, bounce back).")]
        [Min(0.05f)]
        [SerializeField] private float bumpSeconds = 0.3f;

        [Tooltip("How fast it spins to face the way it's heading, in degrees per second. 270 does a quarter turn in about a third of a second.")]
        [Min(1f)]
        [SerializeField] private float turnDegreesPerSecond = 270f;

        [Tooltip("Which way the artwork points when it isn't rotated: 90 = up, 0 = right, 180 = left, -90 = down. The roomba image has its front at the top, so 90 — change this if it ends up facing sideways.")]
        [SerializeField] private float artFacingDegrees = 90f;

        [Header("Hitting the player")]
        [Tooltip("How many cells the player is thrown back. They stop short of anything in the way, so this is a maximum.")]
        [Min(0)]
        [SerializeField] private int knockbackCells = 2;

        [Tooltip("Energy bars the player loses per hit.")]
        [Min(0)]
        [SerializeField] private int energyDamage = 2;

        [Tooltip("Seconds the player is stunned.")]
        [Min(0f)]
        [SerializeField] private float stunSeconds = 1f;

        [Header("Responding to the docking station")]
        [Tooltip("Seconds per cell while rushing to the station — lower is faster. Compare with Move Duration, its normal speed.")]
        [Min(0.05f)]
        [SerializeField] private float rushMoveDuration = 0.35f;

        [Tooltip("How fast it spins while rushing, in degrees per second. Higher than the normal turn speed so it can take corners at pace.")]
        [Min(1f)]
        [SerializeField] private float rushTurnDegreesPerSecond = 720f;

        [Tooltip("Seconds it sits flashing at the station after arriving, before going back to normal. It can't hurt anyone while it does.")]
        [Min(0f)]
        [SerializeField] private float dockedSeconds = 2.5f;

        [Header("Flashing")]
        [Tooltip("How many times per second it flashes while docked.")]
        [Min(0.1f)]
        [SerializeField] private float flashesPerSecond = 3f;

        [Tooltip("How faint it gets at the dim end of a flash (0 = invisible, 1 = no flash).")]
        [Range(0f, 1f)]
        [SerializeField] private float flashMinAlpha = 0.25f;

        private Vector2Int heading;
        private float facingAngle;
        private float baseMoveDuration;
        private float pauseTimer;
        private float dockedTimer;
        private bool busy; // true while a bump is playing out, so no new step starts over it
        private bool rushing;
        private Vector2Int dockCell;
        private readonly List<Vector2Int> patrolCells = new List<Vector2Int>();
        private int patrolIndex;
        private SpriteFlasher flasher;

        /// <summary>True while docked: still, flashing, and harmless to touch.</summary>
        public bool IsFrozen => dockedTimer > 0f;

        /// <summary>The direction it's currently driving in.</summary>
        public Vector2Int Heading => heading;

        // Reset() only sets inspector defaults when this component is first added (or via the
        // right-click Reset menu). A roomba should roll slowly, not hop at GridEntity's brisk default.
        private void Reset()
        {
            MoveDuration = 1f;
        }

        protected override void Start()
        {
            base.Start();

            // GridEntity eases every slide in and out, which on its own makes a mover pulse to a near-stop
            // at each cell. A straight line keeps the speed constant right through a run of cells. Set here
            // rather than in the Inspector so it holds even on an object that never had Reset() run.
            MoveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            baseMoveDuration = MoveDuration;

            heading = CardinalOf(startDirection, Vector2Int.right);

            // Start already facing the way it's going, rather than spinning round on the first frame.
            facingAngle = AngleFor(heading);
            transform.rotation = Quaternion.Euler(0f, 0f, facingAngle);

            if (!TryGetComponent(out flasher))
            {
                flasher = gameObject.AddComponent<SpriteFlasher>();
            }

            foreach (var point in patrolPoints)
            {
                if (point != null)
                {
                    patrolCells.Add(Manager.WorldToCell(point.position));
                }
            }
        }

        /// <summary>
        /// The docking station was hit: drop everything and rush to it. Ignored if already on the way or
        /// already docked, so hitting it repeatedly can't stretch the flashing out.
        /// </summary>
        public void CallTo(Vector2Int stationCell)
        {
            if (rushing || IsFrozen) return;

            rushing = true;
            dockCell = stationCell;
        }

        /// <summary>The player walked (or reached) into the roomba. Hurts them unless it's docked.</summary>
        public void Interact(GridEntity source)
        {
            if (IsFrozen) return;

            if (source is CharacterController player)
            {
                HitPlayer(player, CardinalOf(player.CurrentCell - CurrentCell, heading));
            }
        }

        private void Update()
        {
            TurnTowardHeading();

            if (IsFrozen)
            {
                dockedTimer -= Time.deltaTime;
                return;
            }

            if (busy || IsMoving) return;

            pauseTimer -= Time.deltaTime;
            if (pauseTimer > 0f) return;

            pauseTimer = pauseBetweenSteps;
            Advance();
        }

        /// <summary>Spins the sprite toward the current heading every frame, so turning never has to hold up movement — a corner on a route is taken while still rolling.</summary>
        private void TurnTowardHeading()
        {
            var target = AngleFor(heading);
            if (Mathf.Abs(Mathf.DeltaAngle(facingAngle, target)) < 0.01f) return;

            var speed = rushing ? rushTurnDegreesPerSecond : turnDegreesPerSecond;
            facingAngle = Mathf.MoveTowardsAngle(facingAngle, target, speed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, facingAngle);
        }

        /// <summary>One decision per tick, depending on what it's currently doing.</summary>
        private void Advance()
        {
            MoveDuration = rushing ? rushMoveDuration : baseMoveDuration;

            if (rushing)
            {
                AdvanceRush();
            }
            else if (patrolCells.Count > 0)
            {
                AdvancePatrol();
            }
            else
            {
                StepOrBump(heading, true);
            }
        }

        private void AdvanceRush()
        {
            if (IsAdjacent(CurrentCell, dockCell))
            {
                // Arrived: face the station and sit there flashing.
                rushing = false;
                heading = CardinalOf(dockCell - CurrentCell, heading);
                Dock(dockedSeconds);
                return;
            }

            if (TryFindFirstStep(cell => IsAdjacent(cell, dockCell), out var step))
            {
                StepOrBump(step, false);
            }
            else
            {
                // Walled off or boxed in right now: give up on it and carry on as normal.
                rushing = false;
            }
        }

        private void AdvancePatrol()
        {
            // Walk the route from the current point onward, skipping any point that's already been
            // reached or can't be got to right now, rather than hanging on it.
            for (var tried = 0; tried < patrolCells.Count; tried++)
            {
                var goal = patrolCells[patrolIndex];

                if (CurrentCell == goal)
                {
                    patrolIndex = (patrolIndex + 1) % patrolCells.Count;
                    continue;
                }

                if (TryFindFirstStep(cell => cell == goal, out var step))
                {
                    StepOrBump(step, false);
                    return;
                }

                patrolIndex = (patrolIndex + 1) % patrolCells.Count;
            }
        }

        /// <summary>
        /// Heads one cell in `direction` if it can. If something is in the way — the player included,
        /// they count as a wall — it bumps into it instead. `turnAfterBump` is for wandering, where a bump
        /// means choosing a new direction; on a route there's nothing to choose, so it just tries again.
        /// </summary>
        private void StepOrBump(Vector2Int direction, bool turnAfterBump)
        {
            heading = direction; // face where it's about to go; TurnTowardHeading swings the sprite round

            var target = CurrentCell + direction;
            var player = Manager.GetOccupant(target) as CharacterController;

            // CanEnter first: TryStep would happily push a crate or poke a door, and a roomba shouldn't.
            if (player == null && CanEnter(target) && TryStep(direction)) return;

            StartCoroutine(BumpAndTurn(player, turnAfterBump));
        }

        /// <summary>
        /// Lunges into the blocked cell and bounces back, then (when wandering) picks a new heading and
        /// waits for the sprite to swing round to it. If the thing in the way is the player, the hit lands
        /// at the moment of impact, so they're thrown back as the roomba touches them. The roomba itself
        /// is unaffected and carries straight on.
        /// </summary>
        private IEnumerator BumpAndTurn(CharacterController player, bool turnAfterBump)
        {
            busy = true;

            var home = Manager.CellToWorld(CurrentCell);
            var lunge = home + (Manager.CellToWorld(CurrentCell + heading) - home) * bumpDistance;

            yield return Glide(home, lunge, bumpSeconds * 0.5f);

            if (player != null && !IsFrozen)
            {
                HitPlayer(player, heading);
            }

            yield return Glide(lunge, home, bumpSeconds * 0.5f);

            if (turnAfterBump && !IsFrozen && TurnAway())
            {
                while (!IsFacingHeading())
                {
                    yield return null;
                }
            }

            busy = false;
        }

        private void HitPlayer(CharacterController player, Vector2Int knockbackDirection)
        {
            player.ReceiveHit(knockbackDirection, knockbackCells, energyDamage, stunSeconds);
        }

        /// <summary>Sits still and flashing for `seconds`. Never shortens a dock already running.</summary>
        private void Dock(float seconds)
        {
            if (seconds <= 0f) return;

            dockedTimer = Mathf.Max(dockedTimer, seconds);
            flasher.Flash(seconds, flashesPerSecond, flashMinAlpha);
        }

        /// <summary>
        /// Picks a new heading: the first open one found, scanning the four directions from a random
        /// starting point and skipping the way it was already going. Returns false (keeping its heading,
        /// so it just bumps again next tick) if it's boxed in on all sides.
        /// </summary>
        private bool TurnAway()
        {
            var start = Random.Range(0, Headings.Length);

            for (var i = 0; i < Headings.Length; i++)
            {
                var candidate = Headings[(start + i) % Headings.Length];
                if (candidate == heading) continue;

                if (CanEnter(CurrentCell + candidate))
                {
                    heading = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>An empty, walkable cell on this roomba's own level — the only kind it will drive into.</summary>
        private bool CanEnter(Vector2Int cell)
        {
            return Manager.IsFree(cell) && Manager.GetHeight(cell) == Manager.GetHeight(CurrentCell);
        }

        /// <summary>
        /// What route planning treats as open road: walkable, on this level, and not occupied by anything
        /// solid. The player is deliberately NOT solid here — a route goes through them, so the roomba
        /// carries on along it and bumps them if they're still in the way, instead of detouring round them
        /// or giving up and waiting for them to move.
        /// </summary>
        private bool CanPlan(Vector2Int cell, int level)
        {
            if (!Manager.IsWalkable(cell) || Manager.GetHeight(cell) != level) return false;

            var occupant = Manager.GetOccupant(cell);
            return occupant == null || occupant is CharacterController;
        }

        /// <summary>
        /// Breadth-first search outward from the current cell for the nearest cell matching `isGoal`.
        /// Returns the direction of the FIRST step along the shortest route, or false if there's no way
        /// there (or it's already standing on a goal). The grid is small, and this only runs once per
        /// cell travelled, so there's nothing to optimise.
        /// </summary>
        private bool TryFindFirstStep(System.Func<Vector2Int, bool> isGoal, out Vector2Int step)
        {
            step = Vector2Int.zero;

            var start = CurrentCell;
            var level = Manager.GetHeight(start);
            var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
            var frontier = new Queue<Vector2Int>();
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();

                if (cell != start && isGoal(cell))
                {
                    // Walk back along the route to the cell right after the start — that's the first step.
                    while (cameFrom[cell] != start)
                    {
                        cell = cameFrom[cell];
                    }

                    step = cell - start;
                    return true;
                }

                foreach (var direction in Headings)
                {
                    var next = cell + direction;
                    if (cameFrom.ContainsKey(next) || !CanPlan(next, level)) continue;

                    cameFrom[next] = cell;
                    frontier.Enqueue(next);
                }
            }

            return false;
        }

        private static bool IsAdjacent(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
        }

        /// <summary>The Z rotation that makes the artwork's front point along `direction`.</summary>
        private float AngleFor(Vector2Int direction)
        {
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - artFacingDegrees;
        }

        private bool IsFacingHeading()
        {
            return Mathf.Abs(Mathf.DeltaAngle(facingAngle, AngleFor(heading))) < 1f;
        }

        /// <summary>Slides the Transform between two world positions, easing in and out. Cosmetic only — the roomba's grid cell never changes during a bump.</summary>
        private IEnumerator Glide(Vector3 from, Vector3 to, float seconds)
        {
            var t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds)));
                yield return null;
            }

            transform.position = to;
        }
    }
}
