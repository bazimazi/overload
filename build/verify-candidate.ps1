param([Parameter(Mandatory=$true)][string]$Archive)
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    $archivePath = (Resolve-Path -LiteralPath $Archive).Path
    $expected = (Get-Content -LiteralPath ($archivePath + '.sha256') -Raw).Split(' ')[0].Trim()
    if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expected) { throw 'Archive SHA256 mismatch' }
    $installFolder = Join-Path $RepoRoot ('artifacts/clean install ' + [Guid]::NewGuid().ToString('N'))
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        foreach ($entry in $zip.Entries) {
            $target = [IO.Path]::GetFullPath((Join-Path $installFolder $entry.FullName))
            if (!$target.StartsWith($installFolder + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Archive path escapes clean install' }
        }
    } finally { $zip.Dispose() }
    Expand-Archive -LiteralPath $archivePath -DestinationPath $installFolder
    $manifest = Get-Content -LiteralPath (Join-Path $installFolder 'ARCHIVE-MANIFEST.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($entry in $manifest) {
        $path = [IO.Path]::GetFullPath((Join-Path $installFolder $entry.path))
        if (!$path.StartsWith($installFolder + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escapes install' }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) { throw "File hash mismatch: $($entry.path)" }
    }
    $sourceFolder = Join-Path $installFolder 'expanded source'
    $sourceZip = [IO.Compression.ZipFile]::OpenRead((Join-Path $installFolder 'source.zip'))
    try {
        foreach ($entry in $sourceZip.Entries) {
            $target = [IO.Path]::GetFullPath((Join-Path $sourceFolder $entry.FullName))
            if (!$target.StartsWith($sourceFolder + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Source ZIP path escapes extraction' }
        }
    } finally { $sourceZip.Dispose() }
    Expand-Archive -LiteralPath (Join-Path $installFolder 'source.zip') -DestinationPath $sourceFolder
    $sources = Get-Content -LiteralPath (Join-Path $installFolder 'SOURCE-MANIFEST.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($entry in $sources) {
        $path = [IO.Path]::GetFullPath((Join-Path (Join-Path $sourceFolder 'source') $entry.path))
        if (!$path.StartsWith($sourceFolder + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Source path escapes extraction' }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Source hash mismatch: $($entry.path)" }
    }
    $executable = Join-Path $installFolder 'windows/Overload.exe'
    foreach ($suite in @('smoke-test','endless-smoke','production-smoke','expansion-smoke','quality-install')) {
        $stdout = Join-Path $installFolder ($suite + '.log')
        $stderr = Join-Path $installFolder ($suite + '-errors.log')
        $candidateProcess = Start-Process -FilePath $executable -WorkingDirectory $installFolder -ArgumentList @('--headless','--fixed-fps','60','--',('--' + $suite)) -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        $null = $candidateProcess.Handle
        if (!$candidateProcess.WaitForExit(60000)) { $candidateProcess.Kill(); throw "Clean install $suite timed out" }
        $output = Get-Content -LiteralPath $stdout -Raw
        $errors = Get-Content -LiteralPath $stderr -Raw
        if ($candidateProcess.ExitCode -ne 0 -or $output -notmatch 'OVERLOAD_.*_OK' -or $errors -match 'ERROR:') { throw "Clean install $suite failed: $errors" }
        Write-Output (($output -split "`n" | Where-Object {$_ -match '^OVERLOAD_.*_OK'}) -join "`n")
    }
    [ordered]@{archiveSha256=$expected;packageFiles=$manifest.Count;sourceFiles=$sources.Count;isolatedCleanInstall=$installFolder;launchSuites=5;passed=$true} | ConvertTo-Json | Set-Content artifacts/clean-install-verification.json -Encoding UTF8
    Write-Output "CLEAN_INSTALL_OK packageFiles=$($manifest.Count) sourceFiles=$($sources.Count)"
} finally { Pop-Location }
