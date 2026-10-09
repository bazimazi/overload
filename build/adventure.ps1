param([switch]$Rendered,[switch]$Exported)
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    if(!$Exported){
        Invoke-Checked $DotNet @('build','src/Overload.Game/Overload.Game.csproj','-p:RestoreLockedMode=true')
        Invoke-Checked $Godot @('--headless','--path',$GamePath,'--editor','--import')
    }
    $adventureExecutable=if($Exported){Join-Path $RepoRoot 'artifacts/windows/Overload.exe'}else{$Godot.Replace('_console.exe','.exe')}
    $adventureModes=@('smoke');if($Rendered){$adventureModes+='review'}
    foreach($adventureMode in $adventureModes){
        $adventureArguments=if($Exported){@()}else{@('--path',('"'+$GamePath+'"'))}
        $adventureArguments+=@('--resolution','1280x720','--fixed-fps','120')
        if($adventureMode -eq 'smoke'){$adventureArguments+='--headless'}else{$adventureArguments+=@('--rendering-method','gl_compatibility')}
        $adventureArguments+=@('--',('--adventure-'+$adventureMode))
        $adventureLog=Join-Path $RepoRoot ('artifacts/adventure-'+$adventureMode+'.log')
        $adventureErrors=Join-Path $RepoRoot ('artifacts/adventure-'+$adventureMode+'-errors.log')
        $adventureProcess=Start-Process -FilePath $adventureExecutable -ArgumentList $adventureArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $adventureLog -RedirectStandardError $adventureErrors
        $null=$adventureProcess.Handle
        $adventureDeadline=[DateTime]::UtcNow.AddSeconds(600)
        while(!$adventureProcess.WaitForExit(1000)){if([DateTime]::UtcNow -gt $adventureDeadline){$adventureProcess.Kill();throw 'Adventure review timed out'}}
        $adventureOutput=Get-Content $adventureLog -Raw
        if($adventureProcess.ExitCode -ne 0 -or $adventureOutput -notmatch 'OVERLOAD_ADVENTURE_OK' -or (Get-Content $adventureErrors -Raw) -match 'ERROR:') {throw "Adventure $adventureMode failed; inspect $adventureLog and $adventureErrors"}
        Write-Output ($adventureOutput -split "`n" | Select-Object -Last 2)
    }
}finally{Pop-Location}
