param([switch]$SkipExport, [ValidateSet('Slice','Endless','Production','Expansion')][string]$Variant = 'Slice')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    if (!$SkipExport) { & "$PSScriptRoot/export.ps1" -SmokeTest }
    $folder = Join-Path $RepoRoot 'artifacts/windows'
    if (!(Test-Path (Join-Path $folder 'Overload.exe'))) { throw 'Build the Windows export first' }
    $playtest = if ($Variant -eq 'Expansion') { 'docs/playtests/A03_CHECKS.md' } elseif ($Variant -eq 'Production') { 'docs/playtests/P07_CHECKS.md' } elseif ($Variant -eq 'Endless') { 'docs/playtests/E05_CHECKS.md' } else { 'docs/playtests/S06_FIRST_PLAYTEST.md' }
    Copy-Item -LiteralPath $playtest -Destination (Join-Path $folder 'PLAYTEST.md') -Force
    Copy-Item -LiteralPath 'docs/playtests/S06_FIRST_PLAYTEST.md' -Destination (Join-Path $folder 'S06_FIRST_PLAYTEST.md') -Force
    Copy-Item -LiteralPath 'src/Overload.Game/Assets/MANIFEST.md' -Destination (Join-Path $folder 'ASSET-PROVENANCE.md') -Force
    $manifestPath = Join-Path $folder 'BUILD-MANIFEST.json'
    $files = Get-ChildItem -LiteralPath $folder -Recurse -File | Where-Object { $_.FullName -ne $manifestPath } | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path = $_.FullName.Substring($folder.Length + 1).Replace('\', '/'); bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
    $manifest = [ordered]@{
        label = $(if ($Variant -eq 'Expansion') { 'A03 candidate - ' } elseif ($Variant -eq 'Production') { 'P07 candidate - ' } elseif ($Variant -eq 'Endless') { 'E05 candidate - ' } else { 'S06 candidate - ' }) + (Get-Date -Format 'yyyy-MM-dd')
        builtAtUtc = [DateTime]::UtcNow.ToString('o')
        sourceBaseCommit = (& git rev-parse HEAD).Trim()
        sourceHasUncommittedChanges = [bool](& git status --porcelain)
        independentPlaytest = 'pending'
        files = @($files)
    }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    $archiveName = if ($Variant -eq 'Expansion') { 'Overload-expansion-windows.zip' } elseif ($Variant -eq 'Production') { 'Overload-production-windows.zip' } elseif ($Variant -eq 'Endless') { 'Overload-endless-windows.zip' } else { 'Overload-slice-windows.zip' }
    $zip = Join-Path $RepoRoot ('artifacts/' + $archiveName)
    Compress-Archive -LiteralPath $folder -DestinationPath $zip -CompressionLevel Optimal -Force
    $hash = Get-FileHash -LiteralPath $zip -Algorithm SHA256
    ($hash.Hash + '  ' + $archiveName) | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ASCII
    Write-Output "Packaged $zip"
    Write-Output ($hash.Hash)
} finally { Pop-Location }
