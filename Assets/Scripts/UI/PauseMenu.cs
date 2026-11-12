using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuUI;

    [Tooltip("Controls image + X button inside the pause menu. Optional; hidden on start.")]
    [SerializeField] private GameObject controlsPanel;

    private void Awake()
    {
        if (pauseMenuUI == null)
            Resume(); // Ensure the pause menu is hidden on start

        HideControls();
    }

    private void Update()
    {
        // The stage is over: Esc must not pause or unpause.
        if (StageOverScreen.Instance != null && StageOverScreen.Instance.IsShowing)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Esc on the controls screen closes only that screen; the game stays paused.
            if (controlsPanel != null && controlsPanel.activeSelf)
            {
                HideControls();
                return;
            }

            // Read the centralized state from GameManager
            if (GameManager.Instance.IsGamePaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Pause()
    {
        GameManager.Instance.SetPauseState(true);
        HideControls();
        pauseMenuUI.SetActive(true);
    }

    /// <summary>Button hook: shows the controls screen over the pause buttons.</summary>
    public void ShowControls()
    {
        if (controlsPanel != null)
            controlsPanel.SetActive(true);
    }

    /// <summary>Button hook (X): hides the controls screen.</summary>
    public void HideControls()
    {
        if (controlsPanel != null)
            controlsPanel.SetActive(false);
    }

    public void Resume()
    {
        // Tutorial task: closing a menu that was actually open counts. No-op outside the Tutorial scene.
        if (pauseMenuUI != null && pauseMenuUI.activeSelf)
            TutorialManager.Instance?.NotifyPauseMenuUsed();

        GameManager.Instance.SetPauseState(false);
        HideControls();
        pauseMenuUI.SetActive(false);
    }

    public void ResetLevel()
    {
        // Tutorial task: resetting from the menu also counts. No-op outside the Tutorial scene.
        // Must fire before GameManager.ResetLevel() reloads the scene below.
        TutorialManager.Instance?.NotifyPauseMenuUsed();

        GameManager.Instance?.ResetLevel();
    }

    /// <summary>Button hook: skips to the next level (or back to the first scene after the last).</summary>
    public void NextLevel()
    {
        GameManager.Instance?.SkipToNextScene();
    }

    /// <summary>Button hook: back to the main menu (pause menu and stage-over screen).</summary>
    public void ReturnToMainMenu()
    {
        GameManager.Instance?.LoadMainMenu();
    }

    public void QuitGame()
    {
        AnalyticsLogger.Instance?.LogQuit();

        // Cleanup time scale before exiting
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPauseState(false);
        }
        else
        {
            Time.timeScale = 1f;
        }

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}