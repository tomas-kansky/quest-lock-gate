using System.Text.Json.Serialization;

namespace QuestLock.Core.Models;

public class AppConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("server")]
    public ServerConfig Server { get; set; } = new();

    [JsonPropertyName("rules")]
    public RulesConfig Rules { get; set; } = new();

    [JsonPropertyName("math")]
    public MathConfig Math { get; set; } = new();
}

public class ServerConfig
{
    [JsonPropertyName("port")]
    public int Port { get; set; } = 5001;

    [JsonPropertyName("admin_pin_hash")]
    public string AdminPinHash { get; set; } = "a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3"; // výchozí: 1234

    [JsonPropertyName("admin_pin_salt")]
    public string AdminPinSalt { get; set; } = "c29tZV9yYW5kb21fc2FsdA==";

    [JsonPropertyName("quick_unlock_key")]
    public string QuickUnlockKey { get; set; } = string.Empty;
}

public class RulesConfig
{
    [JsonPropertyName("daily_gatekeeper_enabled")]
    public bool DailyGatekeeperEnabled { get; set; } = true;

    [JsonPropertyName("auto_update_enabled")]
    public bool AutoUpdateEnabled { get; set; } = false;

    [JsonPropertyName("github_repo_url")]
    public string GithubRepoUrl { get; set; } = "https://github.com/tomas-kansky/quest-lock-gate";
}

public class MathConfig
{
    [JsonPropertyName("total_questions")]
    public int TotalQuestions { get; set; } = 5;

    [JsonPropertyName("max_number")]
    public int MaxNumber { get; set; } = 100;

    [JsonPropertyName("allowed_operations")]
    public List<string> AllowedOperations { get; set; } = new() { "add", "subtract", "multiply", "divide" };

    [JsonPropertyName("allow_negative_results")]
    public bool AllowNegativeResults { get; set; } = false;
}
