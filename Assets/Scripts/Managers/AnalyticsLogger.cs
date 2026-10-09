using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

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
/// the game's executable or .app is in; in the Editor, the project folder), and both the folder and the
/// file are created automatically on first write. Existing lines are never touched, so data
/// accumulates across every play session instead of resetting each time the game launches. If
/// that location isn't writable (e.g. a build installed under Program Files) logging is skipped
/// with a warning rather than breaking the game.
///
/// A "run" is one game launch (or one Play in the Editor). The first load of each scene is logged
/// as SCENE_LOADED, so every run gets a number, and the summary includes the session length.
/// Lines always end in CRLF and the file is UTF-8, so it reads the same on macOS and Windows.
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

    // Same line ending on every OS, so a log appended to from both Mac and Windows stays consistent.
    private const string NewLine = "\r\n";

    private static readonly Encoding FileEncoding = new UTF8Encoding(false);

    private string FolderPath => Path.Combine(GameFolder(), LogFolderName);

    private string FilePath => Path.Combine(FolderPath, "analytics_log.txt");

    /// <summary>The folder holding the game's executable (or the .app on macOS), or the project folder in the Editor.</summary>
    private static string GameFolder()
    {
        // Application.dataPath is "<game>_Data" on Windows, "<project>/Assets" in the Editor,
        // and "<game>.app/Contents" in a macOS build, which needs one extra step up to leave the bundle.
        var folder = Directory.GetParent(Application.dataPath);
        if (Application.platform == RuntimePlatform.OSXPlayer && folder.Parent != null)
            folder = folder.Parent;
        return folder.FullName;
    }

    // 0 until the first event of this launch (see StartRunIfNeeded).
    private int runNumber;
    private bool summaryWritten;

    // Real time when this launch started, for the session length (unaffected by pause / timeScale).
    private DateTime sessionStart;

    // Scenes already logged this launch, so reloads after a death or reset aren't logged again.
    private readonly HashSet<string> loggedScenes = new HashSet<string>();

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

        sessionStart = DateTime.Now;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Debug.Log($"AnalyticsLogger writing to {FilePath}");
    }

    private void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!loggedScenes.Add(scene.name)) return;
        Log($"SCENE_LOADED | Scene: {scene.name}");
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
        Log($"QUIT | Session length: {SessionLength()}");
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
            $"=== Run {runNumber} summary ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===" + NewLine +
            $"Session length: {SessionLength()}" + NewLine +
            $"Deaths: {deathCount}" + NewLine +
            $"Stage resets: {resetCount}" + NewLine +
            $"Checkpoint visits: {checkpointVisitCount}" + NewLine +
            $"Times on last bar: {lastBarCount}" + NewLine +
            $"Stages completed: {stageCompleteCount}" + NewLine +
            "===================================";

        WriteLine(summary);
    }

    /// <summary>Time since launch as hh:mm:ss (hours keep counting past 24).</summary>
    private string SessionLength()
    {
        var t = DateTime.Now - sessionStart;
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
    }

    private void WriteLine(string text)
    {
        try
        {
            Directory.CreateDirectory(FolderPath); // does nothing if it's already there
            File.AppendAllText(FilePath, text + NewLine, FileEncoding);
        }
        catch (Exception e) // not just IOException: a read-only install location throws UnauthorizedAccessException
        {
            Debug.LogWarning($"AnalyticsLogger failed to write to {FilePath}: {e.Message}");
        }
    }
}
