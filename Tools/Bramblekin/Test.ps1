param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot/../..").Path
Push-Location $projectRoot
try {
    $output = Join-Path $projectRoot 'Temp/BramblekinTests'
    New-Item -ItemType Directory -Force $output | Out-Null
    $runtime = Get-ChildItem "$UnityData/NetCoreRuntime/shared/Microsoft.NETCore.App" -Directory | Select-Object -First 1
    $compileArgs = @('-target:exe', '-nostdlib+', '-langversion:latest', ('-out:"'+$output+'/ClockTests.dll"'))
    $compileArgs += Get-ChildItem $runtime.FullName -Filter '*.dll' | Where-Object {
        try { [Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null; $true } catch { $false }
    } | ForEach-Object { '-r:"'+$_.FullName+'"' }
    $compileArgs += 'Tools/Bramblekin/ClockTests.cs', 'Assets/_Duskborn/Gameplay/Enemies/BramblekinClock.cs'
    $compileArgs | Set-Content "$output/tests.rsp"
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" "@$output/tests.rsp"
    if ($LASTEXITCODE -ne 0) { throw 'Attack clock compilation failed' }
    @{runtimeOptions=@{tfm='net8.0';framework=@{name='Microsoft.NETCore.App';version=$runtime.Name}}} |
        ConvertTo-Json -Depth 4 | Set-Content "$output/ClockTests.runtimeconfig.json"
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$output/ClockTests.dll"
    if ($LASTEXITCODE -ne 0) { throw 'Attack clock tests failed' }
} finally { Pop-Location }

