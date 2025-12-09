namespace EstellaModLoader;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3
}

public static class Logger
{
    private static readonly object _lock = new();
    private static StreamWriter? _writer;
    private static Action<string>? _gdPrint;
    private static string? _logDirectory;

    public static LogLevel MinLevel { get; set; } = LogLevel.Info;

    internal static void Initialize(string logDirectory, Action<string>? gdPrint)
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
            catch { }
        }

        try
        {
            _writer = new StreamWriter(logPath, append: false) { AutoFlush = true };
        }
        catch { }

        Info("Logger", $"EstellaModLoader initialized - Log level: {MinLevel}");
    }

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
            catch { }
        }

        // Output to Godot console
        try
        {
            _gdPrint?.Invoke(line);
        }
        catch { }
    }

    public static void Debug(string source, string message) => Log(LogLevel.Debug, source, message);
    public static void Info(string source, string message) => Log(LogLevel.Info, source, message);
    public static void Warn(string source, string message) => Log(LogLevel.Warn, source, message);
    public static void Error(string source, string message) => Log(LogLevel.Error, source, message);

    public static void Error(string source, string message, Exception ex)
    {
        Log(LogLevel.Error, source, $"{message}: {ex.Message}");
        Log(LogLevel.Debug, source, ex.StackTrace ?? "No stack trace");
    }

    internal static void WriteCrashDump(string context, Exception ex)
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
        catch { }
    }

    internal static void Shutdown()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
