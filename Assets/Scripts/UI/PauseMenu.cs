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
        GameManager.Instance.SetPauseState(false);
        pauseMenuUI.SetActive(false);
    }

    public void ResetLevel()
    {
        GameManager.Instance?.ResetLevel();
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