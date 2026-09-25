[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('help', 'compile', 'validate', 'test', 'build-windows', 'all')]
    [string]$Command = 'help',

    [string]$UnityPath,
    [string]$BuildPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectVersionFile = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
$logDirectory = Join-Path $projectRoot 'Logs\UnityCli'

function Show-Usage {
    Write-Host @'
Unity CLI workflow

  .\Tools\unity.ps1 compile
  .\Tools\unity.ps1 validate
  .\Tools\unity.ps1 test
  .\Tools\unity.ps1 build-windows
  .\Tools\unity.ps1 all

Options:
  -UnityPath <path>  Override the Unity executable discovered from ProjectVersion.txt.
  -BuildPath <path>  Override the Windows build output path.

The `all` command runs compile, validation, and the project test suites. It does
not create a player build.
'@
}

function Resolve-UnityExecutable {
    if ($UnityPath) {
        $candidate = [System.IO.Path]::GetFullPath($UnityPath)
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            throw "Unity executable not found: $candidate"
        }
        return $candidate
    }

    if (-not (Test-Path -LiteralPath $projectVersionFile -PathType Leaf)) {
        throw "Project version file not found: $projectVersionFile"
    }

    $versionText = Get-Content -Raw -LiteralPath $projectVersionFile
    $match = [regex]::Match($versionText, '(?m)^m_EditorVersion:\s*(\S+)')
    if (-not $match.Success) {
        throw "Could not read m_EditorVersion from $projectVersionFile"
    }

    $programFiles = [Environment]::GetFolderPath('ProgramFiles')
    $candidate = Join-Path $programFiles "Unity\Hub\Editor\$($match.Groups[1].Value)\Editor\Unity.exe"
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Unity $($match.Groups[1].Value) is not installed at the expected Hub path: $candidate"
    }

    return $candidate
}

function Invoke-UnityTask {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [string[]]$TaskArguments,
        [string]$SuccessMarker,
        [string]$ExpectedOutput
    )

    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
    $logPath = Join-Path $logDirectory "unity-$Name.log"
    if (Test-Path -LiteralPath $logPath -PathType Leaf) {
        Remove-Item -LiteralPath $logPath -Force
    }
    $unityArguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $projectRoot,
        '-logFile', $logPath,
        '-timestamps'
    ) + $TaskArguments

    Write-Host "[unity-cli] Running $Name with $script:resolvedUnityPath"
    $quotedArguments = $unityArguments | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + ($_ -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"'
        }
        else {
            $_
        }
    }
    $process = Start-Process -FilePath $script:resolvedUnityPath `
        -ArgumentList ($quotedArguments -join ' ') `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    $exitCode = $process.ExitCode

    if ($exitCode -ne 0) {
        if (Test-Path -LiteralPath $logPath) {
            Write-Host "[unity-cli] Last log lines from $logPath"
            Get-Content -LiteralPath $logPath -Tail 80
        }
        throw "Unity task '$Name' failed with exit code $exitCode. Full log: $logPath"
    }

    if ($SuccessMarker) {
        if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) {
            throw "Unity task '$Name' did not create its log file: $logPath"
        }
        $markerFound = Select-String -LiteralPath $logPath -SimpleMatch $SuccessMarker -Quiet
        if (-not $markerFound) {
            throw "Unity task '$Name' exited without its success marker. Inspect: $logPath"
        }
    }

    if ($ExpectedOutput -and -not (Test-Path -LiteralPath $ExpectedOutput -PathType Leaf)) {
        throw "Unity task '$Name' exited successfully but did not create: $ExpectedOutput"
    }

    Write-Host "[unity-cli] $Name succeeded. Log: $logPath"
}

if ($Command -eq 'help') {
    Show-Usage
    return
}

$script:resolvedUnityPath = Resolve-UnityExecutable

switch ($Command) {
    'compile' {
        Invoke-UnityTask -Name 'compile' `
            -TaskArguments @('-quit', '-executeMethod', 'Duskborn.Editor.DuskbornCli.Compile') `
            -SuccessMarker '[DuskbornCli] Compile check succeeded.'
    }
    'validate' {
        Invoke-UnityTask -Name 'validate' `
            -TaskArguments @('-quit', '-executeMethod', 'Duskborn.Editor.DuskbornCli.Validate') `
            -SuccessMarker '[DuskbornCli] Validation succeeded.'
    }
    'test' {
        Invoke-UnityTask -Name 'test' `
            -TaskArguments @('-quit', '-executeMethod', 'Duskborn.Editor.DuskbornCli.RunTests') `
            -SuccessMarker '[DuskbornCli] Test run succeeded.'
    }
    'build-windows' {
        if (-not $BuildPath) {
            $BuildPath = Join-Path $projectRoot 'Builds\Windows\Mugg.exe'
        }
        $BuildPath = [System.IO.Path]::GetFullPath($BuildPath)
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $BuildPath) | Out-Null
        Invoke-UnityTask -Name 'build-windows' `
            -TaskArguments @(
                '-quit',
                '-buildTarget', 'StandaloneWindows64',
                '-executeMethod', 'Duskborn.Editor.DuskbornCli.BuildWindows',
                '-buildPath', $BuildPath
            ) `
            -SuccessMarker '[DuskbornCli] Windows build succeeded.' `
            -ExpectedOutput $BuildPath
    }
    'all' {
        Invoke-UnityTask -Name 'compile' `
            -TaskArguments @('-quit', '-executeMethod', 'Duskborn.Editor.DuskbornCli.Compile') `
            -SuccessMarker '[DuskbornCli] Compile check succeeded.'
        Invoke-UnityTask -Name 'validate' `
            -TaskArguments @('-quit', '-executeMethod', 'Duskborn.Editor.DuskbornCli.Validate') `
            -SuccessMarker '[DuskbornCli] Validation succeeded.'
        Invoke-UnityTask -Name 'test' `
            -TaskArguments @('-quit', '-executeMethod', 'Duskborn.Editor.DuskbornCli.RunTests') `
            -SuccessMarker '[DuskbornCli] Test run succeeded.'
    }
}
