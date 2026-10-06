param([switch]$Editor)
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    Invoke-Checked $DotNet @('build', 'src/Overload.Game/Overload.Game.csproj', '-p:RestoreLockedMode=true')
    $arguments = @('--path', $GamePath)
    if ($Editor) { $arguments += '--editor' }
    Invoke-Checked $Godot $arguments
} finally { Pop-Location }
