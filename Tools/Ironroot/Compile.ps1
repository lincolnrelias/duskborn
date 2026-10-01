param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot/../..").Path
Push-Location $projectRoot
try {
    # Uses the existing generated project references; never launches Unity or writes its assemblies.
    [xml]$project = Get-Content Assembly-CSharp.csproj
    $output = Join-Path $projectRoot 'Temp/IronrootValidation'
    New-Item -ItemType Directory -Force $output | Out-Null
    $argsList = @('-target:library', '-nostdlib+', '-langversion:latest', '-nowarn:0436', ('-out:"'+$output+'/IronrootCompile.dll"'))
    $argsList += '-define:UNITY_EDITOR,UNITY_6000_0_OR_NEWER,ENABLE_INPUT_SYSTEM'
    $argsList += $project.Project.ItemGroup.Reference.HintPath | Where-Object { $_ } | ForEach-Object { '-r:"'+$_+'"' }
    $argsList += $project.Project.ItemGroup.ProjectReference.Name | Where-Object { $_ } | ForEach-Object { '-r:"Library/ScriptAssemblies/'+$_+'.dll"' }
    $sources = @($project.Project.ItemGroup.Compile.Include | Where-Object { $_ -and (Test-Path -LiteralPath $_) })
    $sources += Get-ChildItem 'Assets/_Duskborn/Gameplay/Player/Ironroot*.cs' | ForEach-Object { $_.FullName }
    $argsList += $sources | ForEach-Object { (Resolve-Path -LiteralPath $_).Path } | Sort-Object -Unique | ForEach-Object { '"'+$_+'"' }
    $argsFile = Join-Path $output 'compile.rsp'
    $argsList | Set-Content $argsFile
    & "$unityData/NetCoreRuntime/dotnet.exe" "$unityData/DotNetSdkRoslyn/csc.dll" "@$argsFile"
    if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed' }
    [xml]$editorProject = Get-Content Assembly-CSharp-Editor.csproj
    $editorArgs = @('-target:library', '-nostdlib+', '-langversion:latest', '-define:UNITY_EDITOR', ('-out:"'+$output+'/IronrootEditorCompile.dll"'), ('-r:"'+$output+'/IronrootCompile.dll"'))
    $editorArgs += $editorProject.Project.ItemGroup.Reference.HintPath | Where-Object { $_ -and $_ -notmatch 'Assembly-CSharp.dll$' } | ForEach-Object { '-r:"'+$_+'"' }
    $editorArgs += $editorProject.Project.ItemGroup.ProjectReference.Name | Where-Object { $_ -and $_ -ne 'Assembly-CSharp' } | ForEach-Object { '-r:"Library/ScriptAssemblies/'+$_+'.dll"' }
    $editorSources = @($editorProject.Project.ItemGroup.Compile.Include | Where-Object { $_ -and (Test-Path -LiteralPath $_) })
    $editorSources += 'Assets/_Duskborn/Editor/IronrootPlayerBuilder.cs'
    $editorArgs += $editorSources | ForEach-Object { (Resolve-Path -LiteralPath $_).Path } | Sort-Object -Unique | ForEach-Object { '"'+$_+'"' }
    $editorFile = Join-Path $output 'editor.rsp'
    $editorArgs | Set-Content $editorFile
    & "$unityData/NetCoreRuntime/dotnet.exe" "$unityData/DotNetSdkRoslyn/csc.dll" "@$editorFile"
    if ($LASTEXITCODE -ne 0) { throw 'Editor compilation failed' }
    Write-Output 'Runtime and Ironroot editor C# compilation passed (offline; no Unity import/render validation).'
} finally { Pop-Location }


