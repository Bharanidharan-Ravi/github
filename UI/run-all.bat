@echo off
REM run-all.bat - Start microfrontends in separate cmd windows and wait for remotes
SETLOCAL ENABLEDELAYEDEXPANSION
set ROOT=%~dp0
cd /d "%ROOT%"

echo Starting 'tickets' in a new window...
start "tickets" cmd /k "cd /d "%ROOT%" && pnpm --filter tickets dev"

echo Starting 'auth' in a new window...
start "auth" cmd /k "cd /d "%ROOT%" && pnpm --filter auth dev"

echo Waiting for remoteEntry URLs (tickets -> http://localhost:5003/assets/remoteEntry.js, auth -> http://localhost:5002/assets/remoteEntry.js)...
REM Use pnpm exec to call wait-on (installed in root devDependencies)
pnpm exec -- wait-on http://localhost:5003/assets/remoteEntry.js http://localhost:5002/assets/remoteEntry.js
if %ERRORLEVEL% neq 0 (
  echo wait-on returned an error. You can continue manually when remotes are ready.
  pause
)

echo Starting 'shell' in a new window...
start "shell" cmd /k "cd /d "%ROOT%" && pnpm --filter shell dev"

echo All start commands issued.
ENDLOCAL
