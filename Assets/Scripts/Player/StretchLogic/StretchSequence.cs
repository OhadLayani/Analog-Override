using UnityEngine;

/// <summary>Pure stretch state machine: stretch 1 → 2 → 3 on begin, hold at 3 while held, contract 3 → 2 → 1 on release.</summary>
public class StretchSequence
{
    public const int Idle = 0;
    public const int Half = 1;
    public const int Full = 2;

    private readonly float frameDuration;
    private int target = Idle;
    private float timer;

    public StretchSequence(float frameDuration)
    {
        this.frameDuration = Mathf.Max(0f, frameDuration);
    }

    /// <summary>Current stage: Idle (mode 1), Half (mode 2) or Full (mode 3).</summary>
    public int Stage { get; private set; } = Idle;

    /// <summary>True from the start of a stretch until fully contracted again. Movement is blocked while true.</summary>
    public bool IsActive => Stage != Idle || target != Idle;

    /// <summary>True while holding at full stretch.</summary>
    public bool IsFull => Stage == Full && target == Full;

    /// <summary>Starts a stretch. Only allowed when fully idle; returns false otherwise.</summary>
    public bool TryBegin()
    {
        if (IsActive) return false;

        Stage = Half;
        target = Full;
        timer = 0f;
        return true;
    }

    /// <summary>
    /// Advances the sequence. A release is only acted on once already at Full (checked before
    /// advancing), so Full is always shown for at least one tick and one tick never rises and contracts.
    /// </summary>
    public void Tick(float deltaTime, bool held)
    {
        if (IsFull)
        {
            if (!held)
            {
                Stage = Half;
                target = Idle;
                timer = 0f;
            }
            return;
        }

        if (Stage == target) return;

        timer += deltaTime;
        if (timer < frameDuration) return;

        // Half is always exactly one step from either target, so one step per tick never overshoots.
        timer = 0f;
        Stage += target > Stage ? 1 : -1;
    }

    /// <summary>A stretch may only start while standing still: not sliding and no movement key held.</summary>
    public static bool CanBegin(bool isMoving, bool movementKeyHeld) => !isMoving && !movementKeyHeld;

    /// <summary>Picks the per-stage value (index 0 = Half, 1 = Full); default for Idle or a missing entry.</summary>
    public static T ForStage<T>(T[] perStage, int stage)
    {
        if (stage <= Idle || perStage == null || stage - 1 >= perStage.Length) return default;
        return perStage[stage - 1];
    }
}
