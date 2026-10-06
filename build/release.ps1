param([switch]$SkipExport)
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    if (!$SkipExport) {
        & "$PSScriptRoot/export.ps1" -SmokeTest
        & "$PSScriptRoot/quality.ps1" -Exported
    }
    foreach ($name in @('matrix.json','soak.json','performance.json','ordinary-performance.json','BALANCE.md')) {
        if (!(Test-Path -LiteralPath (Join-Path $RepoRoot ('artifacts/quality/' + $name)))) { throw "Run quality validation first: missing $name" }
    }
    $qualityRun=Get-Content artifacts/quality/quality-run.json -Raw -Encoding UTF8 | ConvertFrom-Json
    if(!$qualityRun.completed -or !$qualityRun.exported -or $qualityRun.runtimeFiles.Count -lt 1){throw 'Completed standalone quality validation is required'}
    foreach($file in $qualityRun.runtimeFiles){
        if((Get-FileHash -LiteralPath (Join-Path $RepoRoot ('artifacts/windows/'+$file.path)) -Algorithm SHA256).Hash -ne $file.sha256){throw 'Runtime differs from quality-tested files; rerun build/quality.ps1 -Exported'}
    }
    foreach($report in $qualityRun.reports){
        if((Get-FileHash -LiteralPath (Join-Path $RepoRoot ('artifacts/quality/'+$report.path)) -Algorithm SHA256).Hash -ne $report.sha256){throw 'Quality evidence changed; rerun the full standalone quality suite'}
    }
    $soakResult = Get-Content artifacts/quality/soak.json -Raw | ConvertFrom-Json
    $stressResult = Get-Content artifacts/quality/performance.json -Raw | ConvertFrom-Json
    $matrixResult = Get-Content artifacts/quality/matrix.json -Raw | ConvertFrom-Json
    if ($soakResult.simulationSeconds -lt 3600 -or $soakResult.checkpoints -lt 120) { throw 'Full simulated-hour save loop is required' }
    if ($stressResult.seconds -lt 300 -or !$stressResult.targetPassed -or $stressResult.overloadActions -lt 1) { throw 'Five-minute rendered stress with Overload must pass before packaging' }
    if ($matrixResult.displays.Count -lt 80) { throw 'Complete menu/display matrix is required' }
    $candidateId = 'rc-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
    $candidateRoot = Join-Path $RepoRoot ('artifacts/release/' + $candidateId)
    New-Item -ItemType Directory -Path $candidateRoot | Out-Null
    $runtimeFolder = Join-Path $candidateRoot 'windows'
    Copy-Item -LiteralPath (Join-Path $RepoRoot 'artifacts/windows') -Destination $runtimeFolder -Recurse
    Copy-Item -LiteralPath 'docs/RELEASE_REVIEW.md' -Destination (Join-Path $runtimeFolder 'PLAYTEST.md') -Force
    Copy-Item -LiteralPath 'docs/RELEASE_REVIEW.md' -Destination (Join-Path $runtimeFolder 'RELEASE-REVIEW.md')
    Copy-Item -LiteralPath 'docs/playtests/S06_FIRST_PLAYTEST.md' -Destination (Join-Path $runtimeFolder 'S06_FIRST_PLAYTEST.md')
    Copy-Item -LiteralPath 'docs/Q_RELEASE_VALIDATION.md' -Destination (Join-Path $runtimeFolder 'Q_RELEASE_VALIDATION.md')
    Copy-Item -LiteralPath 'src/Overload.Game/Assets/MANIFEST.md' -Destination (Join-Path $runtimeFolder 'ASSET-PROVENANCE.md') -Force
    Invoke-Checked $Godot @('--headless','--path',$GamePath,'--script',(Join-Path $PSScriptRoot 'export-notices.gd'))
    Copy-Item -LiteralPath 'artifacts/GODOT-NOTICES.txt' -Destination (Join-Path $runtimeFolder 'GODOT-NOTICES.txt')
    $runtimeConfig = Get-Content (Join-Path $runtimeFolder 'data_Overload.Game_windows_x86_64/Overload.Game.runtimeconfig.json') -Raw | ConvertFrom-Json
    $runtimeVersion = ($runtimeConfig.runtimeOptions.includedFrameworks | Where-Object {$_.name -eq 'Microsoft.NETCore.App'}).version
    $nugetRoot = (& $DotNet nuget locals global-packages --list) -replace '^global-packages: ',''
    $runtimePack = Join-Path $nugetRoot ('microsoft.netcore.app.runtime.win-x64/' + $runtimeVersion)
    Copy-Item -LiteralPath (Join-Path $runtimePack 'LICENSE.TXT') -Destination (Join-Path $runtimeFolder 'DOTNET-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $runtimePack 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $runtimeFolder 'DOTNET-NOTICES.txt')
    # Archive the actual working source, including untracked authored files and dirty tracked edits.
    $sourceFolder = Join-Path $candidateRoot 'source'
    New-Item -ItemType Directory -Path $sourceFolder | Out-Null
    $sourceFiles = @(& git -c core.quotepath=false ls-files --cached --others --exclude-standard | Sort-Object -Unique)
    $sourceManifest = foreach ($relative in $sourceFiles) {
        $sourcePath = [IO.Path]::GetFullPath((Join-Path $RepoRoot $relative))
        if (!$sourcePath.StartsWith($RepoRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Source path escapes repository' }
        if (!(Test-Path -LiteralPath $sourcePath -PathType Leaf)) { continue }
        $destination = Join-Path $sourceFolder $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        Copy-Item -LiteralPath $sourcePath -Destination $destination
        [ordered]@{path=$relative.Replace('\','/'); sha256=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash}
    }
    $sourceManifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $candidateRoot 'SOURCE-MANIFEST.json') -Encoding UTF8
    @($sourceManifest | Where-Object { $_.path.StartsWith('src/Overload.Game/Content/') -or $_.path -eq 'assets/source/manifest.json' }) | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $candidateRoot 'CONTENT-MANIFEST.json') -Encoding UTF8
    Compress-Archive -LiteralPath $sourceFolder -DestinationPath (Join-Path $candidateRoot 'source.zip') -CompressionLevel Optimal
    $toolchainFiles = @('global.json','Directory.Build.props','.tools/Godot_v4.7.2-stable_mono_win64.zip','.tools/Godot_v4.7.2-stable_mono_export_templates.tpz','.tools/SHA512-SUMS.txt') + @($sourceFiles | Where-Object { $_ -like '*lock.json' })
    $toolHashes = foreach ($relative in $toolchainFiles) { [ordered]@{path=$relative;sha256=(Get-FileHash -LiteralPath $relative -Algorithm SHA256).Hash} }
    $toolchain = [ordered]@{godot='4.7.2 .NET';sdk=(& $DotNet --version).Trim();dotnetHostSha256=(Get-FileHash -LiteralPath $DotNet -Algorithm SHA256).Hash;files=@($toolHashes);rebuild='Extract source.zip, install the pinned SDK and hashed Godot packages, then run build/test.ps1 -Engine and build/export.ps1 -SmokeTest. Byte-identical rebuilding has not been established.'}
    $toolchain | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $candidateRoot 'TOOLCHAIN.json') -Encoding UTF8
    & $DotNet --info | Set-Content (Join-Path $candidateRoot 'DOTNET-INFO.txt') -Encoding UTF8
    Copy-Item -LiteralPath 'artifacts/quality' -Destination (Join-Path $candidateRoot 'evidence') -Recurse
    foreach ($log in @(Get-ChildItem artifacts -File | Where-Object { $_.Name -like 'quality-*.log' -or $_.Name -like 'r01-*.log' -or $_.Name -like 'export-*-smoke*.log' })) {
        Copy-Item -LiteralPath $log.FullName -Destination (Join-Path $candidateRoot 'evidence')
    }
    $manifestPath = Join-Path $runtimeFolder 'BUILD-MANIFEST.json'
    $runtimeFiles = @(Get-ChildItem -LiteralPath $runtimeFolder -File -Recurse | Where-Object {$_.FullName -ne $manifestPath} | Sort-Object FullName | ForEach-Object {
        [ordered]@{path=$_.FullName.Substring($runtimeFolder.Length+1).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
    $manifest = [ordered]@{label=$candidateId;builtAtUtc=[DateTime]::UtcNow.ToString('o');sourceBaseCommit=(& git rev-parse HEAD).Trim();sourceHasUncommittedChanges=[bool](& git status --porcelain);sourceManifestSha256=(Get-FileHash (Join-Path $candidateRoot 'SOURCE-MANIFEST.json') -Algorithm SHA256).Hash;humanReleaseReview='pending';independentPlaytest='pending';expansionChoice='waiting for real player evidence';files=$runtimeFiles}
    $manifest | ConvertTo-Json -Depth 6 | Set-Content $manifestPath -Encoding UTF8
    $packageManifest = @(Get-ChildItem -LiteralPath $candidateRoot -File -Recurse | Where-Object { !$_.FullName.StartsWith($sourceFolder + '\',[StringComparison]::OrdinalIgnoreCase) } | Sort-Object FullName | ForEach-Object {
        [ordered]@{path=$_.FullName.Substring($candidateRoot.Length+1).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
    $packageManifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $candidateRoot 'ARCHIVE-MANIFEST.json') -Encoding UTF8
    $zipPath = Join-Path $RepoRoot ('artifacts/Overload-' + $candidateId + '-windows.zip')
    # Source files are already in source.zip. Keep the review package compact.
    $packageParts = @(Get-ChildItem -LiteralPath $candidateRoot | Where-Object {$_.Name -ne 'source'} | ForEach-Object {$_.FullName})
    Compress-Archive -LiteralPath $packageParts -DestinationPath $zipPath -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    ($hash + '  ' + [IO.Path]::GetFileName($zipPath)) | Set-Content ($zipPath + '.sha256') -Encoding ASCII
    [ordered]@{candidate=$candidateRoot;archive=$zipPath;sha256=$hash;humanReview='pending';expansion='pending player evidence'} | ConvertTo-Json | Set-Content artifacts/LATEST-CANDIDATE.json -Encoding UTF8
    Write-Output "Review candidate: $zipPath"
    Write-Output $hash
} finally { Pop-Location }
