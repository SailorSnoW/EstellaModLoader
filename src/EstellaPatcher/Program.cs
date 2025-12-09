using System.Runtime.Versioning;
using Microsoft.Win32;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace EstellaPatcher;

internal static class Program
{
    private const string Version = "1.0.0";
    private const string GameDllName = "ESTELLA.dll";
    private const string BackupSuffix = ".backup";
    private const string ModLoaderDllName = "EstellaModLoader.dll";
    private const string ModLoaderTypeName = "EstellaModLoader.Core";

    private static readonly string[] GameFolderNames = ["Estella Demo", "Estella"];
    private static readonly string DataFolderName = "data_ESTELLA_windows_x86_64";

    private static int Main(string[] args)
    {
        Console.WriteLine($"EstellaPatcher v{Version}");
        Console.WriteLine(new string('=', 32));

        try
        {
            // No arguments = show menu
            if (args.Length == 0)
            {
                args = ShowMenu();
                if (args.Contains("--exit"))
                {
                    return 0;
                }
            }

            return Run(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FATAL ERROR: {ex.Message}");
            return 1;
        }
    }

    private static string[] ShowMenu()
    {
        Console.WriteLine();

        // Show current game status if we can find it
        string? gamePath = FindGamePath([]);
        if (gamePath is not null)
        {
            var dllPath = Path.Combine(gamePath, GameDllName);
            var backupPath = dllPath + BackupSuffix;

            bool isPatched = IsGamePatched(dllPath);
            bool hasBackup = File.Exists(backupPath);

            Console.WriteLine($"Game found: {gamePath}");
            Console.WriteLine($"Status: {(isPatched ? "PATCHED" : "NOT PATCHED")}");
            if (hasBackup)
            {
                Console.WriteLine("Backup: Available");
            }
        }
        else
        {
            Console.WriteLine("Game: Not found (will search on action)");
        }

        Console.WriteLine();
        Console.WriteLine("What would you like to do?");
        Console.WriteLine();
        Console.WriteLine("  [1] Patch game (enable mods)");
        Console.WriteLine("  [2] Unpatch game (restore original)");
        Console.WriteLine("  [3] Exit");
        Console.WriteLine();
        Console.Write("Choice: ");

        var key = Console.ReadKey(true);
        Console.WriteLine(key.KeyChar);
        Console.WriteLine();

        return key.KeyChar switch
        {
            '1' => [],           // Patch (default action)
            '2' => ["--unpatch"],
            _ => ["--exit"]      // Signal to exit
        };
    }

    private static bool IsGamePatched(string dllPath)
    {
        try
        {
            using var assembly = AssemblyDefinition.ReadAssembly(dllPath);
            var sceneManager = assembly.MainModule.Types.FirstOrDefault(t => t.Name == "SceneManager");
            var cctor = sceneManager?.Methods.FirstOrDefault(m => m.Name == ".cctor");

            if (cctor?.Body is null) return false;

            return cctor.Body.Instructions.Any(i =>
                i.OpCode == OpCodes.Ldstr &&
                i.Operand?.ToString() == "Init");
        }
        catch
        {
            return false;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        if (args.Contains("--exit"))
        {
            return 0;
        }

        bool unpatchMode = args.Contains("--unpatch") || args.Contains("-u");
        var pathArgs = args.Where(a => !a.StartsWith('-')).ToArray();

        string? gamePath = FindGamePath(pathArgs);
        if (gamePath is null)
        {
            Console.WriteLine("ERROR: Could not find game installation path.");
            Console.WriteLine("Please specify the path as an argument or create a config.txt file.");
            Console.WriteLine("Run with --help for more information.");
            return 1;
        }

        Console.WriteLine($"Game path: {gamePath}");

        var dllPath = Path.Combine(gamePath, GameDllName);
        var backupPath = dllPath + BackupSuffix;

        if (!File.Exists(dllPath))
        {
            Console.WriteLine($"ERROR: {GameDllName} not found at {dllPath}");
            return 1;
        }

        if (unpatchMode)
        {
            return Unpatch(dllPath, backupPath);
        }

        return Patch(gamePath, dllPath, backupPath);
    }

    private static void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Usage: EstellaPatcher [options] [game_path]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help      Show this help message");
        Console.WriteLine("  -u, --unpatch   Restore the original game files");
        Console.WriteLine();
        Console.WriteLine("Game path detection (in order of priority):");
        Console.WriteLine($"  1. Command line argument (path to {DataFolderName})");
        Console.WriteLine("  2. config.txt file in patcher directory");
        Console.WriteLine("  3. Auto-detect from Steam installation");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  EstellaPatcher                    Auto-detect and patch");
        Console.WriteLine("  EstellaPatcher --unpatch          Restore original game");
        Console.WriteLine("  EstellaPatcher \"C:\\path\\to\\...\"   Patch specific path");
        Console.WriteLine();
        Console.WriteLine("Tip: Double-click the exe for interactive mode.");
    }

    private static int Unpatch(string dllPath, string backupPath)
    {
        Console.WriteLine("=== UNPATCH MODE ===");
        Console.WriteLine();

        if (!File.Exists(backupPath))
        {
            Console.WriteLine("ERROR: No backup found. Cannot restore original game.");
            Console.WriteLine($"Expected backup at: {backupPath}");
            return 1;
        }

        try
        {
            Console.WriteLine($"Restoring original {GameDllName} from backup...");
            File.Copy(backupPath, dllPath, overwrite: true);
            Console.WriteLine();
            Console.WriteLine("Game restored successfully!");
            return 0;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"ERROR: Failed to restore - {ex.Message}");
            Console.WriteLine("Make sure the game is not running.");
            return 1;
        }
    }

