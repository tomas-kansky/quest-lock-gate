using System;
using System.IO;
using QuestLock.Core.Models;
using QuestLock.Core.Storage;
using Xunit;

namespace QuestLock.Core.Tests;

public class StorageTests : IDisposable
{
    private readonly string _tempDir;

    public StorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "QuestLockTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void ConfigManager_LoadsDefaults_WhenFileMissing()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);

        var config = manager.Load();

        Assert.NotNull(config);
        Assert.Equal(5, config.Math.TotalQuestions);
        Assert.Equal(100, config.Math.MaxNumber);
        Assert.True(File.Exists(configPath));
    }

    [Fact]
    public void StateManager_MarksCompleted_AndDetectsToday()
    {
        var statePath = Path.Combine(_tempDir, "state.json");
        var manager = new StateManager(statePath);

        var state = manager.Load();
        Assert.True(state.IsLocked);

        var today = "2026-10-27";
        manager.MarkCompleted(today, durationSeconds: 65);

        var updated = manager.Load();
        Assert.False(updated.IsLocked);
        Assert.Equal(today, updated.LastCompletedDate);
        Assert.True(updated.IsCompletedToday(today));
        Assert.False(updated.IsCompletedToday("2026-10-28"));
    }

    [Fact]
    public void StateManager_MarksUnlockedByOverride()
    {
        var statePath = Path.Combine(_tempDir, "state.json");
        var manager = new StateManager(statePath);

        manager.MarkUnlockedByOverride("mamka");

        var updated = manager.Load();
        Assert.False(updated.IsLocked);
        Assert.True(updated.ManualOverride.Active);
        Assert.Equal("mamka", updated.ManualOverride.UnlockedBy);
    }

    [Fact]
    public void StateManager_ForceStudy_ResetsCompletedState()
    {
        var statePath = Path.Combine(_tempDir, "state.json");
        var manager = new StateManager(statePath);

        var today = "2026-10-29";
        manager.MarkCompleted(today, durationSeconds: 50);

        var completedState = manager.Load();
        Assert.False(completedState.IsLocked);
        Assert.True(completedState.IsCompletedToday(today));

        // Aktivace okamžitého studia
        manager.ForceStudy();

        var forcedState = manager.Load();
        Assert.True(forcedState.IsLocked);
        Assert.Null(forcedState.LastCompletedDate);
        Assert.False(forcedState.IsCompletedToday(today));
    }
}
