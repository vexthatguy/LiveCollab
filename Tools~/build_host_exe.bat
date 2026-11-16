@echo off
REM Build a standalone LiveCollabHost.exe from livecollab_host.py
REM Requires Python 3.6+ and PyInstaller (one-time: pip install pyinstaller)
REM Usage: double-click or run from a terminal.

setlocal
cd /d "%~dp0"

where python >nul 2>nul
if errorlevel 1 (
  echo [LiveCollab] Python not found on PATH. Install Python 3 and re-run.
  exit /b 1
)

python -m PyInstaller --version >nul 2>nul
if errorlevel 1 (
  echo [LiveCollab] Installing PyInstaller...
  python -m pip install pyinstaller || goto :error
)

python -m PyInstaller --onefile --noconsole --name LiveCollabHost livecollab_host.py || goto :error

echo.
echo [LiveCollab] Done. Executable: dist\LiveCollabHost.exe
echo Copy dist\LiveCollabHost.exe into your project's LiveCollab_Host\ folder
echo (or set the path in the LiveCollab panel's advanced settings).
exit /b 0

:error
echo [LiveCollab] Build failed.
exit /b 1
