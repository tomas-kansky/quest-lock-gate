# QuestLock Gate – Jednokrokový instalační skript
# Vyžaduje spuštění jako Správce (Administrator)

$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "       QuestLock Gate – Jednokroková instalace     " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Kontrola administrátorských práv
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "Tento skript musí být spuštěn jako Správce (Run as Administrator)!"
    exit 1
}

$InstallDir = "$env:ProgramFiles\QuestLock"
$GuardianDir = "$InstallDir\Guardian"
$UiDir = "$InstallDir\UI"
$DataDir = "$env:ProgramData\QuestLock"
$ServiceName = "QuestLock.Guardian"
$FirewallRuleName = "QuestLock Gate Remote Unlock (Port 5001)"

# 2. Zastavení existujících běžících instancí před instalací
Write-Host "`n[1/6] Ukončování předchozích verzí..." -ForegroundColor Yellow
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -eq "Running") {
    Write-Host "  Zastavuji službu $ServiceName..."
    Stop-Service -Name $ServiceName -Force
    Start-Sleep -Seconds 2
}

Get-Process -Name "QuestLock.UI" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "QuestLock.Guardian" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# 3. Sestavení a publikace aplikací (.NET Publish)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

Write-Host "`n[2/6] Sestavování a publikace komponent..." -ForegroundColor Yellow
Write-Host "  Publikuji QuestLock.Guardian (Služba + Web)..."
dotnet publish "$ProjectRoot\src\QuestLock.Guardian\QuestLock.Guardian.csproj" -c Release -o "$GuardianDir" --nologo -v q

Write-Host "  Publikuji QuestLock.UI (Klientská aplikace)..."
dotnet publish "$ProjectRoot\src\QuestLock.UI\QuestLock.UI.csproj" -c Release -o "$UiDir" --nologo -v q

# 4. Inicializace datové složky ProgramData
Write-Host "`n[3/6] Inicializace systémové konfigurace..." -ForegroundColor Yellow
if (-not (Test-Path $DataDir)) {
    New-Item -ItemType Directory -Path $DataDir -Force | Out-Null
}

# 5. Konfigurace Windows Firewallu pro port 5001
Write-Host "`n[4/6] Nastavení Windows Defender Firewallu..." -ForegroundColor Yellow
Remove-NetFirewallRule -DisplayName $FirewallRuleName -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName $FirewallRuleName `
    -Direction Inbound `
    -LocalPort 5001 `
    -Protocol TCP `
    -Action Allow `
    -Profile Any `
    -Description "Povolení vzdáleného odemykání počítače pro rodiče přes lokální síť (QuestLock Gate)." | Out-Null
Write-Host "  Příchozí TCP port 5001 byl povolen ve Firewallu."

# 6. Registrace a spuštění Windows Service
Write-Host "`n[5/6] Registrace Windows Service..." -ForegroundColor Yellow
$GuardianExe = "$GuardianDir\QuestLock.Guardian.exe"

$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "  Aktualizuji existující službu $ServiceName..."
    sc.exe config $ServiceName binPath= "`"$GuardianExe`"" start= auto | Out-Null
} else {
    Write-Host "  Vytvářím novou službu $ServiceName..."
    New-Service -Name $ServiceName `
        -BinaryPathName "`"$GuardianExe`"" `
        -DisplayName "QuestLock Gate Guardian Service" `
        -StartupType Automatic `
        -Description "Systémový hlídač a webový server pro vzdálené odemykání QuestLock Gate." | Out-Null
}

Start-Service -Name $ServiceName
Write-Host "  Služba $ServiceName byla úspěšně spuštěna."

# 7. Nastavení automatického spuštění UI pro uživatele
Write-Host "`n[6/6] Nastavení automatického spuštění UI..." -ForegroundColor Yellow
$UiExe = "$UiDir\QuestLock.UI.exe"
$RunKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run"
Set-ItemProperty -Path $RunKey -Name "QuestLockGate" -Value "`"$UiExe`""
Write-Host "  Přidán záznam do registru HKLM Run."

# Spustit UI pro aktuální relaci
Start-Process -FilePath $UiExe

# 8. Zjištění lokální IP adresy pro zobrazení rodičům
$localIp = (Get-NetIPAddress -AddressFamily IPv4 -InterfaceAlias "Wi-Fi*", "Ethernet*" -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -notlike "169.254*" } | Select-Object -First 1).IPAddress
if (-not $localIp) { $localIp = "192.168.x.x" }

Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "   QuestLock Gate byl úspěšně nainstalován!       " -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Write-Host "Služba běží na pozadí a UI hlídá přihlášení i probuzení ze spánku." -ForegroundColor White
Write-Host "`nOdkaz pro odemčení z mobilu mamky:" -ForegroundColor Cyan
Write-Host "  http://$($localIp):5001/unlock?key=mamka" -ForegroundColor Yellow -BackgroundColor Black
Write-Host "`nHotovo!`n" -ForegroundColor Green