    private static int Patch(string gamePath, string dllPath, string backupPath)
    {
        Console.WriteLine("=== PATCH MODE ===");
        Console.WriteLine();

        var gameRoot = Path.GetDirectoryName(gamePath)
            ?? throw new InvalidOperationException("Could not determine game root directory.");

        var modsFolder = Path.Combine(gameRoot, "mods");
        var modloaderPath = Path.Combine(modsFolder, ModLoaderDllName);
        var tempPath = dllPath + ".temp";

        // Create mods folder if it doesn't exist
        if (!Directory.Exists(modsFolder))
        {
            Console.WriteLine($"Creating mods folder: {modsFolder}");
            Directory.CreateDirectory(modsFolder);
        }

        if (!File.Exists(modloaderPath))
        {
            Console.WriteLine($"WARNING: {ModLoaderDllName} not found.");
            Console.WriteLine($"Make sure to copy it to: {modsFolder}");
            Console.WriteLine();
        }

        // Create or restore from backup
        try
        {
            if (!File.Exists(backupPath))
            {
                Console.WriteLine("Creating backup...");
                File.Copy(dllPath, backupPath);
            }
            else
            {
                Console.WriteLine("Restoring from backup before patching...");
                File.Copy(backupPath, dllPath, overwrite: true);
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"ERROR: Failed to handle backup - {ex.Message}");
            Console.WriteLine("Make sure the game is not running.");
            return 1;
        }

        Console.WriteLine("Loading assembly...");

        using var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(gamePath);

        try
        {
            using var assembly = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadWrite = false
            });

            var module = assembly.MainModule;
            int patchCount = 0;

            patchCount += PatchMethod(module, "SceneManager", ".cctor", "Init", passThis: false, modloaderPath);
            patchCount += PatchMethod(module, "MainMenu", "_Ready", "OnMainMenuReady", passThis: true, modloaderPath);
            patchCount += PatchMethod(module, "Player", "_Ready", "OnPlayerReady", passThis: true, modloaderPath);

            if (patchCount == 0)
            {
                Console.WriteLine();
                Console.WriteLine("No patches were applied. Game may already be patched.");
                return 0;
            }

            Console.WriteLine("Saving patched assembly...");
            assembly.Write(tempPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to patch assembly - {ex.Message}");
            return 1;
        }

