param([string]$OutputDirectory = 'artifacts/balance-e05')
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    Invoke-Checked $DotNet @('run','--project','src/Overload.Tools','-c','Release','--','balance-report',$OutputDirectory)
} finally { Pop-Location }
