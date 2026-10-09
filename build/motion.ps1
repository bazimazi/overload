param([switch]$Rendered,[switch]$Exported)
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    if(!$Exported){
        Invoke-Checked $DotNet @('build','src/Overload.Game/Overload.Game.csproj','-p:RestoreLockedMode=true')
        Invoke-Checked $Godot @('--headless','--path',$GamePath,'--editor','--import')
    }
    $motionExecutable=if($Exported){Join-Path $RepoRoot 'artifacts/windows/Overload.exe'}else{$Godot.Replace('_console.exe','.exe')}
    $motionModes=@('smoke');if($Rendered){$motionModes+='review'}
    foreach($motionMode in $motionModes){
        $motionArguments=if($Exported){@()}else{@('--path',('"'+$GamePath+'"'))}
        $motionArguments+=@('--resolution','1280x720','--fixed-fps','120')
        if($motionMode -eq 'smoke'){$motionArguments+='--headless'}else{$motionArguments+=@('--rendering-method','gl_compatibility')}
        $motionArguments+=@('--',('--motion-'+$motionMode))
        $motionLog=Join-Path $RepoRoot ('artifacts/motion-'+$motionMode+'.log')
        $motionErrors=Join-Path $RepoRoot ('artifacts/motion-'+$motionMode+'-errors.log')
        $motionProcess=Start-Process -FilePath $motionExecutable -ArgumentList $motionArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $motionLog -RedirectStandardError $motionErrors
        $null=$motionProcess.Handle
        $motionDeadline=[DateTime]::UtcNow.AddSeconds(120)
        while(!$motionProcess.WaitForExit(1000)){if([DateTime]::UtcNow -gt $motionDeadline){$motionProcess.Kill();throw 'Movement review timed out'}}
        $motionOutput=Get-Content $motionLog -Raw
        if($motionProcess.ExitCode -ne 0 -or $motionOutput -notmatch 'OVERLOAD_MOTION_OK' -or (Get-Content $motionErrors -Raw) -match 'ERROR:') {throw "Movement $motionMode failed; inspect $motionLog and $motionErrors"}
        Write-Output ($motionOutput -split "`n" | Select-Object -Last 2)
    }
}finally{Pop-Location}
