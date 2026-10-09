param([switch]$Rendered,[switch]$Exported)
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    if(!$Exported){
        Invoke-Checked $DotNet @('build','src/Overload.Game/Overload.Game.csproj','--no-restore','-p:UseSharedCompilation=false')
        Invoke-Checked $Godot @('--headless','--path',$GamePath,'--editor','--import')
    }
    $graphicsExecutable=if($Exported){Join-Path $RepoRoot 'artifacts/windows/Overload.exe'}else{$Godot.Replace('_console.exe','.exe')}
    $graphicsModes=@('smoke');if($Rendered){$graphicsModes+='review'}
    foreach($graphicsMode in $graphicsModes){
        $graphicsArgs=if($Exported){@()}else{@('--path',('"'+$GamePath+'"'))}
        if($graphicsMode-eq 'smoke'){$graphicsArgs+=@('--headless','--fixed-fps','60')}
        else{$graphicsArgs+=@('--rendering-method','gl_compatibility','--resolution','1280x720','--fixed-fps','60')}
        $graphicsArgs+=@('--',('--graphics-'+$graphicsMode))
        $graphicsLog=Join-Path $RepoRoot ('artifacts/graphics-'+$graphicsMode+'.log')
        $graphicsErrors=Join-Path $RepoRoot ('artifacts/graphics-'+$graphicsMode+'-errors.log')
        $graphicsProcess=Start-Process -FilePath $graphicsExecutable -ArgumentList $graphicsArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $graphicsLog -RedirectStandardError $graphicsErrors
        $null=$graphicsProcess.Handle
        $graphicsDeadline=[DateTime]::UtcNow.AddSeconds(90)
        while(!$graphicsProcess.WaitForExit(1000)){if([DateTime]::UtcNow-gt $graphicsDeadline){$graphicsProcess.Kill();throw 'Graphics review timed out'}}
        $graphicsOutput=Get-Content $graphicsLog -Raw
        if($graphicsProcess.ExitCode-ne 0-or $graphicsOutput-notmatch 'OVERLOAD_GRAPHICS_OK'-or (Get-Content $graphicsErrors -Raw)-match 'ERROR:'){throw "Graphics $graphicsMode failed; inspect $graphicsLog and $graphicsErrors"}
        Write-Output ($graphicsOutput-split "`n"|Select-Object -Last 2)
    }
}finally{Pop-Location}
