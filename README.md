# EstellaMod

> ⚠️ **Early Development** - This project is a work-in-progress MVP. Expect breaking changes and missing features.

A modding framework for the Estella rhythm-game.

This is a personal learning project - I'm new to game modding and this is primarily for research and fun exploring the modding world. Contributions and feedback are welcome!

**Tested on**: Estella v0.1.7-indev

## Components

| Component | Description |
|-----------|-------------|
| **EstellaPatcher** | CLI tool that patches the game to load mods |
| **EstellaModLoader** | Runtime library that manages mod lifecycle |

## Quick Start

### 1. Patch the Game

```bash
# Auto-detects Steam installation
EstellaPatcher.exe

# Or specify path manually
EstellaPatcher.exe "C:\...\data_ESTELLA_windows_x86_64"

# Restore original game
EstellaPatcher.exe --unpatch
```

### 2. Install Mods

Place `EstellaModLoader.dll` in the `mods/` folder and mods in subfolders:

```
Estella Demo/
├── mods/
│   ├── EstellaModLoader.dll       ← Required
│   └── MyMod/                     ← Mod folder
│       ├── MyMod.dll              ← Main mod DLL
│       └── SomeDependency.dll     ← Optional dependencies
└── data_ESTELLA_windows_x86_64/
    └── ESTELLA.dll                ← Patched by EstellaPatcher
```

Legacy flat structure (`mods/*.dll`) is still supported for backwards compatibility.

## Creating Mods

### Using ModBase (Recommended)

```csharp
using EstellaModLoader;

public class MyMod : ModBase
{
    public override string Name => "MyMod";
    public override string Version => "1.0.0";

    public override void OnLoad()
        => Logger.Info(Name, "Loaded!");

    public override void OnMainMenu(object mainMenu)
    {
        // Access Godot nodes via dynamic
        dynamic menu = mainMenu;
        Logger.Info(Name, "Main menu ready!");
    }

    public override void OnSessionStart(object session)
    {
        // Called when a song session starts
        dynamic sess = session;
        Logger.Info(Name, $"Playing: {sess.Track.Title}");
    }
}
```

### Using IMod Interface

```csharp
using EstellaModLoader;

public class MyMod : IMod
{
    public string Name => "MyMod";
    public string Version => "1.0.0";

    public void OnLoad() { }
    public void OnMainMenu(object mainMenu) { }
    public void OnPlayer(object player) { }
    public void OnInterfaceMenuChanged(object interfaceMenu, int screenIndex) { }
    public void OnSongSelection(object songSelection) { }
    public void OnResult(object result) { }
    public void OnSettings(object settings) { }
    public void OnChapters(object chapters) { }
    public void OnSessionStart(object session) { }
    public void OnStatsUpdated(object statsService) { }
}
```

> Use `dynamic` for Godot types to avoid assembly conflicts.

## Building

```bash
dotnet build -c Release
```

## Current Limitations

- Windows only (Steam auto-detection)
- No hot-reload
- No mod dependencies

## License

MIT
