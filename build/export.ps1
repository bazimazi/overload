param([switch]$SmokeTest)
. "$PSScriptRoot/common.ps1"
$env:RestoreLockedMode = 'true'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
Push-Location $RepoRoot
try {
    New-Item -ItemType Directory -Force 'artifacts/windows' | Out-Null
    Invoke-Checked $DotNet @('build', 'Overload.sln', '-c', 'Release', '-p:RestoreLockedMode=true')
    # Redirect the actual editor, since its Windows console helper may linger after exit.
    function Invoke-ExportEditor([string[]]$Arguments, [string]$Label) {
        $editor = $Godot.Replace('_console.exe', '.exe')
        $stdout = Join-Path $RepoRoot ('artifacts/' + $Label + '.log')
        $stderr = Join-Path $RepoRoot ('artifacts/' + $Label + '-errors.log')
        $editorProcess = Start-Process -FilePath $editor -ArgumentList $Arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        $null = $editorProcess.Handle
        if (!$editorProcess.WaitForExit(60000)) { $editorProcess.Kill(); throw "Godot $Label timed out" }
        Write-Output (Get-Content $stdout -Raw)
        $errors = Get-Content $stderr -Raw
        if ($editorProcess.ExitCode -ne 0 -or $errors -match 'ERROR:') { throw "Godot $Label failed: $errors" }
    }
    Invoke-ExportEditor @('--headless', '--path', ('"' + $GamePath + '"'), '--editor', '--import') 'export-import'
    Invoke-ExportEditor @('--headless', '--path', ('"' + $GamePath + '"'), '--export-release', '"Windows Desktop"') 'export-engine'
    if ($SmokeTest) {
        $stdout = Join-Path $RepoRoot 'artifacts/export-smoke.log'
        $stderr = Join-Path $RepoRoot 'artifacts/export-smoke-errors.log'
        $process = Start-Process -FilePath (Join-Path $RepoRoot 'artifacts/windows/Overload.exe') -ArgumentList @('--headless', '--fixed-fps', '60', '--', '--smoke-test') -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        $null = $process.Handle # Retain the process handle so Windows PowerShell preserves ExitCode.
        if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Exported smoke test timed out' }
        $output = Get-Content $stdout -Raw
        Write-Output $output
        $errors = Get-Content $stderr -Raw
        if ($process.ExitCode -ne 0 -or $output -notmatch 'OVERLOAD_SMOKE_OK' -or $errors -match 'ERROR:') { throw "Exported smoke failed: $errors" }
        $stdout = Join-Path $RepoRoot 'artifacts/export-endless-smoke.log'
        $stderr = Join-Path $RepoRoot 'artifacts/export-endless-smoke-errors.log'
        $process = Start-Process -FilePath (Join-Path $RepoRoot 'artifacts/windows/Overload.exe') -ArgumentList @('--headless', '--fixed-fps', '60', '--', '--endless-smoke') -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        $null = $process.Handle
        if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Exported endless smoke test timed out' }
        $output = Get-Content $stdout -Raw
        Write-Output $output
        $errors = Get-Content $stderr -Raw
        if ($process.ExitCode -ne 0 -or $output -notmatch 'OVERLOAD_ENDLESS_SMOKE_OK' -or $errors -match 'ERROR:') { throw "Exported endless smoke failed: $errors" }
        $stdout = Join-Path $RepoRoot 'artifacts/export-production-smoke.log'
        $stderr = Join-Path $RepoRoot 'artifacts/export-production-smoke-errors.log'
        $process = Start-Process -FilePath (Join-Path $RepoRoot 'artifacts/windows/Overload.exe') -ArgumentList @('--headless', '--fixed-fps', '60', '--', '--production-smoke') -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        $null = $process.Handle
        if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Exported production smoke test timed out' }
        $output = Get-Content $stdout -Raw
        Write-Output $output
        $errors = Get-Content $stderr -Raw
        if ($process.ExitCode -ne 0 -or $output -notmatch 'OVERLOAD_PRODUCTION_SMOKE_OK' -or $errors -match 'ERROR:') { throw "Exported production smoke failed: $errors" }

        $stdout = Join-Path $RepoRoot 'artifacts/export-expansion-smoke.log'
        $stderr = Join-Path $RepoRoot 'artifacts/export-expansion-smoke-errors.log'
        $process = Start-Process -FilePath (Join-Path $RepoRoot 'artifacts/windows/Overload.exe') -ArgumentList @('--headless', '--fixed-fps', '60', '--', '--expansion-smoke') -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        $null = $process.Handle
        if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Exported expansion smoke test timed out' }
        $output = Get-Content $stdout -Raw
        Write-Output $output
        $errors = Get-Content $stderr -Raw
        if ($process.ExitCode -ne 0 -or $output -notmatch 'OVERLOAD_EXPANSION_SMOKE_OK' -or $errors -match 'ERROR:') { throw "Exported expansion smoke failed: $errors" }
        $stdout = Join-Path $RepoRoot 'artifacts/export-experience-smoke.log'
        $stderr = Join-Path $RepoRoot 'artifacts/export-experience-smoke-errors.log'
        $process = Start-Process -FilePath (Join-Path $RepoRoot 'artifacts/windows/Overload.exe') -ArgumentList @('--headless', '--fixed-fps', '60', '--', '--experience-smoke') -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        $null = $process.Handle
        if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Exported experience smoke test timed out' }
        $output = Get-Content $stdout -Raw
        Write-Output $output
        $errors = Get-Content $stderr -Raw
        if ($process.ExitCode -ne 0 -or $output -notmatch 'OVERLOAD_EXPERIENCE_OK' -or $errors -match 'ERROR:') { throw "Exported experience smoke failed: $errors" }

        $stdout = Join-Path $RepoRoot 'artifacts/export-world-smoke.log'
        $stderr = Join-Path $RepoRoot 'artifacts/export-world-smoke-errors.log'
        $process = Start-Process -FilePath (Join-Path $RepoRoot 'artifacts/windows/Overload.exe') -ArgumentList @('--headless', '--fixed-fps', '60', '--', '--world-smoke') -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        $null = $process.Handle
        if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Exported world smoke test timed out' }
        $output = Get-Content $stdout -Raw
        Write-Output $output
        $errors = Get-Content $stderr -Raw
        if ($process.ExitCode -ne 0 -or $output -notmatch 'OVERLOAD_WORLD_SMOKE_OK' -or $errors -match 'ERROR:') { throw "Exported world smoke failed: $errors" }
    }
} finally { Pop-Location }
