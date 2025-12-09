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
    /// Called when the player is ready.
    /// </summary>
    /// <param name="player">The Player node (use dynamic to access Godot properties)</param>
    void OnPlayer(object player);
}
