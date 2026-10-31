using QuestLock.Core.Common;
using QuestLock.Core.Models;
using QuestLock.Core.Updates;
using Xunit;

namespace QuestLock.Core.Tests;

public class UpdateManagerTests
{
    [Theory]
    [InlineData("1.1.0", "1.2.0", true)]
    [InlineData("1.1.0", "2.0.0", true)]
    [InlineData("1.1.0", "1.1.1", true)]
    [InlineData("v1.1.0", "v1.2.0", true)]
    [InlineData("v1.1.0", "1.2.0", true)]
    [InlineData("1.1.0", "v1.2.0", true)]
    [InlineData("1.1", "1.2", true)]
    [InlineData("1.1.0", "1.1.0", false)]
    [InlineData("v1.1.0", "v1.1.0", false)]
    [InlineData("1.2.0", "1.1.0", false)]
    [InlineData("1.1.5", "1.1.0", false)]
    [InlineData("2.0.0", "1.9.9", false)]
    public void IsNewerVersion_CorrectlyComparesVersions(string current, string remote, bool expected)
    {
        var result = UpdateManager.IsNewerVersion(current, remote);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AppConfig_DefaultRules_HasAutoUpdateEnabled()
    {
        var config = new AppConfig();
        Assert.True(config.Rules.AutoUpdateEnabled);
        Assert.Equal("https://github.com/tomas-kansky/quest-lock-gate", config.Rules.GithubRepoUrl);
    }

    [Fact]
    public void AppInfo_ContainsValidInformation()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.Version));
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.DefaultGithubRepo));
        Assert.Equal("1.1.0", AppInfo.Version);
        Assert.Equal("tomas-kansky/quest-lock-gate", AppInfo.DefaultGithubRepo);
    }

    [Fact]
    public void UpdateCheckResult_DefaultsAreExpected()
    {
        var result = new UpdateCheckResult
        {
            CurrentVersion = "1.1.0",
            LatestVersion = "1.2.0",
            IsUpdateAvailable = true
        };

        Assert.Equal("1.1.0", result.CurrentVersion);
        Assert.Equal("1.2.0", result.LatestVersion);
        Assert.True(result.IsUpdateAvailable);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.ReleaseInfo);
    }
}
