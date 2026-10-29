using System.Text.Json;
using QuestLock.Core.Models;

namespace QuestLock.Core.Storage;

public class StateManager
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public StateManager(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath))
        {
            _filePath = customPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _filePath = Path.Combine(appData, "QuestLock", "state.json");
        }
    }

    public string FilePath => _filePath;

    public AppState Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var state = JsonSerializer.Deserialize<AppState>(json, JsonOptions);
                    if (state != null) return state;
                }
            }
            catch
            {
                // Fallback na výchozí zamčený stav při chybě čtení
            }

            var defaultState = new AppState { IsLocked = true };
            Save(defaultState);
            return defaultState;
        }
    }

    public void Save(AppState state)
    {
        lock (_lock)
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(state, JsonOptions);
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
    }

    public void MarkCompleted(string todayDateString, int durationSeconds = 0)
    {
        lock (_lock)
        {
            var state = Load();
            state.IsLocked = false;
            state.LastCompletedDate = todayDateString;
            state.LastCompletedAt = DateTime.UtcNow;
            state.TodayStats.CorrectAnswers = state.TodayStats.TotalQuestions;
            state.TodayStats.DurationSeconds = durationSeconds;
            Save(state);
        }
    }

    public void MarkUnlockedByOverride(string unlockedBy)
    {
        lock (_lock)
        {
            var state = Load();
            state.IsLocked = false;
            state.LastCompletedDate = DateTime.Now.ToString("yyyy-MM-dd");
            state.LastCompletedAt = DateTime.UtcNow;
            state.ManualOverride = new ManualOverride
            {
                Active = true,
                UnlockedBy = unlockedBy,
                UnlockedAt = DateTime.UtcNow
            };
            Save(state);
        }
    }

    public void ResetLockForNewDay(string todayDateString)
    {
        lock (_lock)
        {
            var state = Load();
            state.IsLocked = true;
            state.TodayStats = new TodayStats();
            state.ManualOverride = new ManualOverride();
            Save(state);
        }
    }

    public void ForceStudy()
    {
        lock (_lock)
        {
            var state = Load();
            state.IsLocked = true;
            state.LastCompletedDate = null;
            state.TodayStats = new TodayStats();
            state.ManualOverride = new ManualOverride
            {
                Active = false,
                UnlockedBy = null,
                UnlockedAt = null
            };
            Save(state);
        }
    }
}
