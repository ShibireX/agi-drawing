using System;
using UnityEngine;

/// <summary>
/// Static event system for game state management
/// Allows decoupled communication between UI and Character systems
/// </summary>
public static class GameEvents
{
    // Game state events
    public static event Action OnGameStarted;
    public static event Action OnGameEnded;
    public static event Action OnGameReset;

    // Trigger methods
    public static void TriggerGameStarted()
    {
        OnGameStarted?.Invoke();
    }

    public static void TriggerGameEnded()
    {
        OnGameEnded?.Invoke();
    }

    public static void TriggerGameReset()
    {
        OnGameReset?.Invoke();
    }

    // Cleanup method (useful for scene transitions)
    public static void ClearAllListeners()
    {
        OnGameStarted = null;
        OnGameEnded = null;
        OnGameReset = null;
    }
}
