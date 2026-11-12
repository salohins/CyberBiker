param(
    [switch]$Check,
    [string]$EditorPath = $env:UNITY_EDITOR_PATH
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$versionFile = Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt'
$versionLine = Get-Content -LiteralPath $versionFile | Where-Object { $_ -match '^m_EditorVersion: ' }
$version = ($versionLine -split ': ', 2)[1].Trim()

if (-not $EditorPath) {
    $EditorPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) {
    throw "Unity $version is required. Install it through Unity Hub (see README.md), or set UNITY_EDITOR_PATH to its Unity.exe."
}
$installedVersion = (Get-Item -LiteralPath $EditorPath).VersionInfo.ProductVersion
if ($installedVersion -notlike "$version*") {
    throw "Expected Unity $version but found $installedVersion at $EditorPath. Use the matching editor to avoid migrating the project."
}

$logDirectory = Join-Path $projectPath 'Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
if ($Check) {
    $logPath = Join-Path $logDirectory 'local-check.log'
    Write-Host "Importing packages and assets, and compiling with Unity $version..."
    $editorArgs = "-batchmode -nographics -quit -projectPath `"$projectPath`" -buildTarget Win64 -logFile `"$logPath`""
    $process = Start-Process -FilePath $EditorPath -ArgumentList $editorArgs -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Unity check failed (exit $($process.ExitCode)). See $logPath"
    }
    Write-Host "Unity import and compilation succeeded. Log: $logPath"
} else {
    $logPath = Join-Path $logDirectory 'editor.log'
    $editorArgs = "-projectPath `"$projectPath`" -logFile `"$logPath`""
    Start-Process -FilePath $EditorPath -ArgumentList $editorArgs | Out-Null
    Write-Host "Opening CyberBiker in Unity $version."
}
