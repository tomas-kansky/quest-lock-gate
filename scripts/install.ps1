# QuestLock Gate - Instalacni skript
# Vyzaduje spusteni jako Spravce (Administrator)

$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "       QuestLock Gate - Jednokrokova instalace    " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Kontrola administratorskych prav
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "Tento skript musi byt spusten jako Spravce (Run as Administrator)!"
    exit 1
}

$InstallDir = "$env:ProgramFiles\QuestLock"
$GuardianDir = "$InstallDir\Guardian"
$UiDir = "$InstallDir\UI"
$DataDir = "$env:ProgramData\QuestLock"
$ServiceName = "QuestLock.Guardian"
$FirewallRuleName = "QuestLock Gate Remote Unlock (Port 5001)"

# 2. Zastaveni existujicich bezicich instanci pred instalaci
Write-Host ""
Write-Host "[1/6] Ukoncovani predchozich verzi..." -ForegroundColor Yellow
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service -and $service.Status -eq "Running") {
    Write-Host "  Zastavuji sluzbu $ServiceName..."
    Stop-Service -Name $ServiceName -Force
    Start-Sleep -Seconds 2
}

Get-Process -Name "QuestLock.UI" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "QuestLock.Guardian" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# 3. Priprava a instalace binarek
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}
if (-not (Test-Path $GuardianDir)) {
    New-Item -ItemType Directory -Path $GuardianDir -Force | Out-Null
}
if (-not (Test-Path $UiDir)) {
    New-Item -ItemType Directory -Path $UiDir -Force | Out-Null
}

Write-Host ""
Write-Host "[2/6] Priprava a kopirovani programovych souboru..." -ForegroundColor Yellow

if ((Test-Path "$ScriptDir\Guardian") -and (Test-Path "$ScriptDir\UI")) {
    Write-Host "  Detekovan hotovy distribucni balicek..."
    Copy-Item -Path "$ScriptDir\Guardian\*" -Destination "$GuardianDir" -Recurse -Force
    Copy-Item -Path "$ScriptDir\UI\*" -Destination "$UiDir" -Recurse -Force
} else {
    Write-Host "  Detekovany zdrojove kody, provadim kompilaci (.NET Publish)..."
    dotnet publish "$ProjectRoot\src\QuestLock.Guardian\QuestLock.Guardian.csproj" -c Release -o "$GuardianDir" --nologo -v q
    dotnet publish "$ProjectRoot\src\QuestLock.UI\QuestLock.UI.csproj" -c Release -o "$UiDir" --nologo -v q
}

# 4. Inicializace datove slozky ProgramData
Write-Host ""
Write-Host "[3/6] Inicializace systemove konfigurace..." -ForegroundColor Yellow
if (-not (Test-Path $DataDir)) {
    New-Item -ItemType Directory -Path $DataDir -Force | Out-Null
}

# 5. Konfigurace Windows Firewallu pro port 5001
Write-Host ""
Write-Host "[4/6] Nastaveni Windows Defender Firewallu..." -ForegroundColor Yellow
Remove-NetFirewallRule -DisplayName $FirewallRuleName -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName $FirewallRuleName `
    -Direction Inbound `
    -LocalPort 5001 `
    -Protocol TCP `
    -Action Allow `
    -Profile Any `
    -Description "Povoleni vzdaleneho odemykani pocitace pro rodice pres lokalni sit (QuestLock Gate)." | Out-Null
Write-Host "  Prichozi TCP port 5001 byl povolen ve Firewallu."

# 6. Registrace a spusteni Windows Service
Write-Host ""
Write-Host "[5/6] Registrace Windows Service..." -ForegroundColor Yellow
$GuardianExe = "$GuardianDir\QuestLock.Guardian.exe"

$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "  Aktualizuji existujici sluzbu $ServiceName..."
    sc.exe config $ServiceName binPath= "`"$GuardianExe`"" start= auto | Out-Null
} else {
    Write-Host "  Vytvarim novou sluzbu $ServiceName..."
    New-Service -Name $ServiceName `
        -BinaryPathName "`"$GuardianExe`"" `
        -DisplayName "QuestLock Gate Guardian Service" `
        -StartupType Automatic `
        -Description "Systemovy hlidac a webovy server pro vzdalene odemykani QuestLock Gate." | Out-Null
}

Start-Service -Name $ServiceName
Write-Host "  Sluzba $ServiceName byla uspesne spustena."

# 7. Nastaveni automatickeho spusteni UI pro uzivatele
Write-Host ""
Write-Host "[6/6] Nastaveni automatickeho spusteni UI..." -ForegroundColor Yellow
$UiExe = "$UiDir\QuestLock.UI.exe"
$RunKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run"
Set-ItemProperty -Path $RunKey -Name "QuestLockGate" -Value "`"$UiExe`""
Write-Host "  Pridan zaznam do registru HKLM Run."

# Spustit UI pro aktualni relaci
Start-Process -FilePath $UiExe

# 8. Zjisteni lokalni IP adresy pro zobrazeni rodicum
$localIp = (Get-NetIPAddress -AddressFamily IPv4 -InterfaceAlias "Wi-Fi*", "Ethernet*" -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -notlike "169.254*" } | Select-Object -First 1).IPAddress
if (-not $localIp) { $localIp = "192.168.x.x" }

Write-Host ""
Write-Host "==================================================" -ForegroundColor Green
Write-Host "   QuestLock Gate byl uspesne nainstalovan!       " -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Write-Host "Sluzba bezi na pozadi a UI hlida prihlaseni i probuzeni ze spanku." -ForegroundColor White
Write-Host ""
Write-Host "Odkazy pro rodice (na domaci Wi-Fi):" -ForegroundColor Cyan
Write-Host "  Hlavni administrace : http://$($localIp):5001/administration" -ForegroundColor Yellow
Write-Host "  Udelit den volna    : http://$($localIp):5001/day-off" -ForegroundColor Yellow
Write-Host "  Okamzite studium    : http://$($localIp):5001/study-now" -ForegroundColor Yellow
Write-Host "  Nastaveni pravidel  : http://$($localIp):5001/settings" -ForegroundColor Yellow
Write-Host ""
Write-Host "Hotovo!" -ForegroundColor Green
