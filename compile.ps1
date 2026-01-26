#!/usr/bin/env pwsh
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Root of the repository (folder containing this script)
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$solution      = Join-Path $root 'CS2-Discord-Utilities.sln'
$compiledRoot  = Join-Path $root 'Compiled Files'
$pluginsRoot   = Join-Path $compiledRoot 'plugins'
$sharedRoot    = Join-Path $compiledRoot 'shared'
$archivesRoot  = Join-Path $compiledRoot 'archives'
$discordUtilitiesProj = Join-Path $root 'src/DiscordUtilities/DiscordUtilities.csproj'
$discordUtilitiesDir  = Join-Path $pluginsRoot 'DiscordUtilities'

# Clean previous archives but keep build outputs (faster incremental builds)
if (Test-Path $archivesRoot) { Remove-Item -Recurse -Force $archivesRoot }
New-Item -ItemType Directory -Path $archivesRoot -Force | Out-Null

if (-not (Test-Path $solution)) {
    throw "Solution file not found at $solution"
}

Write-Host "[STEP] Restore + Build ($solution)" -ForegroundColor Cyan
dotnet restore $solution
dotnet build $solution -c Release --no-restore --nologo

# Ensure DiscordUtilities ships with all runtime dependencies
Write-Host "[STEP] Publish DiscordUtilities with dependencies" -ForegroundColor Cyan
if (Test-Path $discordUtilitiesDir) {
    Remove-Item -Recurse -Force $discordUtilitiesDir
}
dotnet publish $discordUtilitiesProj -c Release --no-restore --nologo -o $discordUtilitiesDir
# CounterStrikeSharp API is provided by the server, avoid shipping our own copy
$cssApi = Join-Path $discordUtilitiesDir 'CounterStrikeSharp.API.dll'
if (Test-Path $cssApi) { Remove-Item $cssApi -Force }

# Add lang files
$langSrc = Join-Path $root 'lang/DiscordUtilities'
$langDst = Join-Path $discordUtilitiesDir 'lang'
if (Test-Path $langSrc) {
    Copy-Item -Path $langSrc -Destination $langDst -Recurse -Force
}

# Optional GeoLite2 database (skip silently if absent)
$geoSrc = Join-Path $root 'GeoLite2-Country.mmdb'
$geoDst = Join-Path $discordUtilitiesDir 'GeoLite2-Country.mmdb'
if (Test-Path $geoSrc) {
    Copy-Item $geoSrc $geoDst -Force
} else {
    Write-Host "[WARN] GeoLite2-Country.mmdb not found; place it at repo root to include it." -ForegroundColor Yellow
}

# Keep only the files the server actually needs
$allowedFiles = @(
    'Discord.Net.Commands.dll',
    'Discord.Net.Core.dll',
    'Discord.Net.Interactions.dll',
    'Discord.Net.Rest.dll',
    'Discord.Net.Webhook.dll',
    'Discord.Net.WebSocket.dll',
    'DiscordUtilities.dll',
    'DiscordUtilities.pdb',
    'DiscordUtilities.deps.json',
    'GeoLite2-Country.mmdb',
    'MaxMind.Db.dll',
    'MaxMind.GeoIP2.dll',
    'MySqlConnector.dll',
    'Newtonsoft.Json.dll'
)

Get-ChildItem $discordUtilitiesDir -File -Recurse | ForEach-Object {
    # Always keep language files under lang/
    if ($_.FullName -like "*\lang\*") { return }
    if ($allowedFiles -notcontains $_.Name) {
        Remove-Item $_.FullName -Force
    }
}
# Remove undesired folders except lang
Get-ChildItem $discordUtilitiesDir -Directory | Where-Object { $_.Name -ne 'lang' } | Remove-Item -Recurse -Force

if (-not (Test-Path $pluginsRoot)) {
    throw "Plugins output folder not found at $pluginsRoot (check csproj OutputPath)."
}

function New-Zip {
    param(
        [Parameter(Mandatory = $true)] [string] $SourceDir,
        [Parameter(Mandatory = $true)] [string] $ZipPath
    )

    if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
    Compress-Archive -Path $SourceDir -DestinationPath $ZipPath
}

# Package each plugin/module with its folder name to avoid upload collisions
Write-Host "[STEP] Packaging plugins" -ForegroundColor Cyan
$pluginZips = @()
Get-ChildItem $pluginsRoot -Directory | ForEach-Object {
    $zip = Join-Path $archivesRoot "$($_.Name).zip"
    New-Zip -SourceDir $_.FullName -ZipPath $zip
    $pluginZips += $zip
}

# Package shared libraries (e.g., DiscordUtilitiesAPI) as well
if (Test-Path $sharedRoot) {
    Write-Host "[STEP] Packaging shared libraries" -ForegroundColor Cyan
    Get-ChildItem $sharedRoot -Directory | ForEach-Object {
        $zip = Join-Path $archivesRoot "$($_.Name).zip"
        New-Zip -SourceDir $_.FullName -ZipPath $zip
        $pluginZips += $zip
    }
}

Write-Host "[OK] Build & packaging finished." -ForegroundColor Green
Write-Host " - Plugins dir : $pluginsRoot"
Write-Host " - Archives    : $archivesRoot"
Write-Host " - Files       :"
$pluginZips | ForEach-Object { Write-Host "   * $_" }
