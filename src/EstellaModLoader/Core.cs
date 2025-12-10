using System.Reflection;
using EstellaModLoader.API;

namespace EstellaModLoader;

public static class Core
{
    public const string Version = "0.2.0";

    private static readonly List<IMod> _mods = [];
    private static string _modsPath = null!;
    private static bool _initialized;

    public static IReadOnlyList<IMod> LoadedMods => _mods.AsReadOnly();
    public static string ModsPath => _modsPath;

    public static void Init()
    {
        if (_initialized) return;

        try
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var dataDir = Path.GetDirectoryName(assemblyPath)!;
            var gameRoot = Path.GetDirectoryName(dataDir)!; // Go up one level to game root
            _modsPath = Path.Combine(gameRoot, "mods");

            // Initialize logger first
            Action<string>? gdPrint = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "GodotSharp")
                {
                    var gdType = asm.GetType("Godot.GD");
                    var printMethod = gdType?.GetMethod("Print", [typeof(object[])]);
                    if (printMethod != null)
                    {
                        gdPrint = msg => printMethod.Invoke(null, [new object[] { msg }]);
                    }
                    break;
                }
            }

            Logger.Initialize(_modsPath, gdPrint);
            Logger.Info("ModLoader", $"EstellaModLoader v{Version} starting...");
            Logger.Info("ModLoader", $"Mods path: {_modsPath}");

            LoadAllMods();
            _initialized = true;
        }
        catch (Exception ex)
        {
            Logger.WriteCrashDump("Core.Init", ex);
        }
    }

    public static void OnMainMenuReady(object mainMenu)
    {
        SafeInvoke("Core.OnMainMenuReady", () =>
        {
            Logger.Info("ModLoader", "MainMenu ready, notifying mods...");

            dynamic menu = mainMenu;

            // Add modded indicator
            SafeInvoke("VersionLabel", () =>
            {
                dynamic versionLabel = menu.GetNode("%Version");
                string currentText = versionLabel.Text;
                versionLabel.Text = currentText + " [MODDED]";
            });

            // Notify all mods
            foreach (var mod in _mods)
            {
                SafeInvoke($"{mod.Name}.OnMainMenu", () => mod.OnMainMenu(menu));
            }
        });
    }

    public static void OnPlayerReady(object player)
    {
        SafeInvoke("Core.OnPlayerReady", () =>
        {
            Logger.Info("ModLoader", "Player ready, notifying mods...");

            // Notify all mods
            foreach (var mod in _mods)
            {
                SafeInvoke($"{mod.Name}.OnPlayer", () => mod.OnPlayer(player));
            }
        });
    }

    private static void LoadAllMods()
    {
        if (!Directory.Exists(_modsPath))
        {
            Logger.Info("ModLoader", $"Creating mods directory: {_modsPath}");
            Directory.CreateDirectory(_modsPath);
            return;
        }

        var dlls = Directory.GetFiles(_modsPath, "*.dll");
        Logger.Info("ModLoader", $"Found {dlls.Length} DLL(s) in mods folder");

        foreach (var dll in dlls)
        {
            var fileName = Path.GetFileName(dll);

            // Skip the modloader itself if copied there
            if (fileName.Contains("ModLoader", StringComparison.OrdinalIgnoreCase))
                continue;

            LoadModFromDll(dll);
        }

        Logger.Info("ModLoader", $"{_mods.Count} mod(s) loaded successfully");
    }

    private static void LoadModFromDll(string dllPath)
    {
        var fileName = Path.GetFileName(dllPath);

        SafeInvoke($"Loading {fileName}", () =>
        {
            Logger.Debug("ModLoader", $"Loading assembly: {fileName}");

            var asm = Assembly.LoadFrom(dllPath);
            var modTypes = asm.GetTypes().Where(t =>
                typeof(IMod).IsAssignableFrom(t) &&
                !t.IsInterface &&
                !t.IsAbstract);

            int count = 0;
            foreach (var modType in modTypes)
            {
                SafeInvoke($"Instantiating {modType.Name}", () =>
                {
                    var mod = (IMod)Activator.CreateInstance(modType)!;

                    SafeInvoke($"{mod.Name}.OnLoad", () =>
                    {
                        mod.OnLoad();
                        _mods.Add(mod);
                        Logger.Info("ModLoader", $"Loaded mod: {mod.Name} v{mod.Version}");
                        count++;
                    });
                });
            }

            if (count == 0)
            {
                Logger.Warn("ModLoader", $"No IMod implementations found in {fileName}");
            }
        });
    }

    private static void SafeInvoke(string context, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logger.Error("ModLoader", $"Exception in {context}", ex);
            Logger.WriteCrashDump(context, ex);
        }
    }
}