<#
.SYNOPSIS
    WinOverlay 배포용 빌드 + Velopack 설치 마법사 생성.

.DESCRIPTION
    1. dotnet publish (Release, win-x64, self-contained, single-file) -> .\publish\
    2. vpk pack -> .\dist\ 에 Setup.exe + 업데이트 패키지 생성

.PARAMETER Version
    Semantic version. 예: 0.1.0. 생략하면 csproj <Version>의 값을 사용.

.EXAMPLE
    .\tools\build-release.ps1
    .\tools\build-release.ps1 -Version 0.2.0
#>
param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$repoRoot   = Resolve-Path (Join-Path $PSScriptRoot '..')
$proj       = Join-Path $repoRoot 'src\OverlayApp.Avalonia\OverlayApp.Avalonia.csproj'
$publishDir = Join-Path $repoRoot 'publish'
$distDir    = Join-Path $repoRoot 'dist'
$packId     = 'WinOverlay'
$exeName    = 'WinOverlay.exe'
$title      = 'WinOverlay'
$authors    = 'ljhoo24'
$icon       = Join-Path $repoRoot 'src\OverlayApp.Avalonia\Assets\app.ico'

if (-not $Version) {
    [xml]$csproj = Get-Content $proj
    $Version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $Version) { throw 'Version not found in csproj and -Version not given.' }
}

Write-Host ""
Write-Host "==== WinOverlay $Version 배포 빌드 ====" -ForegroundColor Cyan
Write-Host "publish: $publishDir"
Write-Host "dist:    $distDir"
Write-Host ""

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $distDir) { Remove-Item $distDir -Recurse -Force }

Write-Host "[1/2] dotnet publish ..." -ForegroundColor Yellow
dotnet publish $proj `
    -c Release `
    -f net8.0-windows `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=embedded `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

Write-Host ""
Write-Host "[2/2] vpk pack ..." -ForegroundColor Yellow
vpk pack `
    --packId $packId `
    --packVersion $Version `
    --packDir $publishDir `
    --mainExe $exeName `
    --packTitle $title `
    --packAuthors $authors `
    --icon $icon `
    --outputDir $distDir
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed ($LASTEXITCODE)" }

Write-Host ""
Write-Host "==== 완료 ====" -ForegroundColor Green
Get-ChildItem $distDir | Sort-Object Length | Format-Table Name, @{N='Size';E={'{0:N0} bytes' -f $_.Length}}
$setup = Join-Path $distDir "$packId-Setup.exe"
if (Test-Path $setup) {
    Write-Host ""
    Write-Host "설치 마법사: $setup" -ForegroundColor Green
}
