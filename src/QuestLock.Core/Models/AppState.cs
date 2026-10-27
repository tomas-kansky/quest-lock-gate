using System.Text.Json.Serialization;

namespace QuestLock.Core.Models;

public class AppState
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("is_locked")]
    public bool IsLocked { get; set; } = true;

    [JsonPropertyName("last_completed_date")]
    public string? LastCompletedDate { get; set; }

    [JsonPropertyName("last_completed_at")]
    public DateTime? LastCompletedAt { get; set; }

    [JsonPropertyName("today_stats")]
    public TodayStats TodayStats { get; set; } = new();

    [JsonPropertyName("manual_override")]
    public ManualOverride ManualOverride { get; set; } = new();

    public bool IsCompletedToday(string todayDateString)
    {
        return LastCompletedDate == todayDateString;
    }
}

public class TodayStats
{
    [JsonPropertyName("attempts_count")]
    public int AttemptsCount { get; set; } = 0;

    [JsonPropertyName("correct_answers")]
    public int CorrectAnswers { get; set; } = 0;

    [JsonPropertyName("total_questions")]
    public int TotalQuestions { get; set; } = 5;

    [JsonPropertyName("duration_seconds")]
    public int DurationSeconds { get; set; } = 0;
}

public class ManualOverride
{
    [JsonPropertyName("active")]
    public bool Active { get; set; } = false;

    [JsonPropertyName("unlocked_by")]
    public string? UnlockedBy { get; set; }

    [JsonPropertyName("unlocked_at")]
    public DateTime? UnlockedAt { get; set; }
}
