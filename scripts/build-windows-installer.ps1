param(
    [string]$Version = "1.1.1"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src\Plataforma.Launcher\Plataforma.Launcher.csproj"
$publishDirectory = Join-Path $repositoryRoot "artifacts\windows\publish"
$releaseDirectory = Join-Path $repositoryRoot "artifacts\windows\release"
$installerName = "Plataforma_laucher.exe"
$outputInstaller = Join-Path $releaseDirectory $installerName

$vpk = (Get-Command vpk -ErrorAction SilentlyContinue).Source
if (-not $vpk) {
    throw "Velopack CLI 1.2.161 is required. Install it with: dotnet tool install --global vpk --version 1.2.161"
}

New-Item -ItemType Directory -Force -Path $publishDirectory, $releaseDirectory | Out-Null

& dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    "-p:Version=$Version" `
    --output $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "The Windows launcher publish failed with exit code $LASTEXITCODE."
}

& $vpk pack `
    --packId Playlab.Launcher `
    --packTitle "Playlab Launcher" `
    --packVersion $Version `
    --packDir $publishDirectory `
    --mainExe Plataforma.Launcher.exe `
    --runtime win-x64 `
    --channel win-x64 `
    --outputDir $releaseDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Velopack failed to package the Windows launcher with exit code $LASTEXITCODE."
}

$generatedInstaller = Join-Path $releaseDirectory "Playlab.Launcher-win-x64-Setup.exe"
if (-not (Test-Path $generatedInstaller)) {
    throw "Velopack did not produce the expected installer at '$generatedInstaller'."
}

Copy-Item -Path $generatedInstaller -Destination $outputInstaller -Force
Get-Item $outputInstaller | Select-Object FullName, Length, LastWriteTime
