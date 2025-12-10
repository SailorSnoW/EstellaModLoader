using System.Reflection;
using System.Runtime.Loader;
using EstellaModLoader.API;

namespace EstellaModLoader;

public static class Core
{
    public const string Version = "0.4.0";

    private static readonly List<IMod> _mods = [];
    private static readonly List<ModLoadContext> _modContexts = [];
    private static string? _modsPath;
    private static bool _initialized;

    public static IReadOnlyList<IMod> LoadedMods => _mods.AsReadOnly();
    public static string ModsPath => _modsPath ?? throw new InvalidOperationException("Core.Init() must be called first");

    public static void Init()
    {
        if (_initialized) return;

        try
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var dataDir = Path.GetDirectoryName(assemblyPath)!;
            var gameRoot = Path.GetDirectoryName(dataDir)!;
            _modsPath = Path.Combine(gameRoot, "mods");

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

    #region Hooks

    public static void OnMainMenuReady(object mainMenu)
    {
        SafeInvoke("Core.OnMainMenuReady", () =>
        {
            Logger.Info("ModLoader", "MainMenu ready, notifying mods...");

            dynamic menu = mainMenu;

            SafeInvoke("VersionLabel", () =>
            {
                dynamic versionLabel = menu.GetNode("%Version");
                string currentText = versionLabel.Text;
                versionLabel.Text = currentText + " [MODDED]";
            });

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

            foreach (var mod in _mods)
            {
                SafeInvoke($"{mod.Name}.OnPlayer", () => mod.OnPlayer(player));
            }
        });
    }

    public static void OnInterfaceMenuOpen(object interfaceMenu, int screenIndex)
    {
        SafeInvoke("Core.OnInterfaceMenuOpen", () =>
        {
            var screenName = screenIndex switch
            {
                ScreenIndex.SongSelection => "SongSelection",
                ScreenIndex.Result => "Result",
                ScreenIndex.Settings => "Settings",
                ScreenIndex.Chapters => "Chapters",
                ScreenIndex.Story => "Story",
                _ => $"Unknown({screenIndex})"
            };

            Logger.Info("ModLoader", $"InterfaceMenu opened: {screenName}");

            foreach (var mod in _mods)
            {
                SafeInvoke($"{mod.Name}.OnInterfaceMenuChanged", () => mod.OnInterfaceMenuChanged(interfaceMenu, screenIndex));
            }

            // Get the current screen control and call specific hooks
            dynamic menu = interfaceMenu;
            dynamic? current = null;

            SafeInvoke("GetCurrent", () =>
            {
                current = menu.GetCurrent();
            });

            if (current is null) return;

            switch (screenIndex)
            {
                case ScreenIndex.SongSelection:
                    foreach (var mod in _mods)
                        SafeInvoke($"{mod.Name}.OnSongSelection", () => mod.OnSongSelection(current));
                    break;

                case ScreenIndex.Result:
                    foreach (var mod in _mods)
                        SafeInvoke($"{mod.Name}.OnResult", () => mod.OnResult(current));
                    break;

                case ScreenIndex.Settings:
                    foreach (var mod in _mods)
                        SafeInvoke($"{mod.Name}.OnSettings", () => mod.OnSettings(current));
                    break;

                case ScreenIndex.Chapters:
                    foreach (var mod in _mods)
                        SafeInvoke($"{mod.Name}.OnChapters", () => mod.OnChapters(current));
                    break;
            }
        });
    }

    public static void OnSessionStart(object session)
    {
        SafeInvoke("Core.OnSessionStart", () =>
        {
            dynamic sess = session;

            Logger.Info("ModLoader", $"New Session started");

            foreach (var mod in _mods)
            {
                SafeInvoke($"{mod.Name}.OnSessionStart", () => mod.OnSessionStart(session));
            }
        });
    }

    public static void OnStatsUpdated(object statsService)
    {
        SafeInvoke("Core.OnStatsUpdated", () =>
        {
            dynamic stats = statsService;

            Logger.Info("ModLoader", $"Stats updated - Rating: {stats.Rating}, Rank: {stats.Rank}");

            foreach (var mod in _mods)
            {
                SafeInvoke($"{mod.Name}.OnStatsUpdated", () => mod.OnStatsUpdated(statsService));
            }
        });
    }

    #endregion

    #region Mod Loading

    private static void LoadAllMods()
    {
        if (_modsPath is null) return;

        if (!Directory.Exists(_modsPath))
        {
            Logger.Info("ModLoader", $"Creating mods directory: {_modsPath}");
            Directory.CreateDirectory(_modsPath);
            return;
        }

        // Load mods from subdirectories (new structure: mods/ModName/ModName.dll)
        var modDirs = Directory.GetDirectories(_modsPath);
        Logger.Info("ModLoader", $"Found {modDirs.Length} mod folder(s)");

        foreach (var modDir in modDirs)
        {
            var dirName = Path.GetFileName(modDir);

            if (dirName.Contains("ModLoader", StringComparison.OrdinalIgnoreCase))
                continue;

            // Look for main DLL (same name as folder)
            var mainDll = Path.Combine(modDir, $"{dirName}.dll");
            if (File.Exists(mainDll))
            {
                LoadModFromDll(mainDll);
            }
            else
            {
                // Fallback: load first DLL found that's not a common dependency
                var dlls = Directory.GetFiles(modDir, "*.dll")
                    .Where(d => !IsCommonDependency(Path.GetFileName(d)))
                    .ToArray();

                if (dlls.Length > 0)
                {
                    LoadModFromDll(dlls[0]);
                }
                else
                {
                    Logger.Warn("ModLoader", $"No mod DLL found in {dirName}/");
                }
            }
        }

        // Also support legacy flat structure (mods/*.dll) for backwards compatibility
        var legacyDlls = Directory.GetFiles(_modsPath, "*.dll")
            .Where(d => !Path.GetFileName(d).Contains("ModLoader", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (legacyDlls.Length > 0)
        {
            Logger.Info("ModLoader", $"Found {legacyDlls.Length} legacy DLL(s) in mods root");
            foreach (var dll in legacyDlls)
            {
                LoadModFromDll(dll);
            }
        }

        Logger.Info("ModLoader", $"{_mods.Count} mod(s) loaded successfully");
    }

    private static bool IsCommonDependency(string fileName)
    {
        var commonDeps = new[]
        {
            "Newtonsoft.Json.dll",
            "DiscordRPC.dll",
            "System.",
            "Microsoft."
        };

        return commonDeps.Any(dep => fileName.StartsWith(dep, StringComparison.OrdinalIgnoreCase));
    }

    private static void LoadModFromDll(string dllPath)
    {
        var fileName = Path.GetFileName(dllPath);

        SafeInvoke($"Loading {fileName}", () =>
        {
            Logger.Debug("ModLoader", $"Loading assembly: {fileName}");

            // Create isolated context for this mod
            var modDir = Path.GetDirectoryName(dllPath)!;
            var context = new ModLoadContext(fileName, modDir);
            _modContexts.Add(context);

            var asm = context.LoadFromAssemblyPath(dllPath);
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

    #endregion

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

/// <summary>
/// Isolated AssemblyLoadContext for each mod.
/// This allows mods to have their own versions of dependencies without conflicts.
/// </summary>
internal class ModLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _modDirectory;

    public ModLoadContext(string name, string modDirectory) : base(name, isCollectible: false)
    {
        _modDirectory = modDirectory;
        _resolver = new AssemblyDependencyResolver(Path.Combine(modDirectory, name));
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Always use shared assemblies for the API and Godot
        if (assemblyName.Name == "EstellaModLoader.API" ||
            assemblyName.Name == "GodotSharp" ||
            assemblyName.Name == "GodotSharpEditor" ||
            assemblyName.Name?.StartsWith("System.") == true ||
            assemblyName.Name?.StartsWith("Microsoft.") == true)
        {
            return null; // Let the default context handle these
        }

        // Try to resolve from the mod's directory first
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            Logger.Debug("ModLoader", $"[{Name}] Loading dependency: {assemblyName.Name} from {Path.GetFileName(assemblyPath)}");
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Try to find in the mod directory
        var localPath = Path.Combine(_modDirectory, $"{assemblyName.Name}.dll");
        if (File.Exists(localPath))
        {
            Logger.Debug("ModLoader", $"[{Name}] Loading dependency: {assemblyName.Name} from local directory");
            return LoadFromAssemblyPath(localPath);
        }

        // Let the default context try to resolve
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
