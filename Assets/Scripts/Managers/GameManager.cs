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

    // Checkpoints register themselves here when they are loaded
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

    // Call this when the player dies/runs out of bars
    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}

