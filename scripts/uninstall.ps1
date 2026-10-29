# QuestLock Gate - Odinstalacni skript
# Vyzaduje spusteni jako Spravce (Administrator)

$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Yellow
Write-Host "       QuestLock Gate - Odinstalace               " -ForegroundColor Yellow
Write-Host "==================================================" -ForegroundColor Yellow

# 1. Kontrola administratorskych prav
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "Tento skript musi byt spusten jako Spravce (Run as Administrator)!"
    exit 1
}

$InstallDir = "$env:ProgramFiles\QuestLock"
$ServiceName = "QuestLock.Guardian"
$FirewallRuleName = "QuestLock Gate Remote Unlock (Port 5001)"

# 2. Zastaveni a odstraneni sluzby
Write-Host ""
Write-Host "[1/4] Zastavovani a odstranovani Windows Service..." -ForegroundColor Yellow
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -eq "Running") {
        Stop-Service -Name $ServiceName -Force
    }
    sc.exe delete $ServiceName | Out-Null
    Write-Host "  Sluzba $ServiceName byla smazana."
}

# 3. Ukonceni procesu
Write-Host ""
Write-Host "[2/4] Ukoncovani procesu..." -ForegroundColor Yellow
Get-Process -Name "QuestLock.UI" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "QuestLock.Guardian" -ErrorAction SilentlyContinue | Stop-Process -Force

# 4. Odstraneni z autostartu
Write-Host ""
Write-Host "[3/4] Odebirani z automatickeho spousteni..." -ForegroundColor Yellow
$RunKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run"
Remove-ItemProperty -Path $RunKey -Name "QuestLockGate" -ErrorAction SilentlyContinue

# 5. Odstraneni pravidla z Firewallu
Remove-NetFirewallRule -DisplayName $FirewallRuleName -ErrorAction SilentlyContinue

# 6. Odstraneni instalacnich souboru
Write-Host ""
Write-Host "[4/4] Mazani aplikacnich souboru..." -ForegroundColor Yellow
if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  Slozka $InstallDir byla odstranena."
}

Write-Host ""
Write-Host "==================================================" -ForegroundColor Green
Write-Host "   QuestLock Gate byl uspesne odinstalovan.      " -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
