$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src\Plataforma.Launcher\Plataforma.Launcher.csproj"
$installerPath = Join-Path $repositoryRoot "installer\Plataforma.Launcher.iss"
$assetDirectory = Join-Path $repositoryRoot "src\Plataforma.Launcher\Assets"
$publishDirectory = Join-Path $repositoryRoot "artifacts\windows\publish"
$outputDirectory = Join-Path $repositoryRoot "artifacts\windows\installer"

$compiler = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if (-not $compiler) {
    $compilerCandidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    )
    $compiler = $compilerCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $compiler) {
    throw "Inno Setup 6 is required. Install it from https://jrsoftware.org/isinfo.php and run this script again."
}

New-Item -ItemType Directory -Force -Path $publishDirectory, $outputDirectory | Out-Null

& dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "The Windows launcher publish failed with exit code $LASTEXITCODE."
}

$publishMacro = "/DPublishDir=`"$publishDirectory`""
$outputMacro = "/DOutputDir=`"$outputDirectory`""
$assetMacro = "/DAssetDir=`"$assetDirectory`""
& $compiler $publishMacro $outputMacro $assetMacro $installerPath
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE."
}

$installer = Join-Path $outputDirectory "Plataforma-Launcher-Setup-win-x64.exe"
Get-Item $installer | Select-Object FullName, Length, LastWriteTime
