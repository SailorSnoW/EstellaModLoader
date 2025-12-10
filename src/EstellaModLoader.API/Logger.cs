namespace EstellaModLoader.API;

/// <summary>
/// Log levels for the logger.
/// </summary>
public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3
}

/// <summary>
/// Logger for mods to output messages.
/// </summary>
public static class Logger
{
    private static readonly object _lock = new();
    private static StreamWriter? _writer;
    private static Action<string>? _gdPrint;
    private static string? _logDirectory;

    /// <summary>
    /// Minimum log level to output. Default is Info.
    /// </summary>
    public static LogLevel MinLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// Initialize the logger. Called internally by the ModLoader.
    /// </summary>
    public static void Initialize(string logDirectory, Action<string>? gdPrint)
    {
        _logDirectory = logDirectory;
        _gdPrint = gdPrint;

        if (!Directory.Exists(logDirectory))
            Directory.CreateDirectory(logDirectory);

        var logPath = Path.Combine(logDirectory, "modloader.log");

        // Rotate old log
        if (File.Exists(logPath))
        {
            var oldLogPath = Path.Combine(logDirectory, "modloader.old.log");
            try
            {
                if (File.Exists(oldLogPath))
                    File.Delete(oldLogPath);
                File.Move(logPath, oldLogPath);
            }
            catch (IOException)
            {
                // Log rotation failed - old log file may be locked, continue without rotation
            }
        }

        try
        {
            _writer = new StreamWriter(logPath, append: false) { AutoFlush = true };
        }
        catch (IOException)
        {
            // Failed to create log file - logging will only output to Godot console
        }

        Info("Logger", $"EstellaModLoader initialized - Log level: {MinLevel}");
    }

    /// <summary>
    /// Log a message at the specified level.
    /// </summary>
    public static void Log(LogLevel level, string source, string message)
    {
        if (level < MinLevel) return;

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelStr = level switch
        {
            LogLevel.Debug => "DBG",
            LogLevel.Info => "INF",
            LogLevel.Warn => "WRN",
            LogLevel.Error => "ERR",
            _ => "???"
        };

        var line = $"[{timestamp}] [{levelStr}] [{source}] {message}";

        lock (_lock)
        {
            try
            {
                _writer?.WriteLine(line);
            }
            catch (ObjectDisposedException)
            {
                // Writer was disposed, ignore
            }
        }

        // Output to Godot console
        try
        {
            _gdPrint?.Invoke(line);
        }
        catch (Exception)
        {
            // Godot print failed - game may be shutting down
        }
    }

    /// <summary>
    /// Log a debug message.
    /// </summary>
    public static void Debug(string source, string message) => Log(LogLevel.Debug, source, message);

    /// <summary>
    /// Log an info message.
    /// </summary>
    public static void Info(string source, string message) => Log(LogLevel.Info, source, message);

    /// <summary>
    /// Log a warning message.
    /// </summary>
    public static void Warn(string source, string message) => Log(LogLevel.Warn, source, message);

    /// <summary>
    /// Log an error message.
    /// </summary>
    public static void Error(string source, string message) => Log(LogLevel.Error, source, message);

    /// <summary>
    /// Log an error message with exception details.
    /// </summary>
    public static void Error(string source, string message, Exception ex)
    {
        Log(LogLevel.Error, source, $"{message}: {ex.Message}");
        Log(LogLevel.Debug, source, ex.StackTrace ?? "No stack trace");
    }

    /// <summary>
    /// Write a crash dump file. Called internally by the ModLoader.
    /// </summary>
    public static void WriteCrashDump(string context, Exception ex)
    {
        if (_logDirectory == null) return;

        try
        {
            var crashPath = Path.Combine(_logDirectory, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            var content = $"""
                EstellaModLoader Crash Report
                ==============================
                Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                Context: {context}

                Exception Type: {ex.GetType().FullName}
                Message: {ex.Message}

                Stack Trace:
                {ex.StackTrace}

                Inner Exception:
                {ex.InnerException?.ToString() ?? "None"}
                """;

            File.WriteAllText(crashPath, content);
            Error("Logger", $"Crash dump saved to: {crashPath}");
        }
        catch (IOException)
        {
            // Failed to write crash dump - cannot recover
        }
    }

    /// <summary>
    /// Shutdown the logger. Called internally by the ModLoader.
    /// </summary>
    public static void Shutdown()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}