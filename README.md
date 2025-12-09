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

Place `EstellaModLoader.dll` and your mods in the `mods/` folder:

```
Estella Demo/
├── mods/
│   ├── EstellaModLoader.dll   ← Required
│   └── YourMod.dll            ← Mods go here
└── data_ESTELLA_windows_x86_64/
    └── ESTELLA.dll            ← Patched by EstellaPatcher
```

## Creating Mods

```csharp
using EstellaModLoader;

public class MyMod : IMod
{
    public string Name => "MyMod";
    public string Version => "1.0.0";

    public void OnLoad()
        => Logger.Info(Name, "Loaded!");

    public void OnMainMenu(object ctx) { }

    public void OnPlayer(object player) { }
}
```

> Use `dynamic` for Godot types to avoid assembly conflicts.

## Building

```bash
dotnet build -c Release
```

## Current Limitations

- Windows only (Steam auto-detection)
- Limited hook points (MainMenu, Player)
- No hot-reload
- No mod dependencies

## License

MIT
