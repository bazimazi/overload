param([switch]$Rendered, [switch]$Exported)
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    if (!$Exported) {
        Invoke-Checked $DotNet @('build','src/Overload.Game/Overload.Game.csproj','-p:RestoreLockedMode=true')
        $importOutput=Join-Path $RepoRoot 'artifacts/rpg-import.log'
        $importErrors=Join-Path $RepoRoot 'artifacts/rpg-import-errors.log'
        $importProcess=Start-Process -FilePath $Godot.Replace('_console.exe','.exe') -ArgumentList @('--headless','--path',('"'+$GamePath+'"'),'--editor','--import') -WindowStyle Hidden -PassThru -RedirectStandardOutput $importOutput -RedirectStandardError $importErrors
        $null=$importProcess.Handle
        if(!$importProcess.WaitForExit(60000)){ $importProcess.Kill(); throw 'RPG asset import timed out' }
        if($importProcess.ExitCode -ne 0 -or (Get-Content $importErrors -Raw) -match 'ERROR:'){throw 'RPG asset import failed'}
    }
    $executable=if($Exported){Join-Path $RepoRoot 'artifacts/windows/Overload.exe'}else{$Godot.Replace('_console.exe','.exe')}
    $modes=@('smoke'); if($Rendered){$modes+='review'}
    foreach($mode in $modes){
        $arguments=if($Exported){@()}else{@('--path',('"'+$GamePath+'"'))}
        if($mode -eq 'smoke'){$arguments+=@('--headless','--fixed-fps','60')}
        else{$arguments+=@('--rendering-method','gl_compatibility','--resolution','1280x720','--fixed-fps','60')}
        $arguments+=@('--',('--rpg-'+$mode))
        $stdout=Join-Path $RepoRoot ('artifacts/rpg-'+$mode+'.log');$stderr=Join-Path $RepoRoot ('artifacts/rpg-'+$mode+'-errors.log')
        $process=Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        $null=$process.Handle
        if(!$process.WaitForExit(60000)){$process.Kill();throw "RPG $mode timed out"}
        $output=Get-Content $stdout -Raw;$errors=Get-Content $stderr -Raw
        if($process.ExitCode -ne 0 -or $output -notmatch 'OVERLOAD_RPG_OK' -or $errors -match 'ERROR:'){throw "RPG $mode failed; inspect $stdout and $stderr"}
        Write-Output ($output -split "`n" | Select-Object -Last 2)
    }
} finally {Pop-Location}
