$ErrorActionPreference = 'Stop'
$crystalSourceRoot = Join-Path $PSScriptRoot 'source'
New-Item -ItemType Directory -Force $crystalSourceRoot | Out-Null
foreach ($crystalSoundId in @('3180', '3181', '3182', '3183')) {
    $crystalThunderNumber = [int]$crystalSoundId - 3173
    $crystalSourcePage = "https://bigsoundbank.com/thunder-$crystalThunderNumber-s$crystalSoundId.html"
    $crystalLicenseResponse = Invoke-WebRequest -Uri $crystalSourcePage
    if ($crystalLicenseResponse.Content -notmatch 'CC0') { throw "Missing CC0 evidence: $crystalSourcePage" }
    $crystalLicenseResponse.Content | Set-Content (Join-Path $crystalSourceRoot "$crystalSoundId-page.html")
    $crystalDownloadResponse = Invoke-WebRequest -Uri 'https://bigsoundbank.com/download.php' -Method Post -Body @{id=$crystalSoundId;format='wav'} -SessionVariable crystalDownloadSession
    $crystalDownloadResponse.Content | Set-Content (Join-Path $crystalSourceRoot "$crystalSoundId-download.html")
    $crystalFormBody = @{}
    foreach ($crystalInput in [regex]::Matches($crystalDownloadResponse.Content, '<input[^>]+>')) {
        $crystalFieldName = [regex]::Match($crystalInput.Value, 'name="([^"]+)"').Groups[1].Value
        $crystalFieldValue = [regex]::Match($crystalInput.Value, 'value="([^"]*)"').Groups[1].Value
        if ($crystalFieldName -and $crystalFieldName -ne 'button') { $crystalFormBody[$crystalFieldName] = $crystalFieldValue }
    }
    Start-Sleep -Seconds 5
    Invoke-WebRequest -Uri 'https://bigsoundbank.com/modules/telecharger.php' -Method Post -Body $crystalFormBody -WebSession $crystalDownloadSession -OutFile (Join-Path $crystalSourceRoot "$crystalSoundId.wav")
}
Get-ChildItem $crystalSourceRoot -Filter '*.wav' | Select-Object Name,Length
