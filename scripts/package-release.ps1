# QuestLock Gate - Release Packaging Script
$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "   QuestLock Gate - Tvorba instalacniho balicku   " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$DistDir = "$ProjectRoot\dist\QuestLock-Setup"

if (Test-Path $DistDir) {
    Remove-Item -Path $DistDir -Recurse -Force
}

New-Item -ItemType Directory -Path "$DistDir\Guardian" -Force | Out-Null
New-Item -ItemType Directory -Path "$DistDir\UI" -Force | Out-Null

Write-Host "`n[1/3] Publikuji QuestLock.Guardian (Self-Contained win-x64)..." -ForegroundColor Yellow
dotnet publish "$ProjectRoot\src\QuestLock.Guardian\QuestLock.Guardian.csproj" `
    -c Release -r win-x64 --self-contained true `
    -o "$DistDir\Guardian" --nologo -v q

Write-Host "`n[2/3] Publikuji QuestLock.UI (Self-Contained win-x64)..." -ForegroundColor Yellow
dotnet publish "$ProjectRoot\src\QuestLock.UI\QuestLock.UI.csproj" `
    -c Release -r win-x64 --self-contained true `
    -o "$DistDir\UI" --nologo -v q

Write-Host "`n[3/3] Kopiruji instalacni skripty a vytvarim ZIP..." -ForegroundColor Yellow
Copy-Item "$ProjectRoot\scripts\install.ps1" -Destination "$DistDir\install.ps1"
Copy-Item "$ProjectRoot\scripts\uninstall.ps1" -Destination "$DistDir\uninstall.ps1"
Copy-Item "$ProjectRoot\README.md" -Destination "$DistDir\README.md"

$ZipPath = "$ProjectRoot\dist\QuestLock-Setup.zip"
if (Test-Path $ZipPath) {
    Remove-Item -Path $ZipPath -Force
}

Compress-Archive -Path "$DistDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal

$sizeMb = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "   Balicek byl uspesne vytvoren!                  " -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Write-Host "Vysledny archiv: $ZipPath ($sizeMb MB)" -ForegroundColor Yellow
Write-Host "Tento ZIP staci prenest na bratruv PC a spustit install.ps1 jako Spravce." -ForegroundColor White
