using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuUI;

    private void Awake()
    {
        if (pauseMenuUI == null)
            Resume(); // Ensure the pause menu is hidden on start
    }

    private void Update()
    {
        // The stage is over: Esc must not pause or unpause.
        if (StageOverScreen.Instance != null && StageOverScreen.Instance.IsShowing)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
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
        pauseMenuUI.SetActive(true);
    }

    public void Resume()
    {
        // Tutorial task: closing a menu that was actually open counts. No-op outside the Tutorial scene.
        if (pauseMenuUI != null && pauseMenuUI.activeSelf)
            TutorialManager.Instance?.NotifyPauseMenuUsed();

        GameManager.Instance.SetPauseState(false);
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