param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$output = Join-Path $projectRoot 'Temp/EngineeringAudit'
New-Item -ItemType Directory -Force $output | Out-Null
Push-Location $projectRoot
try {
    # Use Unity's cached compiler inputs without launching Unity or writing into Bee.
    $cached = Get-ChildItem 'Library/Bee/artifacts' -Filter 'Assembly-CSharp-Editor.rsp' -Recurse |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $cached) { throw 'No cached Editor compiler inputs; close Unity and run Tools/unity.ps1 compile first.' }
    foreach ($assembly in @('Assembly-CSharp', 'Assembly-CSharp-Editor')) {
        $inputFile = Join-Path $cached.DirectoryName "$assembly.rsp"
        $response = Join-Path $output "$assembly.rsp"
        $lines = Get-Content -LiteralPath $inputFile | ForEach-Object {
            if ($_ -match '^-(out|refout):') { return }
            if ($assembly -eq 'Assembly-CSharp-Editor' -and $_ -match '^-r:.*[/\\]Assembly-CSharp\.ref\.dll"$') {
                '-r:"' + $output + '/Assembly-CSharp.dll"'
            } else { $_ }
        }
        $lines += '-out:"' + $output + '/' + $assembly + '.dll"'
        $lines | Set-Content -LiteralPath $response
        & "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" "@$response"
        if ($LASTEXITCODE -ne 0) { throw "Offline $assembly compilation failed." }
        Write-Output "PASS offline $assembly compilation (cached Unity references; no import or FishNet weaving)"
    }
} finally { Pop-Location }
