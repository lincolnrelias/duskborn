param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot/../..").Path
Push-Location $projectRoot
try {
    $output = Join-Path $projectRoot 'Temp/RangedCombatValidation'
    New-Item -ItemType Directory -Force $output | Out-Null
    [xml]$project = Get-Content Assembly-CSharp.csproj
    $compileArgs = @('-target:library', '-nostdlib+', '-langversion:latest', '-nowarn:0436', ('-out:"'+$output+'/RangedCompile.dll"'), '-define:UNITY_EDITOR,UNITY_6000_0_OR_NEWER,ENABLE_INPUT_SYSTEM')
    $compileArgs += $project.Project.ItemGroup.Reference.HintPath | Where-Object { $_ } | ForEach-Object { '-r:"'+$_+'"' }
    $compileArgs += $project.Project.ItemGroup.ProjectReference.Name | Where-Object { $_ } | ForEach-Object { '-r:"Library/ScriptAssemblies/'+$_+'.dll"' }
    $sources = @($project.Project.ItemGroup.Compile.Include | Where-Object { $_ -and (Test-Path -LiteralPath $_) })
    $sources += Get-ChildItem 'Assets/_Duskborn' -Filter '*.cs' -Recurse | Where-Object { $_.FullName -notmatch '[\\/]Editor[\\/]' } | ForEach-Object { $_.FullName }
    $compileArgs += $sources | ForEach-Object { (Resolve-Path -LiteralPath $_).Path } | Sort-Object -Unique | ForEach-Object { '"'+$_+'"' }
    $compileArgs | Set-Content "$output/runtime.rsp"
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" "@$output/runtime.rsp"
    if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed' }
    [xml]$project = Get-Content Assembly-CSharp-Editor.csproj
    $compileArgs = @('-target:library', '-nostdlib+', '-langversion:latest', '-define:UNITY_EDITOR', ('-out:"'+$output+'/RangedEditorCompile.dll"'), ('-r:"'+$output+'/RangedCompile.dll"'))
    $compileArgs += $project.Project.ItemGroup.Reference.HintPath | Where-Object { $_ -and $_ -notmatch 'Assembly-CSharp.dll$' } | ForEach-Object { '-r:"'+$_+'"' }
    $compileArgs += $project.Project.ItemGroup.ProjectReference.Name | Where-Object { $_ -and $_ -ne 'Assembly-CSharp' } | ForEach-Object { '-r:"Library/ScriptAssemblies/'+$_+'.dll"' }
    $sources = @($project.Project.ItemGroup.Compile.Include | Where-Object { $_ -and (Test-Path -LiteralPath $_) })
    $sources += Get-ChildItem 'Assets/_Duskborn/Editor' -Filter '*.cs' -Recurse | ForEach-Object { $_.FullName }
    $compileArgs += $sources | ForEach-Object { (Resolve-Path -LiteralPath $_).Path } | Sort-Object -Unique | ForEach-Object { '"'+$_+'"' }
    $compileArgs | Set-Content "$output/editor.rsp"
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" "@$output/editor.rsp"
    if ($LASTEXITCODE -ne 0) { throw 'Editor compilation failed' }
    Write-Output 'Runtime and editor C# compiled offline. Unity import, FishNet weaving and visuals remain unverified.'
} finally { Pop-Location }
