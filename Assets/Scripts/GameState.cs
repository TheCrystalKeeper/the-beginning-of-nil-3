using UnityEngine;

public static class GameState
{
    // Game states
    public const int Playing = 0;
    public const int PausedNoUI = 1;
    public const int PausedWithUI = 2;

    // Current state
    public static int State { get; private set; } = Playing;
    public static event System.Action<int> OnStateChanged;

    /// <summary>
    /// Set the game state manually.
    /// </summary>

    public static void SetState(int newState)
    {
        State = newState;
        Time.timeScale = (State == Playing) ? 1f : 0f;
        OnStateChanged?.Invoke(State);
    }

    /// <summary>
    /// Resume normal gameplay.
    /// </summary>
    public static void Resume()
    {
        SetState(Playing);
    }

    /// <summary>
    /// Pause the game with no UI overlay.
    /// </summary>
    public static void Pause()
    {
        SetState(PausedNoUI);
    }

    /// <summary>
    /// Pause the game with a UI overlay.
    /// </summary>
    public static void PauseWithUI()
    {
        SetState(PausedWithUI);
    }

    /// <summary>
    /// Returns true if the game is paused in any way.
    /// </summary>
    public static bool IsPaused()
    {
        return State == PausedNoUI || State == PausedWithUI;
    }
}