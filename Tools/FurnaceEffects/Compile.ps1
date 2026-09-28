$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot/../..").Path
Push-Location $projectRoot
try {
    $unityData = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data'
    [xml]$project = Get-Content Assembly-CSharp.csproj
    $output = Join-Path $projectRoot 'Temp/FurnaceValidation'
    New-Item -ItemType Directory -Force $output | Out-Null
    $argsList = @('-target:library', '-nostdlib+', '-langversion:latest', '-nowarn:0436', ('-out:"'+$output+'/FurnaceCompile.dll"'))
    $argsList += '-define:UNITY_EDITOR,UNITY_6000_0_OR_NEWER,ENABLE_INPUT_SYSTEM'
    $argsList += $project.Project.ItemGroup.Reference.HintPath | Where-Object { $_ } | ForEach-Object { '-r:"'+$_+'"' }
    $argsList += $project.Project.ItemGroup.ProjectReference.Name | Where-Object { $_ } | ForEach-Object { '-r:"Library/ScriptAssemblies/'+$_+'.dll"' }
    $sources = @($project.Project.ItemGroup.Compile.Include | Where-Object { $_ -and (Test-Path -LiteralPath $_) })
    $sources += 'Assets/_Duskborn/Effects/Furnace/FurnaceEffects.cs'
    $argsList += $sources | Sort-Object -Unique | ForEach-Object { '"'+$_+'"' }
    $argsFile = Join-Path $output 'compile.rsp'
    $argsList | Set-Content $argsFile
    & "$unityData/NetCoreRuntime/dotnet.exe" "$unityData/DotNetSdkRoslyn/csc.dll" "@$argsFile"
    if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed' }
    [xml]$editorProject = Get-Content Assembly-CSharp-Editor.csproj
    $editorArgs = @('-target:library', '-nostdlib+', '-langversion:latest', '-define:UNITY_EDITOR', ('-out:"'+$output+'/FurnaceEditorCompile.dll"'), ('-r:"'+$output+'/FurnaceCompile.dll"'))
    $editorArgs += $editorProject.Project.ItemGroup.Reference.HintPath | Where-Object { $_ -and $_ -notmatch 'Assembly-CSharp.dll$' } | ForEach-Object { '-r:"'+$_+'"' }
    $editorArgs += '-r:"Library/ScriptAssemblies/FishNet.Runtime.dll"'
    $editorArgs += 'Assets/_Duskborn/Editor/ForgeModelGenerator.cs', 'Assets/_Duskborn/Editor/FurnaceEffectsPreview.cs', 'Assets/_Duskborn/Editor/FurnaceEffectsEditor.cs', 'Assets/_Duskborn/Editor/SceneFurnaceTests.cs'
    $editorFile = Join-Path $output 'editor.rsp'
    $editorArgs | Set-Content $editorFile
    & "$unityData/NetCoreRuntime/dotnet.exe" "$unityData/DotNetSdkRoslyn/csc.dll" "@$editorFile"
    if ($LASTEXITCODE -ne 0) { throw 'Editor compilation failed' }
    Write-Output 'Runtime and furnace editor C# compilation passed (offline; no Unity import/render validation).'
} finally { Pop-Location }
