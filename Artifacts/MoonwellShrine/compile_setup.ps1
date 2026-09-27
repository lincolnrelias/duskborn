$ErrorActionPreference = 'Stop'
$unityData = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data'
$outputDir = Join-Path $PSScriptRoot 'ValidationOutput'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$sourceFile = Join-Path $PSScriptRoot 'UnityExport/MoonwellShrine/Editor/MoonwellUnitySetup.cs'
$responseFile = Join-Path $outputDir 'compile.rsp'
$lines = @('/nostdlib+', '/target:library', '/langversion:latest', '/warn:4')
$lines += '/out:"' + (Join-Path $outputDir 'MoonwellUnitySetup.dll') + '"'
foreach ($assembly in (Get-ChildItem "$unityData/NetStandard/ref/2.1.0" -Filter '*.dll')) {
    $lines += '/reference:"' + $assembly.FullName + '"'
}
foreach ($assembly in (Get-ChildItem "$unityData/Managed/UnityEngine" -Filter '*.dll')) {
    $lines += '/reference:"' + $assembly.FullName + '"'
}
$lines += '"' + $sourceFile + '"'
[System.IO.File]::WriteAllLines($responseFile, $lines)
& 'C:/Program Files/dotnet/dotnet.exe' 'C:/Program Files/dotnet/sdk/9.0.302/Roslyn/bincore/csc.dll' /noconfig "@$responseFile"
if ($LASTEXITCODE -ne 0) { throw "Offline C# compilation failed: $LASTEXITCODE" }
