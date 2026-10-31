using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using QuestLock.Core.Common;

namespace QuestLock.Core.Updates;

public class UpdateManager
{
    private readonly HttpClient _httpClient;
    private readonly string _updatesDirectory;

    public UpdateManager(HttpClient? httpClient = null, string? customUpdatesDirectory = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "QuestLock-AutoUpdater");
        }

        if (!string.IsNullOrEmpty(customUpdatesDirectory))
        {
            _updatesDirectory = customUpdatesDirectory;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _updatesDirectory = Path.Combine(appData, "QuestLock", "updates");
        }
    }

    public string UpdatesDirectory => _updatesDirectory;

    public static bool IsNewerVersion(string currentVersionStr, string remoteVersionStr)
    {
        var cleanCurrent = currentVersionStr.Trim().TrimStart('v', 'V');
        var cleanRemote = remoteVersionStr.Trim().TrimStart('v', 'V');

        // Normalizovat na 3 složky (Major.Minor.Build), pokud je zadáno jen např. 1.1
        var curParts = cleanCurrent.Split('.');
        var remParts = cleanRemote.Split('.');

        if (Version.TryParse(cleanCurrent, out var curVer) && Version.TryParse(cleanRemote, out var remVer))
        {
            return remVer > curVer;
        }

        // Fallback pro nestandardní formáty
        int minLen = System.Math.Min(curParts.Length, remParts.Length);
        for (int i = 0; i < minLen; i++)
        {
            if (int.TryParse(remParts[i], out int rPart) && int.TryParse(curParts[i], out int cPart))
            {
                if (rPart > cPart) return true;
                if (rPart < cPart) return false;
            }
        }

        return remParts.Length > curParts.Length;
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(
        string? repoUrl = null, 
        string? currentVersion = null, 
        CancellationToken cancellationToken = default)
    {
        var curVer = !string.IsNullOrWhiteSpace(currentVersion) ? currentVersion : AppInfo.Version;
        var repoSlug = ParseGitHubRepoSlug(repoUrl ?? AppInfo.DefaultGithubRepo);

        var result = new UpdateCheckResult
        {
            CurrentVersion = curVer
        };

        try
        {
            var apiUrl = $"https://api.github.com/repos/{repoSlug}/releases/latest";
            using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"GitHub API vrátilo kód {(int)response.StatusCode}: {response.ReasonPhrase}";
                return result;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var releaseDto = JsonSerializer.Deserialize<GitHubReleaseDto>(json);

            if (releaseDto == null || string.IsNullOrEmpty(releaseDto.TagName))
            {
                result.ErrorMessage = "Odpověď GitHub API neobsahuje platný tag verze.";
                return result;
            }

            var asset = releaseDto.Assets?.FirstOrDefault(a => 
                string.Equals(a.Name, "QuestLock-Setup.zip", StringComparison.OrdinalIgnoreCase));

            if (asset == null || string.IsNullOrEmpty(asset.BrowserDownloadUrl))
            {
                result.ErrorMessage = $"Release {releaseDto.TagName} neobsahuje soubor QuestLock-Setup.zip.";
                return result;
            }

            var remoteVer = releaseDto.TagName.TrimStart('v', 'V');
            bool isNewer = IsNewerVersion(curVer, releaseDto.TagName);

            result.LatestVersion = remoteVer;
            result.IsUpdateAvailable = isNewer;
            result.ReleaseInfo = new ReleaseInfo
            {
                TagName = releaseDto.TagName,
                Version = remoteVer,
                Name = releaseDto.Name,
                ReleaseNotes = releaseDto.Body,
                PublishedAt = releaseDto.PublishedAt,
                DownloadUrl = asset.BrowserDownloadUrl,
                AssetSize = asset.Size,
                IsNewer = isNewer
            };
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Chyba při kontrole aktualizací: {ex.Message}";
        }

        return result;
    }

    public async Task<string> DownloadAndPrepareUpdateAsync(
        ReleaseInfo release, 
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_updatesDirectory))
        {
            Directory.CreateDirectory(_updatesDirectory);
        }

        var zipPath = Path.Combine(_updatesDirectory, "QuestLock-Setup.zip");
        var extractedDir = Path.Combine(_updatesDirectory, "extracted");

        // 1. Vyčistit předchozí stažené soubory
        if (File.Exists(zipPath))
        {
            try { File.Delete(zipPath); } catch { }
        }
        if (Directory.Exists(extractedDir))
        {
            try { Directory.Delete(extractedDir, recursive: true); } catch { }
        }

        // 2. Stáhnout QuestLock-Setup.zip
        using (var response = await _httpClient.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();

            using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await sourceStream.CopyToAsync(fileStream, cancellationToken);
        }

        // 3. Rozbalit balíček
        ZipFile.ExtractToDirectory(zipPath, extractedDir, overwriteFiles: true);

        return extractedDir;
    }

    public bool LaunchUpdateWorker(string extractedDir, string? installDir = null)
    {
        var targetDir = !string.IsNullOrEmpty(installDir)
            ? installDir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "QuestLock");

        // Hledat update-worker.ps1 nejdříve v rozbaleném balíčku, fallback v cílové složce
        var workerScript = Path.Combine(extractedDir, "update-worker.ps1");
        if (!File.Exists(workerScript))
        {
            workerScript = Path.Combine(targetDir, "update-worker.ps1");
        }

        if (!File.Exists(workerScript))
        {
            throw new FileNotFoundException($"Skript update-worker.ps1 nebyl nalezen v '{extractedDir}' ani v '{targetDir}'.");
        }

        var arguments = $"-ExecutionPolicy Bypass -NoProfile -File \"{workerScript}\" -SourceDir \"{extractedDir}\" -TargetDir \"{targetDir}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        var process = Process.Start(startInfo);
        return process != null;
    }

    private static string ParseGitHubRepoSlug(string urlOrSlug)
    {
        if (string.IsNullOrWhiteSpace(urlOrSlug)) return AppInfo.DefaultGithubRepo;

        var trimmed = urlOrSlug.Trim();
        if (trimmed.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring("https://github.com/".Length).Trim('/');
        }
        else if (trimmed.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring("http://github.com/".Length).Trim('/');
        }

        return trimmed;
    }
}
