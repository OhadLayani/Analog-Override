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
}