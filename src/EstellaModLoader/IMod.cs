namespace EstellaModLoader;

public interface IMod
{
    string Name { get; }
    string Version { get; }
    
    void OnLoad();
    void OnMainMenu(object ctx);
    void OnPlayer(object player);
}