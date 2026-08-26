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
        HasCheckpoint = true;
        RespawnCell = cell;

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

    public void CollectKey(string keyId)
    {
        collectedKeys.Add(keyId);
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

        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextIndex);
        }
        else
        {
            // SceneManager.LoadScene("MainMenuScene");
            SceneManager.LoadScene("Tutorial");
        }
    }
}