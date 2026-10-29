using System.Text.Json.Serialization;

namespace QuestLock.Core.Models;

public class LogEntry
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("level")]
    public string Level { get; set; } = "INFO";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "Guardian";

    [JsonPropertyName("event")]
    public string Event { get; set; } = "GENERAL";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public Dictionary<string, string>? Details { get; set; }

    public override string ToString()
    {
        var localTime = Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
        var detailsStr = Details != null && Details.Count > 0 
            ? " | " + string.Join(", ", Details.Select(kv => $"{kv.Key}={kv.Value}")) 
            : "";
        return $"[{localTime}] [{Level}] [{Source}] [{Event}] {Message}{detailsStr}";
    }
}
