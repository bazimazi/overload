param([string[]]$GameArgs=@())
$ErrorActionPreference='Stop'
$gameExecutable=Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/windows/Overload.exe'
if(!(Test-Path -LiteralPath $gameExecutable)){throw 'Export the game first with build/export.ps1.'}
& $gameExecutable @GameArgs
exit $LASTEXITCODE
