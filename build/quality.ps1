param([ValidateSet('All','Soak','Performance','Matrix','Install')][string]$Mode='All', [switch]$Exported, [int]$SoakSeconds=3600, [int]$StressSeconds=300)
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    if (!$Exported) { Invoke-Checked $DotNet @('build', (Join-Path $GamePath 'Overload.Game.csproj'), '-c', 'Debug', '-p:RestoreLockedMode=true', '-p:UseSharedCompilation=false', '--disable-build-servers') }
    $runStarted=[DateTime]::UtcNow.ToString('o')
    $runtimeRoot=Join-Path $RepoRoot 'artifacts/windows'
    $runtimeHashes=if($Exported){@(Get-ChildItem -LiteralPath $runtimeRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{path=$_.FullName.Substring($runtimeRoot.Length+1);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })}else{@()}
    if($Mode -eq 'All'){
        New-Item -ItemType Directory -Force (Join-Path $RepoRoot 'artifacts/quality') | Out-Null
        [ordered]@{startedUtc=$runStarted;exported=[bool]$Exported;completed=$false} | ConvertTo-Json | Set-Content artifacts/quality/quality-run.json -Encoding UTF8
    }
    $executable = if ($Exported) { Join-Path $RepoRoot 'artifacts/windows/Overload.exe' } else { $Godot.Replace('_console.exe','.exe') }
    function Invoke-Quality([string]$Label, [string[]]$Arguments, [string]$Marker, [int]$TimeoutSeconds) {
        $stdout = Join-Path $RepoRoot ('artifacts/quality-' + $Label + '.log')
        $stderr = Join-Path $RepoRoot ('artifacts/quality-' + $Label + '-errors.log')
        $launchArguments = if ($Exported) { $Arguments } else { @('--path', ('"' + $GamePath + '"')) + $Arguments }
        $qualityProcess = Start-Process -FilePath $executable -ArgumentList $launchArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        $null = $qualityProcess.Handle
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        while (!$qualityProcess.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -gt $deadline) { $qualityProcess.Kill(); throw "Quality $Label timed out" }
        }
        $output = Get-Content -LiteralPath $stdout -Raw
        $errors = Get-Content -LiteralPath $stderr -Raw
        Write-Output $output
        if ($qualityProcess.ExitCode -ne 0 -or $output -notmatch $Marker -or $errors -match 'ERROR:') { throw "Quality $Label failed: $errors" }
    }
    if ($Mode -in @('All','Install')) { Invoke-Quality 'install' @('--headless','--fixed-fps','60','--','--quality-install') 'OVERLOAD_QUALITY_INSTALL_OK' 60 }
    if ($Mode -in @('All','Matrix')) { Invoke-Quality 'matrix' @('--','--quality-matrix') 'OVERLOAD_QUALITY_MATRIX_OK' 60 }
    if ($Mode -in @('All','Soak')) { Invoke-Quality 'soak' @('--headless','--',('--quality-soak=' + $SoakSeconds)) 'OVERLOAD_QUALITY_SOAK_OK' 1200 }
    if ($Mode -in @('All','Performance')) {
        Invoke-Quality 'performance' @('--resolution','1920x1080','--',('--quality-perf=' + $StressSeconds)) 'OVERLOAD_QUALITY_PERF_OK' ($StressSeconds + 90)
        Invoke-Quality 'ordinary-performance' @('--resolution','1920x1080','--','--quality-ordinary=60') 'OVERLOAD_QUALITY_PERF_OK' 150
        foreach ($report in @('performance.json','ordinary-performance.json')) {
            $measurement = Get-Content -LiteralPath (Join-Path $RepoRoot ('artifacts/quality/' + $report)) -Raw | ConvertFrom-Json
            if (!$measurement.targetPassed) { throw "Frame-time target missed: $report; measurement completed and evidence retained" }
        }
    }
    if($Mode -eq 'All'){
        foreach($file in $runtimeHashes){
            if((Get-FileHash -LiteralPath (Join-Path $runtimeRoot $file.path) -Algorithm SHA256).Hash -ne $file.sha256){throw 'Runtime changed during quality validation'}
        }
        $reportHashes=@(foreach($name in @('matrix.json','soak.json','performance.json','ordinary-performance.json','BALANCE.md')){
            [ordered]@{path=$name;sha256=(Get-FileHash -LiteralPath (Join-Path $RepoRoot ('artifacts/quality/'+$name)) -Algorithm SHA256).Hash}
        })
        [ordered]@{startedUtc=$runStarted;finishedUtc=[DateTime]::UtcNow.ToString('o');exported=[bool]$Exported;completed=$true;runtimeFiles=$runtimeHashes;reports=$reportHashes} | ConvertTo-Json -Depth 6 | Set-Content artifacts/quality/quality-run.json -Encoding UTF8
    }
} finally { Pop-Location }
