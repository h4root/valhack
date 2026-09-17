param(
    [string]$ValheimPath = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
)

$ErrorActionPreference = "Stop"

# Официальный релиз UnityDoorstop v4.5.0, Windows. Размер и адрес проверены заранее.
$DoorstopUrl = "https://github.com/NeighTools/UnityDoorstop/releases/download/v4.5.0/doorstop_win_release_4.5.0.zip"
$DoorstopSize = 23763

$root = $PSScriptRoot
$exe = Join-Path $ValheimPath "valheim.exe"
if (-not (Test-Path $exe)) {
    throw "valheim.exe не найден в '$ValheimPath'. Передай -ValheimPath с реальным путём."
}

$releaseDir = Join-Path $root "bin\Release"
$hostDir = Join-Path $root "host\bin\Release"
$dll = Join-Path $releaseDir "ValheimAdminOverlay.dll"
$hostDll = Join-Path $hostDir "ValheimAdminOverlay.Host.dll"
if (-not (Test-Path $dll) -or -not (Test-Path $hostDll)) {
    throw "Не собрано. Сначала запусти .\build.ps1 (нужен .NET SDK)."
}

$tmp = Join-Path $env:TEMP ("doorstop_" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null
$zip = Join-Path $tmp "doorstop.zip"

Write-Host "Скачиваю UnityDoorstop v4.5.0..."
Invoke-WebRequest -Uri $DoorstopUrl -OutFile $zip

$actual = (Get-Item $zip).Length
if ($actual -ne $DoorstopSize) {
    throw "Размер загруженного файла ($actual Б) не совпал с ожидаемым ($DoorstopSize Б). Прерываю."
}
Write-Host "Скачано и проверено по размеру."

Expand-Archive -Path $zip -DestinationPath $tmp -Force

$winhttp = Get-ChildItem -Path $tmp -Recurse -Filter "winhttp.dll" |
    Where-Object { $_.FullName -match "x64" } |
    Select-Object -First 1
if (-not $winhttp) {
    $winhttp = Get-ChildItem -Path $tmp -Recurse -Filter "winhttp.dll" | Select-Object -First 1
}
if (-not $winhttp) {
    throw "В архиве Doorstop не нашёлся winhttp.dll."
}

Copy-Item $winhttp.FullName (Join-Path $ValheimPath "winhttp.dll") -Force
Copy-Item (Join-Path $root "dist\doorstop_config.ini") (Join-Path $ValheimPath "doorstop_config.ini") -Force

$payloadDir = Join-Path $ValheimPath "Doorstop"
New-Item -ItemType Directory -Path $payloadDir -Force | Out-Null
# Оверлей плюс зависимости HarmonyX (0Harmony, MonoMod, Mono.Cecil) — всё рядом.
Get-ChildItem -Path $releaseDir -Filter *.dll | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $payloadDir $_.Name) -Force
}

Remove-Item $tmp -Recurse -Force

Write-Host ""
Write-Host "Готово. Разложено в '$ValheimPath':"
Write-Host "  winhttp.dll              - инжектор Doorstop"
Write-Host "  doorstop_config.ini      - указывает на Doorstop\ValheimAdminOverlay.dll"
Write-Host "  Doorstop\                - сам оверлей и 0Harmony.dll"
Write-Host ""
Write-Host "Запусти игру. В игре Insert - меню, End - выгрузить."
Write-Host "Чтобы временно сыграть в ваниль без удаления файлов, в Steam ->"
Write-Host "  Свойства Valheim -> Параметры запуска впиши:  DOORSTOP_ENABLED=false %command%"