        // Replace original with patched version
        try
        {
            File.Delete(dllPath);
            File.Move(tempPath, dllPath);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"ERROR: Failed to replace game DLL - {ex.Message}");
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("Patching complete!");
        Console.WriteLine($"Mods folder: {modsFolder}");
        return 0;
    }

    private static int PatchMethod(
        ModuleDefinition module,
        string typeName,
        string methodName,
        string loaderMethod,
        bool passThis,
        string modloaderPath)
    {
        Console.WriteLine($"Patching {typeName}.{methodName} -> {loaderMethod}()...");

        var type = module.Types.FirstOrDefault(t => t.Name == typeName);
        if (type is null)
        {
            Console.WriteLine($"  WARNING: Type '{typeName}' not found, skipping.");
            return 0;
        }

        var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
        if (method is null)
        {
            Console.WriteLine($"  WARNING: Method '{methodName}' not found, skipping.");
            return 0;
        }

        // Check if already patched (look for our marker string)
        if (method.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && i.Operand?.ToString() == loaderMethod))
        {
            Console.WriteLine("  Already patched, skipping.");
            return 0;
        }

        InjectModLoaderCall(module, method, loaderMethod, passThis, modloaderPath);
        Console.WriteLine("  Done.");
        return 1;
    }

    private static void InjectModLoaderCall(
        ModuleDefinition module,
        MethodDefinition method,
        string loaderMethod,
        bool passThis,
        string modloaderPath)
    {
        var il = method.Body.GetILProcessor();
        var instructions = method.Body.Instructions;

        // Import reflection methods
        var assemblyType = typeof(System.Reflection.Assembly);
        var loadFrom = module.ImportReference(assemblyType.GetMethod("LoadFrom", [typeof(string)]));
        var getType = module.ImportReference(assemblyType.GetMethod("GetType", [typeof(string)]));
        var getMethod = module.ImportReference(typeof(Type).GetMethod("GetMethod", [typeof(string)]));
        var invoke = module.ImportReference(typeof(System.Reflection.MethodBase).GetMethod("Invoke", [typeof(object), typeof(object[])]));

        // Build injection code
        var injectedCode = new List<Instruction>
        {
            il.Create(OpCodes.Ldstr, modloaderPath),
            il.Create(OpCodes.Call, loadFrom),
            il.Create(OpCodes.Ldstr, ModLoaderTypeName),
            il.Create(OpCodes.Callvirt, getType),
            il.Create(OpCodes.Ldstr, loaderMethod),
            il.Create(OpCodes.Callvirt, getMethod),
            il.Create(OpCodes.Ldnull)
        };

        if (passThis)
        {
            // Create object[] { this }
            injectedCode.Add(il.Create(OpCodes.Ldc_I4_1));
            injectedCode.Add(il.Create(OpCodes.Newarr, module.TypeSystem.Object));
            injectedCode.Add(il.Create(OpCodes.Dup));
            injectedCode.Add(il.Create(OpCodes.Ldc_I4_0));
            injectedCode.Add(il.Create(OpCodes.Ldarg_0));
            injectedCode.Add(il.Create(OpCodes.Stelem_Ref));
        }
        else
        {
            injectedCode.Add(il.Create(OpCodes.Ldnull));
        }

        injectedCode.Add(il.Create(OpCodes.Callvirt, invoke));
        injectedCode.Add(il.Create(OpCodes.Pop));

        var finalRet = il.Create(OpCodes.Ret);

        // Find all return instructions
        var returnInstructions = instructions.Where(i => i.OpCode == OpCodes.Ret).ToList();
        var firstInjected = injectedCode[0];

        // Append injected code at the end
        foreach (var instr in injectedCode)
        {
            il.Append(instr);
        }
        il.Append(finalRet);

        // Replace all original returns with branches to our code
        foreach (var ret in returnInstructions)
        {
            var branch = il.Create(OpCodes.Br, firstInjected);
            il.Replace(ret, branch);
        }

        // Update any branch targets that pointed to old returns
        foreach (var instr in instructions)
        {
            if (instr.Operand is Instruction target && returnInstructions.Contains(target))
            {
                instr.Operand = firstInjected;
            }
        }

        method.Body.MaxStackSize = Math.Max(method.Body.MaxStackSize, 8);
    }

    #region Game Path Detection

    private static string? FindGamePath(string[] args)
    {
        // 1. Command line argument
        if (args.Length > 0)
        {
            var path = ValidateGamePath(args[0]);
            if (path is not null)
            {
                Console.WriteLine("Using path from command line argument.");
                return path;
            }
        }

        // 2. Config file
        var configPath = Path.Combine(AppContext.BaseDirectory, "config.txt");
        if (File.Exists(configPath))
        {
            var pathFromConfig = File.ReadAllText(configPath).Trim();
            var path = ValidateGamePath(pathFromConfig);
            if (path is not null)
            {
                Console.WriteLine("Using path from config.txt.");
                return path;
            }
        }

        // 3. Auto-detect from Steam
        return FindGamePathFromSteam();
    }

    private static string? ValidateGamePath(string path)
    {
        if (!Directory.Exists(path)) return null;

        var dllPath = Path.Combine(path, GameDllName);
        return File.Exists(dllPath) ? path : null;
    }

    private static string? FindGamePathFromSteam()
    {
        var steamPath = FindSteamPath();
        if (steamPath is null) return null;

        // Check main Steam folder and library folders
        var libraryPaths = new List<string> { steamPath };

        var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFoldersPath))
        {
            libraryPaths.AddRange(ParseSteamLibraryFolders(libraryFoldersPath));
        }

        foreach (var libraryPath in libraryPaths)
        {
            foreach (var gameName in GameFolderNames)
            {
                var gamePath = Path.Combine(libraryPath, "steamapps", "common", gameName, DataFolderName);
                var validPath = ValidateGamePath(gamePath);
                if (validPath is not null)
                {
                    Console.WriteLine("Auto-detected game path from Steam.");
                    return validPath;
                }
            }
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static string? FindSteamPathFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var steamPath = key?.GetValue("SteamPath") as string;
            if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
            {
                return steamPath;
            }
        }
        catch
        {
            // Registry access failed
        }

        return null;
    }

    private static string? FindSteamPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var registryPath = FindSteamPathFromRegistry();
            if (registryPath is not null) return registryPath;

            // Fallback to common paths
            string[] commonPaths = [@"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam"];
            foreach (var path in commonPaths)
            {
                if (Directory.Exists(path)) return path;
            }
        }

        return null;
    }

    private static List<string> ParseSteamLibraryFolders(string vdfPath)
    {
        var paths = new List<string>();

        try
        {
            foreach (var line in File.ReadLines(vdfPath))
            {
                if (!line.Contains("\"path\"")) continue;

                var parts = line.Split('"');
                if (parts.Length >= 4)
                {
                    var path = parts[3].Replace(@"\\", @"\");
                    if (Directory.Exists(path))
                    {
                        paths.Add(path);
                    }
                }
            }
        }
        catch
        {
            // Failed to parse VDF file
        }

        return paths;
    }

    #endregion
}
