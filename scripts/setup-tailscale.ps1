# QuestLock Gate - Tailscale Remote Access Setup
# Vyzaduje spusteni jako Spravce (Administrator)

param(
    [string]$AuthKey = "",
    [string]$Hostname = "questlock-pc"
)

$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "   QuestLock Gate - Nastaveni Tailscale VPN       " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Kontrola administratorskych prav
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "Tento skript musi byt spusten jako Spravce (Run as Administrator)!"
    exit 1
}

# 2. Ziskani Auth klice
if ([string]::IsNullOrWhiteSpace($AuthKey)) {
    Write-Host ""
    Write-Host "Pro automaticke sparovani je potreba Tailscale Auth Key (tskey-auth-...)." -ForegroundColor Yellow
    Write-Host "Vygenerujete jej v administraci na https://login.tailscale.com/admin/settings/keys" -ForegroundColor White
    Write-Host ""
    $AuthKey = Read-Host "Zadejte Tailscale Auth Key"
}

if ([string]::IsNullOrWhiteSpace($AuthKey)) {
    Write-Error "Nebyl zadan platny Tailscale Auth Key. Instalace byla zrusena."
    exit 1
}

$AuthKey = $AuthKey.Trim()

# 3. Kontrola nebo instalace Tailscale
$TailscaleDir = "$env:ProgramFiles\Tailscale"
$TailscaleExe = "$TailscaleDir\tailscale.exe"

if (-not (Test-Path $TailscaleExe)) {
    Write-Host ""
    Write-Host "[1/3] Stahuji oficialni instalator Tailscale..." -ForegroundColor Yellow
    $InstallerUrl = "https://pkgs.tailscale.com/stable/tailscale-setup-latest.exe"
    $TempInstaller = "$env:TEMP\tailscale-setup.exe"

    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $InstallerUrl -OutFile $TempInstaller -UseBasicParsing

    Write-Host "[2/3] Instaluji Tailscale (ticha instalace na pozadi)..." -ForegroundColor Yellow
    $process = Start-Process -FilePath $TempInstaller -ArgumentList "/quiet /norestart" -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Write-Error "Instalace Tailscale selhala s kodem: $($process.ExitCode)"
        exit 1
    }

    Start-Sleep -Seconds 3
    Remove-Item $TempInstaller -Force -ErrorAction SilentlyContinue
} else {
    Write-Host ""
    Write-Host "Tailscale je jiz na pocitaci nainstalovan." -ForegroundColor Green
}

# 4. Overeni existence sluzby a spusteni
$tsService = Get-Service -Name "Tailscale" -ErrorAction SilentlyContinue
if ($tsService -and $tsService.Status -ne "Running") {
    Start-Service -Name "Tailscale"
    Start-Sleep -Seconds 2
}

# 5. Pripojeni pocitace do vasi Tailscale site
Write-Host ""
Write-Host "[3/3] Registruji pocitac do vasi Tailscale site..." -ForegroundColor Yellow

& $TailscaleExe up --authkey "$AuthKey" --unattended --hostname "$Hostname" --accept-routes

Start-Sleep -Seconds 4

# 6. Ziskani pridelene IP adresy
$tsIp = ""
try {
    $tsIp = (& $TailscaleExe ip -4).Trim()
} catch {
    # Fallback
}

Write-Host ""
Write-Host "==================================================" -ForegroundColor Green
Write-Host "   Tailscale byl uspesne pripojen!                " -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Write-Host "Pocitac bezi v rezimu unattended (24/7 pristup na pozadi)." -ForegroundColor White
Write-Host ""
if ($tsIp) {
    Write-Host "Pridelena Tailscale IP adresa: $tsIp" -ForegroundColor Cyan
    Write-Host "Hostname v siti:              $Hostname" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Odkazy pro vzdaleny pristup odkudkoliv ze sveta:" -ForegroundColor Yellow
    Write-Host "  Hlavni administrace : http://$($tsIp):5001/administration" -ForegroundColor White
    Write-Host "  Prehled aktivity    : http://$($tsIp):5001/activity" -ForegroundColor White
    Write-Host "  AI Diagnostika      : http://$($tsIp):5001/logs" -ForegroundColor White
    Write-Host "  Udelit den volna    : http://$($tsIp):5001/day-off" -ForegroundColor White
    Write-Host "  Okamzite studium    : http://$($tsIp):5001/study-now" -ForegroundColor White
} else {
    Write-Host "Pocitac byl zaregistrovan pod jmenem: $Hostname" -ForegroundColor Cyan
    Write-Host "IP adresu naleznete ve sve Tailscale konzoli na https://login.tailscale.com/admin/machines" -ForegroundColor White
}
Write-Host ""
Write-Host "Hotovo!" -ForegroundColor Green
