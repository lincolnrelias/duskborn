param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot/../..").Path
Push-Location $projectRoot
try {
    $output = Join-Path $projectRoot 'Temp/BuildingValidation/CoreTests'
    New-Item -ItemType Directory -Force $output | Out-Null
    $runtime = Get-ChildItem "$UnityData/NetCoreRuntime/shared/Microsoft.NETCore.App" -Directory | Select-Object -First 1
    $argsFile = Join-Path $output 'tests.rsp'
    $argsList = @('-target:exe', '-nostdlib+', '-langversion:latest', ('-out:"'+$output+'/CoreTests.dll"'))
    $argsList += Get-ChildItem $runtime.FullName -Filter '*.dll' | Where-Object { try { [Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null; $true } catch { $false } } | ForEach-Object { '-r:"'+$_.FullName+'"' }
    $argsList += @('Tools/BuildingTests/Program.cs', 'Assets/_Duskborn/Gameplay/Loot/ResourceInventory.cs', 'Assets/_Duskborn/Gameplay/Building/BuildableDefinition.cs', 'Assets/_Duskborn/Gameplay/Building/PlacedBuilding.cs', 'Assets/_Duskborn/Gameplay/Crafting/CraftingRecipe.cs', 'Assets/_Duskborn/Gameplay/Crafting/CraftingTier.cs', 'Assets/_Duskborn/Gameplay/Crafting/CraftingStationType.cs')
    $argsList | Set-Content $argsFile
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" "@$argsFile"
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
    @{ runtimeOptions = @{ tfm = 'net8.0'; framework = @{ name='Microsoft.NETCore.App'; version=$runtime.Name } } } | ConvertTo-Json -Depth 4 | Set-Content "$output/CoreTests.runtimeconfig.json"
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$output/CoreTests.dll"
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
} finally { Pop-Location }

