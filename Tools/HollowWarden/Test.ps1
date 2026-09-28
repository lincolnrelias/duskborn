param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot/../..").Path
Push-Location $projectRoot
try {
    $output = Join-Path $projectRoot 'Temp/HollowWardenTests'
    New-Item -ItemType Directory -Force $output | Out-Null
    $runtime = Get-ChildItem "$UnityData/NetCoreRuntime/shared/Microsoft.NETCore.App" -Directory | Select-Object -First 1
    $argsList = @('-target:exe', '-nostdlib+', '-langversion:latest', ('-out:"'+$output+'/EncounterTests.dll"'))
    $argsList += Get-ChildItem $runtime.FullName -Filter '*.dll' | Where-Object {
        try { [Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null; $true } catch { $false }
    } | ForEach-Object { '-r:"'+$_.FullName+'"' }
    $argsList += 'Tools/HollowWarden/EncounterTests.cs', 'Assets/_Duskborn/Gameplay/Enemies/HollowWardenEncounter.cs', 'Assets/_Duskborn/Gameplay/Enemies/HollowWardenNightRules.cs'
    $argsFile = Join-Path $output 'tests.rsp'
    $argsList | Set-Content $argsFile
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" "@$argsFile"
    if ($LASTEXITCODE -ne 0) { throw 'Encounter compilation failed' }
    @{runtimeOptions=@{tfm='net8.0';framework=@{name='Microsoft.NETCore.App';version=$runtime.Name}}} |
        ConvertTo-Json -Depth 4 | Set-Content "$output/EncounterTests.runtimeconfig.json"
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$output/EncounterTests.dll"
    if ($LASTEXITCODE -ne 0) { throw 'Encounter tests failed' }
} finally { Pop-Location }
