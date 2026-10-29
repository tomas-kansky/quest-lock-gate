using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestLock.Core.Storage;
using Xunit;

namespace QuestLock.Core.Tests;

public class LogManagerTests : IDisposable
{
    private readonly string _testDir;
    private readonly LogManager _logManager;

    public LogManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "QuestLockTestLogs_" + Guid.NewGuid());
        _logManager = new LogManager(_testDir, maxRingBufferSize: 5);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    [Fact]
    public void Log_AddsEntriesAndRespectsRingBufferLimit()
    {
        for (int i = 1; i <= 8; i++)
        {
            _logManager.Info("Test", $"EVENT_{i}", $"Message {i}");
        }

        var recent = _logManager.GetRecentLogs(limit: 10);

        Assert.Equal(5, recent.Count);
        // Poslední záznam je nejnovější na začátku (Reverse)
        Assert.Equal("EVENT_8", recent[0].Event);
        Assert.Equal("EVENT_4", recent[4].Event);
    }

    [Fact]
    public void GetRecentLogs_FiltersByLevelCorrectly()
    {
        _logManager.Info("SourceA", "INFO_EVENT", "Informace");
        _logManager.Warn("SourceB", "WARN_EVENT", "Varování");
        _logManager.Error("SourceC", "ERROR_EVENT", "Chyba");

        var errorsOnly = _logManager.GetRecentLogs(levelFilter: "ERROR");
        var warnsOnly = _logManager.GetRecentLogs(levelFilter: "WARN");

        Assert.Single(errorsOnly);
        Assert.Equal("ERROR_EVENT", errorsOnly[0].Event);

        Assert.Single(warnsOnly);
        Assert.Equal("WARN_EVENT", warnsOnly[0].Event);
    }

    [Fact]
    public void GetRawLogContent_ReturnsFileLogs()
    {
        _logManager.Info("UI", "TEST_RAW", "Testovací záznam pro raw soubor");

        var raw = _logManager.GetRawLogContent();

        Assert.Contains("TEST_RAW", raw);
        Assert.Contains("Testovací záznam pro raw soubor", raw);
    }

    [Fact]
    public void GenerateDiagnosticsBundle_ProducesValidBundle()
    {
        var cfgPath = Path.Combine(_testDir, "config.json");
        var statePath = Path.Combine(_testDir, "state.json");
        var cfg = new ConfigManager(cfgPath);
        var state = new StateManager(statePath);

        _logManager.Info("Test", "DIAG_EVENT", "Diagnostika");

        var bundle = _logManager.GenerateDiagnosticsBundle(cfg, state);

        Assert.NotNull(bundle);
    }
}
