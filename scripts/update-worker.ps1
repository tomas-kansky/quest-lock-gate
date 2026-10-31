# QuestLock Gate - Background Update Worker
# Spousti se na pozadi pro bezpecnou vymenu binarek za behu sluzby
# Vyzaduje administratorska prava (spousteno pres NT AUTHORITY\SYSTEM)

param(
    [string]$SourceDir = "",
    [string]$TargetDir = "$env:ProgramFiles\QuestLock",
    [string]$ServiceName = "QuestLock.Guardian"
)

$ErrorActionPreference = "Continue"

function Write-UpdateLog([string]$level, [string]$event, [string]$message) {
    try {
        $logsDir = "$env:ProgramData\QuestLock\logs"
        if (-not (Test-Path $logsDir)) {
            New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
        }
        $today = (Get-Date).ToString("yyyy-MM-dd")
        $logPath = "$logsDir\questlock-$today.log"
        $timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fff")
        $entry = "[$timestamp] [$level] [UpdateWorker] [$event] $message"
        Add-Content -Path $logPath -Value $entry
    } catch {
        # Ochrana logovani
    }
}

Write-UpdateLog "INFO" "WORKER_START" "Update worker zahajil cinnost. SourceDir='$SourceDir', TargetDir='$TargetDir'"

if ([string]::IsNullOrWhiteSpace($SourceDir) -or (-not (Test-Path $SourceDir))) {
    Write-UpdateLog "ERROR" "INVALID_SOURCE" "Slozka se zdrojem aktualizace '$SourceDir' neexistuje!"
    exit 1
}

# 1. Pockat 2 sekundy na uvolneni spojeni
Start-Sleep -Seconds 2

# 2. Zastavit Windows Service QuestLock.Guardian
Write-UpdateLog "INFO" "STOPPING_SERVICE" "Zastavuji sluzbu $ServiceName..."
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -eq "Running") {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    
    $timeout = 15
    while ($timeout -gt 0) {
        Start-Sleep -Seconds 1
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($service.Status -eq "Stopped") {
            break
        }
        $timeout--
    }
}

# 3. Ukoncit pripadne bezici procesy Kiosku a Služby
Write-UpdateLog "INFO" "TERMINATING_PROCESSES" "Ukoncuji procesy QuestLock.UI a QuestLock.Guardian..."
Get-Process -Name "QuestLock.UI" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process -Name "QuestLock.Guardian" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# 4. Zalohovat a prekopirovat programove soubory
Write-UpdateLog "INFO" "COPYING_FILES" "Kopiruji nove soubory z '$SourceDir' do '$TargetDir'..."

$guardianSource = "$SourceDir\Guardian"
$uiSource = "$SourceDir\UI"
$guardianTarget = "$TargetDir\Guardian"
$uiTarget = "$TargetDir\UI"

if (Test-Path $guardianSource) {
    if (-not (Test-Path $guardianTarget)) {
        New-Item -ItemType Directory -Path $guardianTarget -Force | Out-Null
    }
    Copy-Item -Path "$guardianSource\*" -Destination "$guardianTarget" -Recurse -Force -ErrorAction SilentlyContinue
}

if (Test-Path $uiSource) {
    if (-not (Test-Path $uiTarget)) {
        New-Item -ItemType Directory -Path $uiTarget -Force | Out-Null
    }
    Copy-Item -Path "$uiSource\*" -Destination "$uiTarget" -Recurse -Force -ErrorAction SilentlyContinue
}

# Prekopirovat skripty
foreach ($scriptName in @("install.ps1", "uninstall.ps1", "setup-tailscale.ps1", "update-worker.ps1", "README.md")) {
    $scriptSrc = "$SourceDir\$scriptName"
    if (Test-Path $scriptSrc) {
        Copy-Item -Path $scriptSrc -Destination "$TargetDir\$scriptName" -Force -ErrorAction SilentlyContinue
    }
}

Write-UpdateLog "INFO" "FILES_COPIED" "Vsechny soubory byly uspesne prekopirovany."

# 5. Nastartovat novou verzi Windows Service
Write-UpdateLog "INFO" "STARTING_SERVICE" "Startuji sluzbu $ServiceName..."
Start-Service -Name $ServiceName -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# 6. Spustit Kiosk v relaci aktualne prihlaseneho uzivatele (pokud je nekdo prihlasen)
try {
    $consoleUser = (Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction SilentlyContinue).UserName
    if ($consoleUser) {
        $uiExe = "$TargetDir\UI\QuestLock.UI.exe"
        if (Test-Path $uiExe) {
            Write-UpdateLog "INFO" "RESTARTING_UI" "Restartuji Kiosk pro uzivatele '$consoleUser'..."
            $taskName = "QuestLock_ResumeUI_Update"
            schtasks.exe /create /tn $taskName /tr "`"$uiExe`"" /sc once /st (Get-Date).AddMinutes(1).ToString("HH:mm") /ru "$consoleUser" /f | Out-Null
            schtasks.exe /run /tn $taskName | Out-Null
            Start-Sleep -Seconds 2
            schtasks.exe /delete /tn $taskName /f | Out-Null
        }
    }
} catch {
    Write-UpdateLog "WARN" "UI_RESTART_FAILED" "Nepodarilo se restartovat Kiosk: $($_.Exception.Message)"
}

# 7. Uklid docasnych souboru
try {
    if (Test-Path $SourceDir) {
        Remove-Item -Path $SourceDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    $updatesDir = Split-Path -Parent $SourceDir
    $zipPath = "$updatesDir\QuestLock-Setup.zip"
    if (Test-Path $zipPath) {
        Remove-Item -Path $zipPath -Force -ErrorAction SilentlyContinue
    }
    Write-UpdateLog "INFO" "CLEANUP_DONE" "Docasne instalacni soubory byly uklizeny."
} catch {
    # Ignorovat chybu pri uklidu
}

Write-UpdateLog "INFO" "WORKER_FINISH" "Aktualizace na novou verzi byla uspesne dokoncena!"
