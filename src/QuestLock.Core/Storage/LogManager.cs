using System.Text.Json;
using QuestLock.Core.Models;

namespace QuestLock.Core.Storage;

public class LogManager
{
    private readonly string _logsDirectory;
    private readonly int _maxRingBufferSize;
    private readonly LinkedList<LogEntry> _ringBuffer = new();
    private readonly object _lock = new();

    public LogManager(string? customLogsDirectory = null, int maxRingBufferSize = 250)
    {
        _maxRingBufferSize = maxRingBufferSize;

        if (!string.IsNullOrEmpty(customLogsDirectory))
        {
            _logsDirectory = customLogsDirectory;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _logsDirectory = Path.Combine(appData, "QuestLock", "logs");
        }

        try
        {
            if (!Directory.Exists(_logsDirectory))
            {
                Directory.CreateDirectory(_logsDirectory);
            }
        }
        catch
        {
            // Ochrana při inicializaci
        }
    }

    public string LogsDirectory => _logsDirectory;

    public void Log(string level, string source, string eventType, string message, Dictionary<string, string>? details = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level.ToUpperInvariant(),
            Source = source,
            Event = eventType.ToUpperInvariant(),
            Message = message,
            Details = details
        };

        lock (_lock)
        {
            // 1. Zápis do kruhové paměti (Ring Buffer)
            _ringBuffer.AddLast(entry);
            while (_ringBuffer.Count > _maxRingBufferSize)
            {
                _ringBuffer.RemoveFirst();
            }

            // 2. Persistentní zápis do denního souboru
            try
            {
                if (!Directory.Exists(_logsDirectory))
                {
                    Directory.CreateDirectory(_logsDirectory);
                }

                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var filePath = Path.Combine(_logsDirectory, $"questlock-{today}.log");
                File.AppendAllText(filePath, entry.ToString() + Environment.NewLine);
            }
            catch
            {
                // Zápis na disk nesmí zablokovat běh aplikace
            }
        }
    }

    public void Info(string source, string eventType, string message, Dictionary<string, string>? details = null)
        => Log("INFO", source, eventType, message, details);

    public void Warn(string source, string eventType, string message, Dictionary<string, string>? details = null)
        => Log("WARN", source, eventType, message, details);

    public void Error(string source, string eventType, string message, Dictionary<string, string>? details = null)
        => Log("ERROR", source, eventType, message, details);

    public void Debug(string source, string eventType, string message, Dictionary<string, string>? details = null)
        => Log("DEBUG", source, eventType, message, details);

    public IReadOnlyList<LogEntry> GetRecentLogs(int limit = 100, string? levelFilter = null)
    {
        lock (_lock)
        {
            var query = _ringBuffer.AsEnumerable();

            if (!string.IsNullOrEmpty(levelFilter) && levelFilter != "ALL")
            {
                query = query.Where(e => string.Equals(e.Level, levelFilter, StringComparison.OrdinalIgnoreCase));
            }

            return query.Reverse().Take(limit).ToList();
        }
    }

    public string GetRawLogContent(DateTime? date = null)
    {
        lock (_lock)
        {
            var dayStr = (date ?? DateTime.Now).ToString("yyyy-MM-dd");
            var filePath = Path.Combine(_logsDirectory, $"questlock-{dayStr}.log");

            if (File.Exists(filePath))
            {
                try
                {
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }
                catch
                {
                    // Fallback při zamčeném souboru
                }
            }

            // Pokud soubor ještě neexistuje nebo nejde otevřít, vrátíme obsah paměti
            return string.Join(Environment.NewLine, _ringBuffer.Select(e => e.ToString()));
        }
    }

    public object GenerateDiagnosticsBundle(ConfigManager cfg, StateManager state)
    {
        var currentConfig = cfg.Load();
        var currentState = state.Load();
        var recentLogs = GetRecentLogs(limit: 50);

        return new
        {
            generated_at = DateTime.UtcNow,
            system = new
            {
                os = Environment.OSVersion.ToString(),
                is_64bit_os = Environment.Is64BitOperatingSystem,
                machine_name = Environment.MachineName,
                user_name = Environment.UserName,
                dotnet_version = Environment.Version.ToString(),
                process_uptime = Environment.TickCount64 / 1000
            },
            config = currentConfig,
            state = currentState,
            logs = recentLogs
        };
    }
}
