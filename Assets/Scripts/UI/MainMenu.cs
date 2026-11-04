using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Main menu buttons: load a level, show/hide the controls screen, quit.</summary>
public class MainMenu : MonoBehaviour
{
    [Tooltip("Exact scene name loaded by the Play button (must be in Build Settings).")]
    [SerializeField] private string playSceneName;

    [Tooltip("Exact scene name loaded by the Tutorial button (must be in Build Settings).")]
    [SerializeField] private string tutorialSceneName;

    [Tooltip("The controls screen (image + X button). Hidden on start.")]
    [SerializeField] private GameObject controlsPanel;

    private void Awake()
    {
        // Safety: arriving here from a paused level must not leave time frozen.
        Time.timeScale = 1f;

        if (controlsPanel != null)
            controlsPanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && controlsPanel != null && controlsPanel.activeSelf)
            HideControls();
    }

    /// <summary>Button hook: starts the first level.</summary>
    public void Play() => LoadScene(playSceneName);

    /// <summary>Button hook: opens the tutorial.</summary>
    public void OpenTutorial() => LoadScene(tutorialSceneName);

    /// <summary>Button hook: shows the controls screen.</summary>
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

    /// <summary>Button hook: quits the game (stops Play mode in the Editor).</summary>
    public void QuitGame()
    {
        AnalyticsLogger.Instance?.LogQuit();

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Loads a scene from scratch: clears the progress GameManager keeps across scene loads.</summary>
    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"MainMenu: scene '{sceneName}' isn't in Build Settings (check the name in the Inspector).", this);
            return;
        }

        // GameManager survives scene loads (DontDestroyOnLoad), so keys and checkpoint from a previous run would carry over.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearCheckpoint();
            GameManager.Instance.ClearKeys();
            GameManager.Instance.SetPauseState(false);
        }
        RoomSnapshot.Instance?.Clear();

        SceneManager.LoadScene(sceneName);
    }
}
