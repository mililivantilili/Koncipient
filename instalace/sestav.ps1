# Sestaví instalátor Koncipientu: vystup\Koncipient-instalace-<verze>.exe
#
# Spusť v PowerShellu z kořene repozitáře:
#     powershell -ExecutionPolicy Bypass -File instalace\sestav.ps1
#
# Potřebuje .NET 10 SDK (je ve Visual Studiu) a Inno Setup 6. Inno Setup stačí
# nainstalovat jednou:  winget install JRSoftware.InnoSetup
# Skript stáhne modely OCR (asi 140 MB) a přibalí je, takže nainstalovaný program
# k síti nepřistupuje. Spisy ani žádná jiná data se nikam neposílají.

param(
    [string]$Verze = "0.1.0"
)

$ErrorActionPreference = "Stop"
$koren = Split-Path -Parent $PSScriptRoot
$vystup = Join-Path $koren "vystup"
$program = Join-Path $vystup "program"

Write-Host "1/4 Čistím $program"
if (Test-Path $program) { Remove-Item $program -Recurse -Force }
New-Item -ItemType Directory -Force -Path $program | Out-Null

Write-Host "2/4 Překládám program a koncipient-cli (samostatné, bez instalace .NET)"
foreach ($projekt in @("src\Koncipient.App\Koncipient.App.csproj", "src\Koncipient.Cli\Koncipient.Cli.csproj")) {
    dotnet publish (Join-Path $koren $projekt) -c Release -r win-x64 --self-contained true `
        -p:Version=$Verze -p:DebugType=none -o $program
    if ($LASTEXITCODE -ne 0) { throw "Překlad $projekt selhal." }
}

# nepotřebné soubory knihoven: ladicí symboly, ladicí DirectML, video kodeky OpenCV
Get-ChildItem $program -Include *.pdb, DirectML.Debug.*, onnxruntime.lib, opencv_videoio_ffmpeg*.dll -Recurse |
    Remove-Item -Force

Write-Host "3/4 Stahuji modely OCR do instalace"
$modely = Join-Path $program "modely"
& (Join-Path $program "koncipient-cli.exe") stahni-modely --kam $modely
if ($LASTEXITCODE -ne 0) { throw "Stažení modelů selhalo." }

Write-Host "4/4 Balím instalátor"
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Chybí Inno Setup 6. Nainstaluj ho příkazem:  winget install JRSoftware.InnoSetup  a spusť skript znovu."
}
& $iscc "/DVerze=$Verze" "/DZdroj=$program" (Join-Path $PSScriptRoot "Koncipient.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup selhal." }

$exe = Join-Path $vystup "Koncipient-instalace-$Verze.exe"
Write-Host ""
Write-Host "Hotovo: $exe ($([math]::Round((Get-Item $exe).Length / 1MB)) MB)"
