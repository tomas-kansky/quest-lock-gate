using QuestLock.Core.Models;
using QuestLock.Core.Storage;
using QuestLock.Core.Updates;

namespace QuestLock.Guardian.Services;

public class UpdateService : BackgroundService
{
    private readonly UpdateManager _updateManager;
    private readonly ConfigManager _configManager;
    private readonly LogManager _logManager;
    private readonly IpcServerService _ipcServerService;
    private readonly ILogger<UpdateService> _logger;

    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private DateTime _lastAutoCheckTime = DateTime.MinValue;

    public UpdateCheckResult? LastCheckResult { get; private set; }
    public DateTime? LastCheckTime { get; private set; }
    public bool IsUpdating { get; private set; }
    public string? UpdateStatusMessage { get; private set; }

    public UpdateService(
        UpdateManager updateManager,
        ConfigManager configManager,
        LogManager logManager,
        IpcServerService ipcServerService,
        ILogger<UpdateService> logger)
    {
        _updateManager = updateManager;
        _configManager = configManager;
        _logManager = logManager;
        _ipcServerService = ipcServerService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UpdateService byl spuštěn. Poslouchám události odemčení pro automatické aktualizace.");
        _ipcServerService.OnUnlocked += HandleUnlockEvent;

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Běžné ukončení služby
        }
        finally
        {
            _ipcServerService.OnUnlocked -= HandleUnlockEvent;
        }
    }

    private void HandleUnlockEvent(string source)
    {
        // Spustit asynchronně na pozadí, abychom neblokovali IPC server
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessUnlockUpdateCheckAsync(source);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chyba při zpracování automatické aktualizace po odemčení ({Source})", source);
                _logManager.Error("Update", "AUTO_UPDATE_ERROR", $"Chyba při automatické kontrole aktualizace: {ex.Message}");
            }
        });
    }

    private async Task ProcessUnlockUpdateCheckAsync(string source)
    {
        if (IsUpdating)
        {
            _logger.LogDebug("Auto-update kontrola přeskočena: aktualizace již probíhá.");
            return;
        }

        var config = _configManager.Load();
        if (!config.Rules.AutoUpdateEnabled)
        {
            _logger.LogDebug("Auto-update kontrola po odemčení přeskočena: v konfiguraci je automatická aktualizace vypnuta.");
            return;
        }

        // Ochrana proti příliš častému dotazování GitHub API (cooldown 30 minut)
        if (LastCheckTime.HasValue && (DateTime.UtcNow - _lastAutoCheckTime).TotalMinutes < 30 && LastCheckResult?.IsUpdateAvailable != true)
        {
            _logger.LogDebug("Auto-update kontrola přeskočena kvůli cooldownu (poslední kontrola před < 30 min).");
            return;
        }

        _logger.LogInformation("Detekováno odemčení ({Source}). Vyčkávám 5 sekund na stabilizaci sítě...", source);
        await Task.Delay(5000);

        _lastAutoCheckTime = DateTime.UtcNow;
        _logManager.Info("Update", "UNLOCK_CHECK", $"Detekováno odemčení ({source}). Spouštím kontrolu dostupné verze na GitHubu.");

        var result = await CheckForUpdateAsync();
        if (result.IsUpdateAvailable && result.ReleaseInfo != null)
        {
            _logger.LogInformation("Nalezena nová verze {Version}. Spouštím automatické stažení a instalaci!", result.LatestVersion);
            _logManager.Info("Update", "AUTO_UPDATE_START", $"Detekována nová verze {result.LatestVersion} (aktuální: {result.CurrentVersion}). Zahajuji automatickou aktualizaci.");

            var (success, message) = await ApplyUpdateAsync();
            if (!success)
            {
                _logger.LogWarning("Automatická aktualizace selhala: {Message}", message);
                _logManager.Warn("Update", "AUTO_UPDATE_FAILED", $"Automatická aktualizace selhala: {message}");
            }
        }
        else
        {
            _logger.LogInformation("Kontrola dokončena. Aplikace je aktuální (verze {Version}).", result.CurrentVersion);
        }
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var config = _configManager.Load();
        var repoUrl = config.Rules.GithubRepoUrl;

        _logger.LogInformation("Kontroluji aktualizace z GitHub repozitáře: {RepoUrl}...", repoUrl);
        var result = await _updateManager.CheckForUpdateAsync(repoUrl, null, cancellationToken);

        LastCheckResult = result;
        LastCheckTime = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            _logger.LogWarning("Chyba při kontrole aktualizací: {ErrorMessage}", result.ErrorMessage);
            _logManager.Warn("Update", "CHECK_ERROR", $"Chyba při kontrole aktualizací: {result.ErrorMessage}");
        }
        else if (result.IsUpdateAvailable)
        {
            _logger.LogInformation("Je k dispozici nová verze: {LatestVersion} (aktuální: {CurrentVersion})", result.LatestVersion, result.CurrentVersion);
            _logManager.Info("Update", "UPDATE_AVAILABLE", $"Dostupná nová verze: {result.LatestVersion} (stávající: {result.CurrentVersion})");
        }
        else
        {
            _logger.LogInformation("Není dostupná žádná novější verze. Aktuální verze: {CurrentVersion}", result.CurrentVersion);
        }

        return result;
    }

    public async Task<(bool success, string message)> ApplyUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (!await _updateLock.WaitAsync(0, cancellationToken))
        {
            return (false, "Aktualizace již probíhá na pozadí.");
        }

        try
        {
            IsUpdating = true;

            var release = LastCheckResult?.ReleaseInfo;
            if (release == null)
            {
                // Zkusíme ještě rychle zkontrolovat
                var check = await CheckForUpdateAsync(cancellationToken);
                release = check.ReleaseInfo;
            }

            if (release == null || string.IsNullOrEmpty(release.DownloadUrl))
            {
                UpdateStatusMessage = "Není k dispozici žádný balíček ke stažení.";
                return (false, UpdateStatusMessage);
            }

            UpdateStatusMessage = $"Stahování aktualizace {release.Version}...";
            _logManager.Info("Update", "DOWNLOAD_START", $"Stahování aktualizace {release.Version} z {release.DownloadUrl}");

            var extractedDir = await _updateManager.DownloadAndPrepareUpdateAsync(release, cancellationToken);

            UpdateStatusMessage = "Spouštění aktualizačního workeru...";
            _logManager.Info("Update", "WORKER_START", $"Instalační soubory připraveny v '{extractedDir}'. Spouštím update-worker.ps1.");

            var launched = _updateManager.LaunchUpdateWorker(extractedDir);
            if (!launched)
            {
                throw new InvalidOperationException("Nepodařilo se spustit proces update-worker.ps1.");
            }

            UpdateStatusMessage = "Aktualizace byla úspěšně spuštěna na pozadí. Služba bude restartována.";
            _logManager.Info("Update", "WORKER_LAUNCHED", "Aktualizační skript byl úspěšně spuštěn na pozadí.");

            return (true, "Aktualizace byla úspěšně zahájena na pozadí. Systémová služba a Kiosk se restartují.");
        }
        catch (Exception ex)
        {
            UpdateStatusMessage = $"Chyba při aktualizaci: {ex.Message}";
            _logger.LogError(ex, "Chyba při spouštění aktualizace");
            _logManager.Error("Update", "APPLY_ERROR", $"Chyba při spouštění aktualizace: {ex.Message}");
            return (false, ex.Message);
        }
        finally
        {
            IsUpdating = false;
            _updateLock.Release();
        }
    }
}
