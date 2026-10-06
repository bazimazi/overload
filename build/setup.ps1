. "$PSScriptRoot/common.ps1"
$cache = Join-Path $RepoRoot '.tools'
$ProgressPreference = 'SilentlyContinue'
New-Item -ItemType Directory -Force $cache | Out-Null
$base = 'https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable'
$files = @('Godot_v4.7.2-stable_mono_win64.zip', 'Godot_v4.7.2-stable_mono_export_templates.tpz', 'SHA512-SUMS.txt')
foreach ($file in $files) {
    $destination = Join-Path $cache $file
    if (!(Test-Path $destination)) { Invoke-WebRequest "$base/$file" -OutFile $destination -UseBasicParsing }
}
$checksums = Get-Content (Join-Path $cache 'SHA512-SUMS.txt')
$pinned = Get-Content (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
foreach ($file in $files[0..1]) {
    $entry = $checksums | Where-Object { $_ -match ([regex]::Escape($file) + '$') }
    if (!$entry) { throw "No published checksum for $file" }
    $expected = ($entry -split '\s+')[0]
    $actual = (Get-FileHash (Join-Path $cache $file) -Algorithm SHA512).Hash
    $recorded = ($pinned.downloads | Where-Object { $_.file -eq $file }).sha512
    if ($actual -ne $expected -or $actual -ne $recorded) { throw "Checksum mismatch for $file. Remove the incomplete download and rerun setup." }
    Write-Output "$file SHA512 $actual"
}
if (!(Test-Path $Godot)) { Expand-Archive (Join-Path $cache $files[0]) $cache -Force }
$templates = Join-Path $env:APPDATA 'Godot/export_templates/4.7.2.stable.mono'
if (!(Test-Path (Join-Path $templates 'windows_release_x86_64.exe'))) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $cache $files[1]))
    try {
        foreach ($entry in $archive.Entries) {
            # Only Windows x64 is needed for the first target.
            $relative = $entry.FullName -replace '^templates/', ''
            if ($relative -notmatch '^(windows_|version.txt)' -or $relative -match 'arm64|x86_32') { continue }
            $target = [IO.Path]::GetFullPath((Join-Path $templates $relative))
            $prefix = [IO.Path]::GetFullPath($templates) + [IO.Path]::DirectorySeparatorChar
            if (!$target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe archive path' }
            if ($entry.Name -eq '') { continue }
            New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
    } finally { $archive.Dispose() }
}
Invoke-Checked $Godot @('--version')
Invoke-Checked $DotNet @('--version')
