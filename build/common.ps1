$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_ROOT = Join-Path $env:ProgramFiles 'dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$DotNet = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
$Godot = Join-Path $RepoRoot '.tools/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
$GamePath = Join-Path $RepoRoot 'src/Overload.Game'
function Invoke-Checked {
    param([string]$Executable, [string[]]$Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE" }
}
