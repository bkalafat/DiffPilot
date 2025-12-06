# DiffPilot Build Scripts
# Usage: .\scripts\build.ps1 <command>

param(
    [Parameter(Position=0)]
    [ValidateSet("build", "test", "run", "vsix", "publish", "install", "all")]
    [string]$Command = "build"
)

$ErrorActionPreference = "Stop"
$RootDir = Split-Path -Parent $PSScriptRoot
$ExtensionDir = Join-Path $RootDir "vscode-extension"

function Build-Server {
    Write-Host "🔨 Building server..." -ForegroundColor Cyan
    Push-Location $RootDir
    dotnet build
    Pop-Location
}

function Test-Server {
    Write-Host "🧪 Running tests..." -ForegroundColor Cyan
    Push-Location $RootDir
    dotnet test
    Pop-Location
}

function Run-Server {
    Write-Host "🚀 Running server..." -ForegroundColor Cyan
    Push-Location $RootDir
    dotnet run
    Pop-Location
}

function Build-Vsix {
    Write-Host "📦 Building VSIX..." -ForegroundColor Cyan
    Push-Location $ExtensionDir
    vsce package
    Pop-Location
}

function Publish-Vsix {
    Write-Host "🚀 Publishing to Marketplace..." -ForegroundColor Cyan
    Push-Location $ExtensionDir
    vsce publish
    Pop-Location
}

function Install-Extension {
    Write-Host "💿 Installing extension locally..." -ForegroundColor Cyan
    $vsix = Get-ChildItem -Path $ExtensionDir -Filter "*.vsix" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($vsix) {
        code --install-extension $vsix.FullName
        Write-Host "✅ Installed: $($vsix.Name)" -ForegroundColor Green
    } else {
        Write-Host "❌ No VSIX found. Run 'vsix' first." -ForegroundColor Red
    }
}

switch ($Command) {
    "build"   { Build-Server }
    "test"    { Test-Server }
    "run"     { Run-Server }
    "vsix"    { Build-Vsix }
    "publish" { Publish-Vsix }
    "install" { Install-Extension }
    "all"     { Build-Server; Test-Server; Build-Vsix }
}
