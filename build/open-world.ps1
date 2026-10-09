param([switch]$Rendered,[switch]$Exported)
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    if(!$Exported){
        Invoke-Checked $DotNet @('build','src/Overload.Game/Overload.Game.csproj','-p:RestoreLockedMode=true')
        Invoke-Checked $Godot @('--headless','--path',$GamePath,'--editor','--import')
    }
    $worldExecutable=if($Exported){Join-Path $RepoRoot 'artifacts/windows/Overload.exe'}else{$Godot.Replace('_console.exe','.exe')}
    $worldModes=@('smoke');if($Rendered){$worldModes+='review'}
    foreach($worldMode in $worldModes){
        $worldArguments=if($Exported){@()}else{@('--path',('"'+$GamePath+'"'))}
        $worldArguments+=@('--resolution','1280x720','--fixed-fps','60')
        if($worldMode -eq 'smoke'){$worldArguments+='--headless'}else{$worldArguments+=@('--rendering-method','gl_compatibility')}
        $worldArguments+=@('--',('--open-world-'+$worldMode))
        $worldLog=Join-Path $RepoRoot ('artifacts/open-world-'+$worldMode+'.log')
        $worldErrors=Join-Path $RepoRoot ('artifacts/open-world-'+$worldMode+'-errors.log')
        $worldProcess=Start-Process -FilePath $worldExecutable -ArgumentList $worldArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $worldLog -RedirectStandardError $worldErrors
        $null=$worldProcess.Handle
        $worldDeadline=[DateTime]::UtcNow.AddSeconds(120)
        while(!$worldProcess.WaitForExit(1000)){if([DateTime]::UtcNow -gt $worldDeadline){$worldProcess.Kill();throw 'Open-world review timed out'}}
        $worldOutput=Get-Content $worldLog -Raw
        if($worldProcess.ExitCode -ne 0 -or $worldOutput -notmatch 'OVERLOAD_OPEN_WORLD_OK' -or (Get-Content $worldErrors -Raw) -match 'ERROR:') {throw "Open-world $worldMode failed; inspect $worldLog and $worldErrors"}
        Write-Output ($worldOutput -split "`n" | Select-Object -Last 2)
    }
}finally{Pop-Location}
