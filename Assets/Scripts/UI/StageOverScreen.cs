using UnityEngine;

/// <summary>
/// Shows the "stage complete" panel and pauses the game (reusing GameManager's existing
/// pause state - CharacterController already stops responding to input while paused, no
/// separate "level finished" flag needed). Triggered by StageGoal walking the player onto it.
/// </summary>
public class StageOverScreen : MonoBehaviour
{
    public static StageOverScreen Instance { get; private set; }

    [SerializeField] private GameObject stageOverUI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (stageOverUI != null)
            stageOverUI.SetActive(false); // hidden until Show() is called
    }

    public void Show()
    {
        if (stageOverUI != null)
            stageOverUI.SetActive(true);

        if (GameManager.Instance != null)
            GameManager.Instance.SetPauseState(true);
    }

    // Wire this to a "Restart" button on the panel.
    public void RestartLevel()
    {
        if (GameManager.Instance == null) return;

        // Full reset, matching PauseMenu.ResetLevel(): forget checkpoint progress and
        // collected keys so restarting the stage starts it completely from scratch.
        GameManager.Instance.ClearCheckpoint();
        GameManager.Instance.ClearKeys();
        // GameManager handles unpausing internally now
        GameManager.Instance.ReloadScene();
    }

    // Wire this to a "Next Level" button on the panel, if/when you add one.
    public void NextLevel()
    {
        GameManager.Instance?.LoadNextScene();
    }
}
