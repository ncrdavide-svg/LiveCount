# Crea dist\LiveCount-Setup-<versione>.exe e dist\LiveCount-Setup.exe.
# Richiede Inno Setup 6 (gratuito): winget install JRSoftware.InnoSetup
# Uso: powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
# La versione si prende dall'exe compilato (AssemblyVersion in src\Program.cs).

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 non trovato. Installalo con: winget install JRSoftware.InnoSetup" }

Write-Host "Compilazione di LiveCount.exe..."
& cmd /c "`"$root\build.bat`""
if ($LASTEXITCODE -ne 0) { throw "build.bat non riuscito" }

$exe = Join-Path $root "LiveCount.exe"
$v = [Reflection.AssemblyName]::GetAssemblyName($exe).Version
$version = "$($v.Major).$($v.Minor).$($v.Build)"

Write-Host "Creazione installer LiveCount $version..."
& $iscc "/DAppVersion=$version" "/DExePath=$exe" (Join-Path $PSScriptRoot "LiveCount.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup non riuscito" }

$setup = Join-Path $root "dist\LiveCount-Setup-$version.exe"
# Stesso file con un nome fisso, per il link stabile releases/latest/download/LiveCount-Setup.exe.
Copy-Item $setup (Join-Path $root "dist\LiveCount-Setup.exe") -Force
Get-Item $setup, (Join-Path $root "dist\LiveCount-Setup.exe")
