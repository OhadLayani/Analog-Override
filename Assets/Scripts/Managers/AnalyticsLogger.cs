using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Appends one line per tracked gameplay event to a plain text log, for offline analysis
/// (e.g. counting how many DEATH lines there are, or when things happened relative to each
/// other in a session), plus a per-run summary block tallying each event type — so "how many
/// times did Run 1 die/reset/complete" is readable at a glance instead of needing to count
/// raw lines by hand. Deliberately minimal: writes happen only on the handful of discrete
/// events below — never per-frame or per-step — so a simple synchronous File.AppendAllText
/// is more than cheap enough. No batching, no async I/O, no re-reading the file to compute
/// the summary — the per-run tallies are just a few in-memory ints kept in sync with each
/// Log call, written out once at the end of the run.
///
/// The log lives in an "Analog Override" folder created next to the game (in a build, the folder
/// the game's executable is in; in the Editor, the project folder), and both the folder and the
/// file are created automatically on first write. Existing lines are never touched, so data
/// accumulates across every play session instead of resetting each time the game launches. If
/// that location isn't writable (e.g. a build installed under Program Files) logging is skipped
/// with a warning rather than breaking the game.
///
/// A "run" is a game launch that actually logged something: the run number is only taken at the
/// first event, and a launch with no events writes no summary, so opening and closing the game
/// (or pressing Play in the Editor to look around) doesn't use up run numbers or add empty blocks.
///
/// Self-bootstrapping: no GameObject needs to be added to any scene — see Bootstrap below.
/// It creates and persists itself the moment the game starts, so it can't be forgotten
/// when setting up a new scene.
/// </summary>
public class AnalyticsLogger : MonoBehaviour
{
    public static AnalyticsLogger Instance { get; private set; }

    // Persisted via PlayerPrefs (not the log file itself) so it keeps incrementing across
    // separate game launches/builds without ever needing to parse old log lines back out.
    private const string RunNumberPrefKey = "AnalyticsLogger.RunNumber";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject(nameof(AnalyticsLogger));
        DontDestroyOnLoad(go);
        go.AddComponent<AnalyticsLogger>();
    }

    private const string LogFolderName = "Analog Override";

    // Application.dataPath is the "<game>_Data" folder in a build and "<project>/Assets" in the Editor,
    // so its parent is the folder holding the game's executable, or the project folder.
    private string FolderPath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, LogFolderName);

    private string FilePath => Path.Combine(FolderPath, "analytics_log.txt");

    // 0 until the first event of this launch (see StartRunIfNeeded).
    private int runNumber;
    private bool summaryWritten;

    // Per-run tallies. Only ever reset by a fresh game launch (a new AnalyticsLogger
    // instance) — never mid-run — so the end-of-run summary always covers everything
    // since this run started, regardless of how many scene reloads happened in between.
    private int deathCount;
    private int checkpointVisitCount;
    private int resetCount;
    private int lastBarCount;
    private int stageCompleteCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>Takes this launch's run number the first time something is logged, so launches with no events don't use one up.</summary>
    private void StartRunIfNeeded()
    {
        if (runNumber != 0) return;

        runNumber = PlayerPrefs.GetInt(RunNumberPrefKey, 0) + 1;
        PlayerPrefs.SetInt(RunNumberPrefKey, runNumber);
        PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        // Safety net for whenever the game closes WITHOUT going through the in-game Quit
        // button (e.g. the OS window close button, or stopping Play mode in the Editor) —
        // WriteRunSummary() is idempotent (summaryWritten guard), so this never double-writes
        // alongside the summary LogQuit() below already triggers for a normal in-game quit.
        WriteRunSummary();
    }

    // מספר פעמים שניפסל (אפס אנרגיה)
    public void LogDeath()
    {
        deathCount++;
        Log("DEATH");
    }

    // מספר פעמים שהלך לתחנת טעינה
    public void LogCheckpointVisit()
    {
        checkpointVisitCount++;
        Log("CHECKPOINT_VISIT");
    }

    // מספר פעמים שלחץ איפוס שלב
    public void LogStageReset()
    {
        resetCount++;
        Log("STAGE_RESET");
    }

    // שחקן מסיים את השלב
    public void LogStageComplete()
    {
        stageCompleteCount++;
        Log("STAGE_COMPLETE");
    }

    // כמה פעמים היה על הסף (בר אחרון)
    public void LogLastBar()
    {
        lastBarCount++;
        Log("LAST_BAR");
    }

    // שחקן לוחץ quit
    public void LogQuit()
    {
        Log("QUIT");
        WriteRunSummary();
    }

    private void Log(string eventName)
    {
        StartRunIfNeeded();
        WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | Run {runNumber} | {eventName}");
    }

    private void WriteRunSummary()
    {
        if (summaryWritten) return;
        summaryWritten = true;

        // Nothing was logged this launch, so there's no run to summarize.
        if (runNumber == 0) return;

        var summary =
            $"=== Run {runNumber} summary ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===" + Environment.NewLine +
            $"Deaths: {deathCount}" + Environment.NewLine +
            $"Stage resets: {resetCount}" + Environment.NewLine +
            $"Checkpoint visits: {checkpointVisitCount}" + Environment.NewLine +
            $"Times on last bar: {lastBarCount}" + Environment.NewLine +
            $"Stages completed: {stageCompleteCount}" + Environment.NewLine +
            "===================================";

        WriteLine(summary);
    }

    private void WriteLine(string text)
    {
        try
        {
            Directory.CreateDirectory(FolderPath); // does nothing if it's already there
            File.AppendAllText(FilePath, text + Environment.NewLine);
        }
        catch (Exception e) // not just IOException: a read-only install location throws UnauthorizedAccessException
        {
            Debug.LogWarning($"AnalyticsLogger failed to write to {FilePath}: {e.Message}");
        }
    }
}
