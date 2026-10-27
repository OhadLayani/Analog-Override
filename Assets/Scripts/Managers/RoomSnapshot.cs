using System.Collections.Generic;
using AnalogOverride.GridSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Saves how the room is at a checkpoint and puts it back after a death. Dying still reloads the
/// scene (so everything starts from its authored state); this then moves things back to where they
/// were when the player last touched a checkpoint: pushables and roombas in their cells, doors open
/// or shut, shelf objects fallen, and the collected keys rolled back to what they were then, so a key
/// picked up after the checkpoint is lying where it was again.
///
/// Lives on the GameManager object (added by it), so it survives scene reloads. Capture() overwrites
/// the previous snapshot; Clear() drops it (full level reset). A snapshot is only ever applied to the
/// scene it was taken in, so loading another level discards it.
///
/// Objects are matched by their name and place in the scene hierarchy, recorded when the scene loads
/// (before anything can be destroyed or spawned), so two objects that share a name need to keep
/// their order. Only those objects take part: things created at runtime aren't saved, and a saveable
/// re-creates what it needs itself (see PushOffObject).
/// </summary>
[DefaultExecutionOrder(-1000)] // Its Update must come before every other script's, see Update()
public class RoomSnapshot : MonoBehaviour
{
    public static RoomSnapshot Instance { get; private set; }

    private const string CellSuffix = "|cell";
    private const string StateSuffix = "|state";

    // Every object that can be saved, with its hierarchy id, as recorded when the scene loaded.
    private readonly Dictionary<Component, string> ids = new Dictionary<Component, string>();

    private readonly Dictionary<string, string> states = new Dictionary<string, string>();
    private readonly List<string> keys = new List<string>();
    private int sceneIndex = -1; // -1 = nothing saved
    private bool restorePending;

    public bool HasSnapshot => sceneIndex >= 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // The GameManager is created while the first scene is loading, so that load's own event can
        // arrive before or without this: name that scene's objects now (they all exist by Awake time).
        RecordIds();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    /// <summary>Saves the room as it is right now, replacing any earlier snapshot.</summary>
    public void Capture()
    {
        var game = GameManager.Instance;
        if (game == null) return;

        if (ids.Count == 0) RecordIds(); // safety net in case the scene's load event was missed

        states.Clear();
        keys.Clear();
        keys.AddRange(game.CollectedKeys);
        sceneIndex = SceneManager.GetActiveScene().buildIndex;

        foreach (var pair in ids)
        {
            var component = pair.Key;
            if (component == null) continue; // destroyed since the scene loaded

            // The logical cell, not the Transform: a slide may still be playing.
            if (component is GridEntity entity && entity.IsMovable && entity.isActiveAndEnabled)
            {
                states[pair.Value + CellSuffix] = entity.CurrentCell.x + "," + entity.CurrentCell.y;
            }

            if (component is ISaveable saveable)
            {
                states[pair.Value + StateSuffix] = saveable.CaptureState();
            }
        }
    }

    /// <summary>Forgets the snapshot, so the next reload starts the room from scratch.</summary>
    public void Clear()
    {
        states.Clear();
        keys.Clear();
        sceneIndex = -1;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        RecordIds();

        if (!HasSnapshot) return;

        if (scene.buildIndex != sceneIndex)
        {
            Clear(); // a different level: the old room means nothing here
            return;
        }

        // Right now, before any Start() runs, so a Key that was collected after the checkpoint doesn't
        // see itself as collected and remove itself, and the key icon starts out right.
        GameManager.Instance.ReplaceKeys(keys);

        restorePending = true;
    }

    // Runs ahead of every other Update (see the execution order above): by the first frame every Start
    // has run, but nothing has moved yet. Restoring any later could land after a roomba has already
    // begun a step, leaving its slide heading for the old cell.
    private void Update()
    {
        if (!restorePending) return;

        restorePending = false;
        Restore();
    }

    /// <summary>Names every saveable object by its place in the hierarchy, while the scene is still exactly as authored (before anything is destroyed, spawned or renamed).</summary>
    private void RecordIds()
    {
        ids.Clear();

        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var isSaveable = behaviour is ISaveable;
            var isMovableEntity = behaviour is GridEntity entity && entity.IsMovable;
            if (!isSaveable && !isMovableEntity) continue;

            ids[behaviour] = HierarchyId(behaviour.transform);
        }
    }

    // Names, with a rank among same-named siblings to tell apart e.g. several "Crate"s. Not plain sibling
    // indices: those shift whenever a sibling elsewhere in the list is removed, and the GameManager moving
    // itself out of the scene is enough to throw every root object's index off by one between loads.
    private static string HierarchyId(Transform transform)
    {
        var id = "";
        for (var t = transform; t != null; t = t.parent)
        {
            id = "/" + t.name + "#" + SameNameRank(t) + id;
        }

        return id;
    }

    private static int SameNameRank(Transform t)
    {
        var rank = 0;

        if (t.parent != null)
        {
            foreach (Transform sibling in t.parent)
            {
                if (sibling == t) break;
                if (sibling.name == t.name) rank++;
            }
        }
        else
        {
            foreach (var root in t.gameObject.scene.GetRootGameObjects())
            {
                if (root.transform == t) break;
                if (root.name == t.name) rank++;
            }
        }

        return rank;
    }

    private void Restore()
    {
        var movers = new List<GridEntity>();
        var cells = new List<Vector2Int>();
        var saveables = new List<ISaveable>();
        var saved = new List<string>();

        foreach (var pair in ids)
        {
            var component = pair.Key;
            if (component == null) continue;

            if (component is GridEntity entity && states.TryGetValue(pair.Value + CellSuffix, out var cellText))
            {
                var parts = cellText.Split(',');
                movers.Add(entity);
                cells.Add(new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1])));
            }

            if (component is ISaveable saveable && states.TryGetValue(pair.Value + StateSuffix, out var state))
            {
                saveables.Add(saveable);
                saved.Add(state);
            }
        }

        // Lift all before placing any: two objects may have swapped places since the scene loaded, and
        // each target cell is still held by the other until both are off the grid.
        foreach (var mover in movers) mover.LiftFromGrid();
        for (var i = 0; i < movers.Count; i++) movers[i].PlaceAtCell(cells[i]);

        for (var i = 0; i < saveables.Count; i++) saveables[i].RestoreState(saved[i]);
    }
}
