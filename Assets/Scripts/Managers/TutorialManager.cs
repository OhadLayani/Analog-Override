using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the Tutorial scene's task checklist: activates a check image next to a note once
/// its action has been performed (in any order), and enables the Start button only once
/// every task is complete. Scene-scoped singleton — only exists in the Tutorial scene, so
/// every other script that notifies it (CharacterController, Checkpoint, PauseMenu) does so
/// via Instance?.NotifyX(), since those scripts also run in scenes with no TutorialManager.
///
/// Progress is stored in STATIC fields, not instance fields: resetting via the pause menu
/// reloads the scene, destroying and recreating this component like everything else in it.
/// Static fields survive that (no domain reload happens on a runtime scene load) — Start()
/// re-applies them to the freshly recreated check images so a reset never undoes progress.
/// </summary>
[DefaultExecutionOrder(-40)] // After SpringManager (-50), before default (0) — consistent with the project's singleton ordering convention.
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Task Checkmarks")]
    [Tooltip("Check image shown once the player has pressed a movement key (W/A/S/D).")]
    [SerializeField] private GameObject moveCheck;

    [Tooltip("Check image shown once the player has stood on the checkpoint.")]
    [SerializeField] private GameObject checkpointCheck;

    [Tooltip("Check image shown once the player has reset the level via the pause menu.")]
    [SerializeField] private GameObject resetCheck;

    [Tooltip("Check image shown once the player has pushed the crate.")]
    [SerializeField] private GameObject pushCheck;

    [Header("Start Button")]
    [Tooltip("Disabled until every task above is complete.")]
    [SerializeField] private Button startButton;

    // See the class doc comment for why these are static rather than instance fields.
    private static bool hasMoved;
    private static bool hasVisitedCheckpoint;
    private static bool hasReset;
    private static bool hasPushed;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Re-apply whatever progress already exists (e.g. after a pause-menu reset) to this
        // freshly instantiated scene's check images and Start button, rather than assuming
        // a blank slate.
        ApplyCheckState(moveCheck, hasMoved);
        ApplyCheckState(checkpointCheck, hasVisitedCheckpoint);
        ApplyCheckState(resetCheck, hasReset);
        ApplyCheckState(pushCheck, hasPushed);
        UpdateStartButton();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Call when the player presses a movement key (W/A/S/D) — see CharacterController.Update.</summary>
    public void NotifyPlayerMoved() => CompleteTask(ref hasMoved, moveCheck);

    /// <summary>Call when the player stands on the checkpoint — see Checkpoint.OnTriggerEnter2D.</summary>
    public void NotifyCheckpointVisited() => CompleteTask(ref hasVisitedCheckpoint, checkpointCheck);

    /// <summary>Call when the player resets the level via the pause menu — see PauseMenu.ResetLevel.</summary>
    public void NotifyLevelReset() => CompleteTask(ref hasReset, resetCheck);

    /// <summary>Call when the player successfully pushes the crate — see CharacterController.Update.</summary>
    public void NotifyBlockPushed() => CompleteTask(ref hasPushed, pushCheck);

    /// <summary>Idempotent: only the first call for a given task actually does anything, so callers don't need to guard against notifying more than once.</summary>
    private void CompleteTask(ref bool flag, GameObject check)
    {
        if (flag) return;

        flag = true;
        ApplyCheckState(check, true);
        UpdateStartButton();
    }

    private void ApplyCheckState(GameObject check, bool state)
    {
        if (check != null) check.SetActive(state);
    }

    private void UpdateStartButton()
    {
        if (startButton != null)
            startButton.interactable = hasMoved && hasVisitedCheckpoint && hasReset && hasPushed;
    }
}
