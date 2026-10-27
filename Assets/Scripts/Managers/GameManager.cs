using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-60)] // Runs before standard scripts, after GridManager
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Checkpoint Data")]
    public Vector2Int RespawnCell { get; private set; }
    public bool HasCheckpoint { get; private set; }

    // --- NEW PAUSE STATE ---
    public bool IsGamePaused { get; private set; }

    [Header("Keys")]
    // Keyed by keyId so multiple distinct key/Door pairs can coexist. Collecting a key
    // doesn't consume it - the same key opens every Door sharing its keyId, and stays
    // collected across a scene reload/respawn (persisted here via DontDestroyOnLoad,
    // same as checkpoint state).
    private readonly HashSet<string> collectedKeys = new HashSet<string>();

    private List<Checkpoint> allCheckpoints = new List<Checkpoint>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Ensures the GameManager and its saved checkpoint data survive scene reloads
        DontDestroyOnLoad(gameObject);

        // The saved room state has to survive reloads too, so it lives here rather than in the scene.
        if (!TryGetComponent<RoomSnapshot>(out _))
        {
            gameObject.AddComponent<RoomSnapshot>();
        }
    }

    public void RegisterCheckpoint(Checkpoint checkpoint)
    {
        if (!allCheckpoints.Contains(checkpoint))
            allCheckpoints.Add(checkpoint);
    }

    public void DeregisterCheckpoint(Checkpoint checkpoint)
    {
        if (allCheckpoints.Contains(checkpoint))
            allCheckpoints.Remove(checkpoint);
    }

    public void ActivateCheckpoint(Checkpoint activeCheckpoint, Vector2Int cell)
    {
        // Disabled: death now fully resets the level, so a checkpoint no longer saves a respawn cell.
        // HasCheckpoint = true;
        // RespawnCell = cell;

        // Deactivate all other checkpoints
        foreach (var cp in allCheckpoints)
        {
            if (cp != activeCheckpoint)
            {
                cp.SetActiveState(false);
            }
        }
    }

    /// <summary>
    /// Clears the saved checkpoint so the next reload/respawn starts the level from scratch
    /// instead of returning to the last activated checkpoint. Scene-local Checkpoint components
    /// reset themselves on scene reload; this only needs to clear the state GameManager itself
    /// persists across scenes via DontDestroyOnLoad.
    /// </summary>
    public void ClearCheckpoint()
    {
        HasCheckpoint = false;
    }

    public bool HasKey(string keyId) => collectedKeys.Contains(keyId);

    /// <summary>The ids of every key collected so far (see RoomSnapshot, which saves and restores this).</summary>
    public IReadOnlyCollection<string> CollectedKeys => collectedKeys;

    /// <summary>Replaces the collected keys with exactly `keyIds` — used to roll keys back to a room snapshot.</summary>
    public void ReplaceKeys(IEnumerable<string> keyIds)
    {
        collectedKeys.Clear();
        collectedKeys.UnionWith(keyIds);
    }

    public void CollectKey(string keyId)
    {
        collectedKeys.Add(keyId);
    }

    /// <summary>
    /// Forgets every collected key, so the next reload requires collecting them again
    /// instead of every Door with a matching keyId silently staying unlockable. Pair this
    /// with ClearCheckpoint() for a genuine "start over" reset — don't call it from a plain
    /// death respawn (ReloadScene alone), where keys should stay collected same as checkpoint progress.
    /// </summary>
    public void ClearKeys()
    {
        collectedKeys.Clear();
    }

    /// <summary>
    /// Full reset (not a death respawn): forgets checkpoint progress and collected keys,
    /// then reloads, so the level starts completely from scratch. Shared by PauseMenu and
    /// StageOverScreen so both "restart" actions stay in sync and get logged exactly once.
    /// </summary>
    public void ResetLevel()
    {
        AnalyticsLogger.Instance?.LogStageReset();
        RestartLevel();
    }

    /// <summary>Full reset without logging: used by ResetLevel and by a death (which logs DEATH itself).</summary>
    public void RestartLevel()
    {
        ClearCheckpoint();
        ClearKeys();
        RoomSnapshot.Instance?.Clear();
        ReloadScene();
    }

    /// <summary>
    /// Centralized method to handle pause state and time scaling.
    /// </summary>
    public void SetPauseState(bool isPaused)
    {
        IsGamePaused = isPaused;
        Time.timeScale = IsGamePaused ? 0f : 1f;
    }

    // Call this when the player dies/runs out of bars or clicks Restart
    public void ReloadScene()
    {
        // ALWAYS unpause and restore time before loading a scene to prevent a frozen reload
        SetPauseState(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Advances to the next scene in the Build Settings list, or loops back to the main
    /// menu (by name) once the last level has been completed.
    /// </summary>
    public void LoadNextScene()
    {
        // ALWAYS unpause and restore time before loading a scene to prevent a frozen reload
        SetPauseState(false);

        var currentScene = SceneManager.GetActiveScene();
        int nextIndex = currentScene.buildIndex + 1;

        // Logged before the load, so the line's own scene tag is the level being left.
        if (currentScene.name == "Tutorial")
        {
            AnalyticsLogger.Instance?.LogTutorialComplete();
        }

        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            var nextName = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(nextIndex));
            AnalyticsLogger.Instance?.LogLevelChange(currentScene.name, nextName);
            SceneManager.LoadScene(nextIndex);
        }
        else
        {
            // SceneManager.LoadScene("MainMenuScene");
            AnalyticsLogger.Instance?.LogLevelChange(currentScene.name, "Tutorial");
            SceneManager.LoadScene("Tutorial");
        }
    }
}