param([switch]$Rendered, [switch]$Exported)
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    if (!$Exported) {
        Invoke-Checked $DotNet @('build', 'src/Overload.Game/Overload.Game.csproj', '-p:RestoreLockedMode=true')
        Invoke-Checked $Godot @('--headless', '--path', $GamePath, '--editor', '--import')
    }
    $executable = if ($Exported) { Join-Path $RepoRoot 'artifacts/windows/Overload.exe' } else { $Godot.Replace('_console.exe','.exe') }
    $baseArguments = if ($Exported) { @() } else { @('--path', ('"' + $GamePath + '"')) }
    $modes = @('smoke'); if ($Rendered) { $modes += 'review' }
    foreach ($mode in $modes) {
        $arguments = $baseArguments
        if ($mode -eq 'smoke') { $arguments += @('--headless', '--fixed-fps', '60') }
        $arguments += @('--', ('--experience-' + $mode))
        $stdout = Join-Path $RepoRoot ('artifacts/experience-' + $mode + '.log')
        $stderr = Join-Path $RepoRoot ('artifacts/experience-' + $mode + '-errors.log')
        $process = Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        $null = $process.Handle
        $deadline = [DateTime]::UtcNow.AddSeconds(120)
        while (!$process.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw 'Experience review timed out' }
        }
        $output = Get-Content -LiteralPath $stdout -Raw; $errors = Get-Content -LiteralPath $stderr -Raw
        Write-Output $output
        if ($process.ExitCode -ne 0 -or $output -notmatch 'OVERLOAD_EXPERIENCE_OK' -or $errors -match 'ERROR:') { throw "Experience $mode failed: $errors" }
    }
} finally { Pop-Location }
