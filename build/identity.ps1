param([switch]$Rendered,[switch]$Exported)
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    if(!$Exported){
        Invoke-Checked $DotNet @('build','src/Overload.Game/Overload.Game.csproj','-p:RestoreLockedMode=true')
        Invoke-Checked $Godot @('--headless','--path',$GamePath,'--editor','--import')
    }
    $identityExecutable=if($Exported){Join-Path $RepoRoot 'artifacts/windows/Overload.exe'}else{$Godot.Replace('_console.exe','.exe')}
    $identityModes=@('smoke');if($Rendered){$identityModes+='review'}
    foreach($identityMode in $identityModes){
        $identityArgs=if($Exported){@()}else{@('--path',('"'+$GamePath+'"'))}
        if($identityMode-eq 'smoke'){$identityArgs+=@('--headless','--fixed-fps','60')}
        else{$identityArgs+=@('--rendering-method','gl_compatibility','--resolution','1280x720','--fixed-fps','60')}
        $identityArgs+=@('--',('--identity-'+$identityMode))
        $identityLog=Join-Path $RepoRoot ('artifacts/identity-'+$identityMode+'.log')
        $identityErrors=Join-Path $RepoRoot ('artifacts/identity-'+$identityMode+'-errors.log')
        $identityProcess=Start-Process -FilePath $identityExecutable -ArgumentList $identityArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $identityLog -RedirectStandardError $identityErrors
        $null=$identityProcess.Handle
        $identityDeadline=[DateTime]::UtcNow.AddSeconds(90)
        while(!$identityProcess.WaitForExit(1000)){if([DateTime]::UtcNow-gt $identityDeadline){$identityProcess.Kill();throw 'Identity review timed out'}}
        $identityOutput=Get-Content $identityLog -Raw
        if($identityProcess.ExitCode-ne 0-or $identityOutput-notmatch 'OVERLOAD_IDENTITY_OK'-or (Get-Content $identityErrors -Raw)-match 'ERROR:'){throw "Identity $identityMode failed; inspect $identityLog and $identityErrors"}
        Write-Output ($identityOutput-split "`n"|Select-Object -Last 2)
    }
}finally{Pop-Location}
