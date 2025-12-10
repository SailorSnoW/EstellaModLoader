namespace EstellaModLoader;

/// <summary>
/// Base class for mods. Provides default empty implementations for all hooks.
/// Inherit from this class to only override the hooks you need.
/// </summary>
public abstract class ModBase : IMod
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Version { get; }

    /// <inheritdoc />
    public virtual void OnLoad() { }

    /// <inheritdoc />
    public virtual void OnMainMenu(object mainMenu) { }

    /// <inheritdoc />
    public virtual void OnPlayer(object player) { }

    /// <inheritdoc />
    public virtual void OnInterfaceMenuChanged(object interfaceMenu, int screenIndex) { }

    /// <inheritdoc />
    public virtual void OnSongSelection(object songSelection) { }

    /// <inheritdoc />
    public virtual void OnResult(object result) { }

    /// <inheritdoc />
    public virtual void OnSettings(object settings) { }

    /// <inheritdoc />
    public virtual void OnChapters(object chapters) { }

    /// <inheritdoc />
    public virtual void OnSessionStart(object session) { }

    /// <inheritdoc />
    public virtual void OnStatsUpdated(object statsService) { }
}
