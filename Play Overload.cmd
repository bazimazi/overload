@echo off
pushd "%~dp0"
if not exist "artifacts\windows\Overload.exe" (
  echo Run build/export.ps1 to create the playable game.
  pause
  popd
  exit /b 1
)
start "Overload" "%~dp0artifacts\windows\Overload.exe"
popd
