<#
dev-all.ps1 - Start microfrontends in separate PowerShell windows and wait for remotes
Usage: Right-click -> Run with PowerShell, or run from PowerShell: .\dev-all.ps1
#>

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $root

Write-Host "Starting 'tickets' in a new PowerShell window..."
Start-Process -FilePath "powershell" -ArgumentList "-NoExit","-Command","Set-Location '$root'; pnpm --filter tickets dev" -WindowStyle Normal

Write-Host "Starting 'auth' in a new PowerShell window..."
Start-Process -FilePath "powershell" -ArgumentList "-NoExit","-Command","Set-Location '$root'; pnpm --filter auth dev" -WindowStyle Normal

Write-Host "Waiting for remoteEntry URLs (tickets -> http://localhost:5003/assets/remoteEntry.js, auth -> http://localhost:5002/assets/remoteEntry.js)..."
try {
    & pnpm exec -- wait-on http://localhost:5003/assets/remoteEntry.js http://localhost:5002/assets/remoteEntry.js
} catch {
    Write-Warning "wait-on failed or was interrupted. Press Enter to continue when remotes are ready or Ctrl+C to abort."
    Read-Host
}

Write-Host "Starting 'shell' in a new PowerShell window..."
Start-Process -FilePath "powershell" -ArgumentList "-NoExit","-Command","Set-Location '$root'; pnpm --filter shell dev" -WindowStyle Normal

Write-Host "All start commands issued."
