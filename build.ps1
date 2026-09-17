param(
    [string]$ValheimPath = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
)

$ErrorActionPreference = "Stop"

$managed = Join-Path $ValheimPath "valheim_Data\Managed\assembly_valheim.dll"
if (-not (Test-Path $managed)) {
    throw "Не найден assembly_valheim.dll в '$ValheimPath'. Укажи путь через -ValheimPath."
}

$dotnet = "dotnet"
if (-not (Get-Command $dotnet -ErrorAction SilentlyContinue)) {
    $dotnet = "C:\Program Files\dotnet\dotnet.exe"
    if (-not (Test-Path $dotnet)) { throw "Не найден .NET SDK. Поставь: winget install Microsoft.DotNet.SDK.8" }
}

& $dotnet build "$PSScriptRoot\host\ValheimAdminOverlay.Host.csproj" -c Release "-p:ValheimPath=$ValheimPath"
if ($LASTEXITCODE -ne 0) { throw "Сборка хоста упала." }

& $dotnet build "$PSScriptRoot\ValheimAdminOverlay.csproj" -c Release "-p:ValheimPath=$ValheimPath"
if ($LASTEXITCODE -ne 0) { throw "Сборка меню упала." }

Write-Host ""
Write-Host "Собрано: $PSScriptRoot\bin\Release\ValheimAdminOverlay.dll"
Write-Host "Дальше запусти install-doorstop.ps1 (тем же -ValheimPath, если игра не в стандартной папке)."
