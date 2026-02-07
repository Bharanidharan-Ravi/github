@echo off
REM preview-all.bat - Build apps and start Vite previews in separate cmd windows, then preview shell
SETLOCAL ENABLEDELAYEDEXPANSION
set ROOT=%~dp0
cd /d "%ROOT%"

echo Building shared packages and apps...
pnpm --filter shared-store build
pnpm --filter shared-signalr build
pnpm --filter tickets build
pnpm --filter auth build

echo Starting 'tickets' preview in a new window...
start "tickets-preview" cmd /k "cd /d "%ROOT%" && pnpm --filter tickets preview"

echo Starting 'auth' preview in a new window...
start "auth-preview" cmd /k "cd /d "%ROOT%" && pnpm --filter auth preview"

echo Waiting for remoteEntry URLs (tickets -> http://localhost:6003/assets/remoteEntry.js, auth -> http://localhost:5002/assets/remoteEntry.js)...
pnpm exec -- wait-on http://localhost:6003/assets/remoteEntry.js http://localhost:5002/assets/remoteEntry.js
if %ERRORLEVEL% neq 0 (
  echo wait-on returned an error. You can continue manually when remotes are ready.
  pause
)

echo Starting 'shell' preview in a new window...
start "shell-preview" cmd /k "cd /d "%ROOT%" && pnpm --filter shell preview"

echo All preview commands issued.
ENDLOCAL
