param([switch]$Review,[switch]$Performance,[switch]$Soak,[switch]$Exported,[int]$Seconds=30)
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    $worldOutput=Join-Path $RepoRoot 'artifacts/world'
    New-Item -ItemType Directory -Force $worldOutput | Out-Null
    if($Exported) { $worldEngine=Join-Path $RepoRoot 'artifacts/windows/Overload.exe';$worldArgs=@() }
    else {
        Invoke-Checked $DotNet @('build','src/Overload.Game/Overload.Game.csproj','-p:RestoreLockedMode=true')
        Invoke-Checked $Godot @('--headless','--path',$GamePath,'--editor','--import')
        $worldEngine=$Godot.Replace('_console.exe','.exe');$worldArgs=@('--path',('"'+$GamePath+'"'))
    }
    if($Review){$worldMode='review';$worldArgs+=@('--rendering-method','gl_compatibility','--resolution','1280x720','--fixed-fps','60','--','--world-review')}
    elseif($Performance){$worldMode='performance';$worldArgs+=@('--rendering-method','gl_compatibility','--resolution','1280x720','--',('--world-perf='+$Seconds))}
    elseif($Soak){$worldMode='soak';if(!$PSBoundParameters.ContainsKey('Seconds')){$Seconds=600};$worldArgs+=@('--headless','--',('--world-soak='+$Seconds))}
    else {$worldMode='smoke';$worldArgs+=@('--headless','--fixed-fps','60','--','--world-smoke')}
    $worldLog=Join-Path $worldOutput ('world-'+$worldMode+'.log');$worldErrors=Join-Path $worldOutput ('world-'+$worldMode+'-errors.log')
    $worldProcess=Start-Process -FilePath $worldEngine -ArgumentList $worldArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $worldLog -RedirectStandardError $worldErrors
    $null=$worldProcess.Handle;$worldProcess.WaitForExit()
    $worldResult=Get-Content $worldLog -Raw;$worldErrorText=Get-Content $worldErrors -Raw
    if($worldProcess.ExitCode-ne 0-or $worldErrorText-match 'ERROR:'-or $worldResult-notmatch 'OVERLOAD_WORLD_(SMOKE|QUALITY)_OK'){throw "World $worldMode failed. Inspect $worldLog and $worldErrors"}
    Write-Output ($worldResult -split "`n" | Select-Object -Last 3)
    if($Performance){$worldMetrics=Get-Content (Join-Path $worldOutput 'performance.json') -Raw | ConvertFrom-Json;if(!$worldMetrics.targetPassed){throw 'World performance missed its local frame-time target'}}
} finally { Pop-Location }
