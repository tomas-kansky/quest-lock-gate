using System.Text.Json;
using QuestLock.Core.Models;

namespace QuestLock.Core.Storage;

public class ConfigManager
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ConfigManager(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath))
        {
            _filePath = customPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _filePath = Path.Combine(appData, "QuestLock", "config.json");
        }
    }

    public string FilePath => _filePath;

    public AppConfig Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                    if (config != null) return config;
                }
            }
            catch
            {
                // Fallback na výchozí konfiguraci při chybě čtení
            }

            var defaultConfig = new AppConfig();
            Save(defaultConfig);
            return defaultConfig;
        }
    }

    public void Save(AppConfig config)
    {
        lock (_lock)
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(config, JsonOptions);
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
    }
}
