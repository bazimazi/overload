param([switch]$Rendered,[switch]$Exported)
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    if(!$Exported){
        Invoke-Checked $DotNet @('build','src/Overload.Game/Overload.Game.csproj','-p:RestoreLockedMode=true')
        Invoke-Checked $Godot @('--headless','--path',$GamePath,'--editor','--import')
    }
    $campaignExecutable=if($Exported){Join-Path $RepoRoot 'artifacts/windows/Overload.exe'}else{$Godot.Replace('_console.exe','.exe')}
    $campaignModes=@('smoke');if($Rendered){$campaignModes+='review'}
    foreach($campaignMode in $campaignModes){
        $campaignArguments=if($Exported){@()}else{@('--path',('"'+$GamePath+'"'))}
        $campaignArguments+=@('--resolution','1280x720','--fixed-fps','60')
        if($campaignMode -eq 'smoke'){$campaignArguments+='--headless'}else{$campaignArguments+=@('--rendering-method','gl_compatibility')}
        $campaignArguments+=@('--',('--campaign-'+$campaignMode))
        $campaignLog=Join-Path $RepoRoot ('artifacts/campaign-'+$campaignMode+'.log')
        $campaignErrors=Join-Path $RepoRoot ('artifacts/campaign-'+$campaignMode+'-errors.log')
        $campaignProcess=Start-Process -FilePath $campaignExecutable -ArgumentList $campaignArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $campaignLog -RedirectStandardError $campaignErrors
        $null=$campaignProcess.Handle
        $campaignDeadline=[DateTime]::UtcNow.AddSeconds(1500)
        while(!$campaignProcess.WaitForExit(1000)){if([DateTime]::UtcNow -gt $campaignDeadline){$campaignProcess.Kill();throw 'Campaign review timed out'}}
        $campaignOutput=Get-Content $campaignLog -Raw
        if($campaignProcess.ExitCode -ne 0 -or $campaignOutput -notmatch 'OVERLOAD_CAMPAIGN_OK' -or (Get-Content $campaignErrors -Raw) -match 'ERROR:') {throw "Campaign $campaignMode failed; inspect $campaignLog and $campaignErrors"}
        Write-Output ($campaignOutput -split "`n" | Select-Object -Last 2)
    }
}finally{Pop-Location}
