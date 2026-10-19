public enum GameMode
{
    Trial,
    StandardLevel
}

/// <summary>
/// Global switch between the guided trial session and the unguided assessment run.
/// Set through <see cref="BoxSpawner.gameMode"/> in the scene.
/// </summary>
public static class GameModeSettings
{
    public static GameMode CurrentMode = GameMode.Trial;

    /// <summary>Question banner, Box-View card, and on-screen hints.</summary>
    public static bool ShowGuidance => CurrentMode == GameMode.Trial;

    public static string SessionType => CurrentMode == GameMode.Trial ? "trial" : "assessment";
}
