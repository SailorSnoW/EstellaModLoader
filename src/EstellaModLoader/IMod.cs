namespace EstellaModLoader;

/// <summary>
/// Interface that all mods must implement.
/// </summary>
public interface IMod
{
    /// <summary>
    /// Display name of the mod.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Version string of the mod.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Called when the mod is loaded.
    /// </summary>
    void OnLoad();

    /// <summary>
    /// Called when the main menu is ready.
    /// </summary>
    /// <param name="mainMenu">The MainMenu node (use dynamic to access Godot properties)</param>
    void OnMainMenu(object mainMenu);

    /// <summary>
    /// Called when the player is ready (entering gameplay).
    /// </summary>
    /// <param name="player">The Player node (use dynamic to access Godot properties)</param>
    void OnPlayer(object player);

    /// <summary>
    /// Called when the interface menu changes screen.
    /// </summary>
    /// <param name="interfaceMenu">The InterfaceMenu node</param>
    /// <param name="screenIndex">The screen index (0=SongSelection, 1=Result, 2=Settings, 3=Chapters, 4=Story)</param>
    void OnInterfaceMenuChanged(object interfaceMenu, int screenIndex);

    /// <summary>
    /// Called when entering song selection screen.
    /// </summary>
    /// <param name="songSelection">The SongSelection control node</param>
    void OnSongSelection(object songSelection);

    /// <summary>
    /// Called when viewing results after a song.
    /// </summary>
    /// <param name="result">The Result control node</param>
    void OnResult(object result);

    /// <summary>
    /// Called when entering settings screen.
    /// </summary>
    /// <param name="settings">The Settings control node</param>
    void OnSettings(object settings);

    /// <summary>
    /// Called when entering chapters screen.
    /// </summary>
    /// <param name="chapters">The Chapters control node</param>
    void OnChapters(object chapters);

    /// <summary>
    /// Called when a game session starts (song selected and about to play).
    /// Use Session.Instance to access track metadata, chart info, etc.
    /// </summary>
    /// <param name="session">The Session singleton containing track/chart info</param>
    void OnSessionStart(object session);

    /// <summary>
    /// Called when player stats are refreshed (rating, rank updated).
    /// </summary>
    /// <param name="statsService">The StatsService singleton containing Rating, Rank, etc.</param>
    void OnStatsUpdated(object statsService);
}

/// <summary>
/// Screen indices for InterfaceMenu.
/// </summary>
public static class ScreenIndex
{
    public const int SongSelection = 0;
    public const int Result = 1;
    public const int Settings = 2;
    public const int Chapters = 3;
    public const int Story = 4;
}
