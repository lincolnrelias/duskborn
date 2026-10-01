param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $SfxArguments
)
$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot 'natural-game-sfx\scripts\sfx.py'
$runtime = Get-Command python -ErrorAction SilentlyContinue
if ($runtime) {
    $pythonPath = $runtime.Source
} else {
    $pythonPath = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
}
if (-not (Test-Path -LiteralPath $pythonPath)) {
    throw 'Python is unavailable. Locate the bundled Python with load_workspace_dependencies.'
}
& $pythonPath $helper @SfxArguments
exit $LASTEXITCODE
