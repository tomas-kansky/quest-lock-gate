using System.Text.Json.Serialization;
using QuestLock.Core.Models;

namespace QuestLock.Core.Ipc;

public static class IpcConstants
{
    public const string PipeName = "QuestLockPipe";
}

public class IpcMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public string? Payload { get; set; }
}

public static class IpcMessageTypes
{
    public const string GetInitData = "GET_INIT_DATA";
    public const string InitDataResponse = "INIT_DATA_RESPONSE";
    public const string SubmitCompletion = "SUBMIT_COMPLETION";
    public const string UnlockGranted = "UNLOCK_GRANTED";
    public const string ForceLock = "FORCE_LOCK";
    public const string Heartbeat = "HEARTBEAT";
}

public class InitDataPayload
{
    [JsonPropertyName("is_locked_required")]
    public bool IsLockedRequired { get; set; }

    [JsonPropertyName("math_config")]
    public MathConfig MathConfig { get; set; } = new();

    [JsonPropertyName("today_date")]
    public string TodayDate { get; set; } = string.Empty;
}

public class SubmitCompletionPayload
{
    [JsonPropertyName("correct_answers")]
    public int CorrectAnswers { get; set; }

    [JsonPropertyName("total_questions")]
    public int TotalQuestions { get; set; }

    [JsonPropertyName("duration_seconds")]
    public int DurationSeconds { get; set; }
}

public class UnlockGrantedPayload
{
    [JsonPropertyName("unlocked_at")]
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("unlocked_by")]
    public string UnlockedBy { get; set; } = "SYSTEM";
}
