param([switch]$Engine)
. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    Invoke-Checked $DotNet @('build', 'Overload.sln', '-c', 'Release', '-p:RestoreLockedMode=true')
    Invoke-Checked $DotNet @('test', 'Overload.sln', '-c', 'Release', '--no-build')
    Invoke-Checked $DotNet @('run', '--project', 'src/Overload.Tools', '-c', 'Release', '--no-build', '--', 'validate-content')
    Invoke-Checked $DotNet @('run', '--project', 'src/Overload.Tools', '-c', 'Release', '--no-build', '--', 'frame-report')
    Invoke-Checked $DotNet @('run', '--project', 'src/Overload.Tools', '-c', 'Release', '--no-build', '--', 'world-report')
    if ($Engine) {
        Invoke-Checked $Godot @('--headless', '--path', $GamePath, '--editor', '--import')
        Invoke-Checked $Godot @('--headless', '--path', $GamePath, '--fixed-fps', '60', '--', '--smoke-test')
        Invoke-Checked $Godot @('--headless', '--path', $GamePath, '--fixed-fps', '60', '--', '--endless-smoke')
        Invoke-Checked $Godot @('--headless', '--path', $GamePath, '--fixed-fps', '60', '--', '--production-smoke')
        Invoke-Checked $Godot @('--headless', '--path', $GamePath, '--fixed-fps', '60', '--', '--expansion-smoke')
        Invoke-Checked $Godot @('--headless', '--path', $GamePath, '--fixed-fps', '60', '--', '--experience-smoke')
        Invoke-Checked $Godot @('--headless', '--path', $GamePath, '--fixed-fps', '60', '--', '--world-smoke')
        Invoke-Checked $Godot @('--headless', '--path', $GamePath, '--fixed-fps', '60', '--', '--identity-smoke')
    }
} finally { Pop-Location }
