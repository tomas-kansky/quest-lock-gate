# QuestLock Gate – Odinstalační skript
# Vyžaduje spuštění jako Správce (Administrator)

$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Yellow
Write-Host "       QuestLock Gate – Odinstalace               " -ForegroundColor Yellow
Write-Host "==================================================" -ForegroundColor Yellow

# 1. Kontrola administrátorských práv
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "Tento skript musí být spuštěn jako Správce (Run as Administrator)!"
    exit 1
}

$InstallDir = "$env:ProgramFiles\QuestLock"
$ServiceName = "QuestLock.Guardian"
$FirewallRuleName = "QuestLock Gate Remote Unlock (Port 5001)"

# 2. Zastavení a odstranění služby
Write-Host "`n[1/4] Zastavování a odstraňování Windows Service..." -ForegroundColor Yellow
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -eq "Running") {
        Stop-Service -Name $ServiceName -Force
    }
    sc.exe delete $ServiceName | Out-Null
    Write-Host "  Služba $ServiceName byla smazána."
}

# 3. Ukončení procesů
Write-Host "`n[2/4] Ukončování procesů..." -ForegroundColor Yellow
Get-Process -Name "QuestLock.UI" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "QuestLock.Guardian" -ErrorAction SilentlyContinue | Stop-Process -Force

# 4. Odstranění z autostartu
Write-Host "`n[3/4] Odebírání z automatického spouštění..." -ForegroundColor Yellow
$RunKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run"
Remove-ItemProperty -Path $RunKey -Name "QuestLockGate" -ErrorAction SilentlyContinue

# 5. Odstranění pravidla z Firewallu
Remove-NetFirewallRule -DisplayName $FirewallRuleName -ErrorAction SilentlyContinue

# 6. Odstranění instalačních souborů
Write-Host "`n[4/4] Mazání aplikačních souborů..." -ForegroundColor Yellow
if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  Složka $InstallDir byla odstraněna."
}

Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "   QuestLock Gate byl úspěšně odinstalován.      " -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
