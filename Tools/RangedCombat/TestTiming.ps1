param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot/../..").Path
Push-Location $projectRoot
try {
    $output = Join-Path $projectRoot 'Temp/RangedCombatValidation'
    New-Item -ItemType Directory -Force $output | Out-Null
    $runtime = Get-ChildItem "$UnityData/NetCoreRuntime/shared/Microsoft.NETCore.App" -Directory | Select-Object -First 1
    $compileArgs = @('-target:exe', '-nostdlib+', '-langversion:latest', ('-out:"'+$output+'/TimingTests.dll"'))
    $compileArgs += Get-ChildItem $runtime.FullName -Filter '*.dll' | Where-Object { try { [Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null; $true } catch { $false } } | ForEach-Object { '-r:"'+$_.FullName+'"' }
    $compileArgs += 'Tools/RangedCombat/TimingTests.cs', 'Assets/_Duskborn/Gameplay/Projectiles/RangedAttackGate.cs'
    $compileArgs | Set-Content "$output/timing.rsp"
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" "@$output/timing.rsp"
    if ($LASTEXITCODE -ne 0) { throw 'Timing test compilation failed' }
    @{ runtimeOptions = @{ tfm = 'net8.0'; framework = @{ name='Microsoft.NETCore.App'; version=$runtime.Name } } } | ConvertTo-Json -Depth 4 | Set-Content "$output/TimingTests.runtimeconfig.json"
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$output/TimingTests.dll"
    if ($LASTEXITCODE -ne 0) { throw 'Timing tests failed' }
} finally { Pop-Location }
