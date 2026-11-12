param([switch]$Build)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$validationRoot = Join-Path $projectRoot 'Logs/RefactorValidation'
$version = ((Get-Content (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion: ' }) -split ': ', 2)[1].Trim()
$editor = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
if (-not (Test-Path -LiteralPath $editor)) { throw "Unity $version is required." }
if (Test-Path -LiteralPath (Join-Path $validationRoot 'Temp/UnityLockfile')) { throw 'The validation copy is already open. Close it before running checks.' }
New-Item -ItemType Directory -Path $validationRoot -Force | Out-Null
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
    & robocopy (Join-Path $projectRoot $folder) (Join-Path $validationRoot $folder) /E /NFL /NDL /NJH /NJS /NP /XF '._*' | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Failed to copy $folder." }
}
$checks = @('ArchitectureChecks.Run', 'EngineAudioChecks.Run', 'GameplaySmokeCheck.Run')
if ($Build) { $checks += 'StandaloneBuildCheck.Run' }
foreach ($check in $checks) {
    $log = Join-Path $projectRoot "Logs/$check.log"
    $quit = if ($check -eq 'GameplaySmokeCheck.Run') { '' } else { '-quit' }
    $arguments = "-batchmode -nographics $quit -projectPath `"$validationRoot`" -executeMethod $check -logFile `"$log`""
    Write-Host "Running $check..."
    $process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "$check failed. See $log" }
}
Get-Content (Join-Path $validationRoot 'Logs/architecture-checks.txt')
Get-Content (Join-Path $validationRoot 'Logs/gameplay-smoke-check.txt')

Get-Content (Join-Path $validationRoot 'Logs/engine-audio-checks.txt')
