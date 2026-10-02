param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot/../..").Path
Push-Location $projectRoot
try {
    & "$projectRoot/Tools/BuildingTests/Run.ps1" -UnityData $UnityData
    $output = Join-Path $projectRoot 'Temp/EngineeringAudit'
    New-Item -ItemType Directory -Force $output | Out-Null
    $response = Join-Path $output 'tests.rsp'
    $lines = Get-Content 'Temp/BuildingValidation/CoreTests/tests.rsp' | Where-Object { $_ -notmatch '^-out:' }
    $lines += @('-main:EngineeringAuditTests', ('-out:"' + $output + '/RegressionTests.dll"'),
        'Tools/EngineeringAudit/ContractTests.cs', 'Assets/_Duskborn/Gameplay/Combat/StatusEffectController.cs',
        'Assets/_Duskborn/Gameplay/Combat/StatusEffect.cs')
    $lines | Set-Content -LiteralPath $response
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" "@$response"
    if ($LASTEXITCODE -ne 0) { throw 'Engineering test compilation failed.' }
    Copy-Item 'Temp/BuildingValidation/CoreTests/CoreTests.runtimeconfig.json' "$output/RegressionTests.runtimeconfig.json"
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$output/RegressionTests.dll"
    if ($LASTEXITCODE -ne 0) { throw 'Engineering tests failed.' }
} finally { Pop-Location }
