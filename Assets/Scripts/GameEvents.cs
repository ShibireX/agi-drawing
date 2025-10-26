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
        Debug.Log("GameEvents: Game Started");
        OnGameStarted?.Invoke();
    }

    public static void TriggerGameEnded()
    {
        Debug.Log("GameEvents: Game Ended");
        OnGameEnded?.Invoke();
    }

    public static void TriggerGameReset()
    {
        Debug.Log("GameEvents: Game Reset");
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
