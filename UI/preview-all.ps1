<#
preview-all.ps1 - Build apps and start Vite previews in separate PowerShell windows, then preview shell
Usage: Run from repo root: .\preview-all.ps1
#>

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $root

Write-Host "Building shared packages and apps..."
pnpm --filter shared-store build
pnpm --filter shared-signalr build
pnpm --filter tickets build
pnpm --filter auth build

Write-Host "Starting 'tickets' preview in a new PowerShell window..."
Start-Process -FilePath "powershell" -ArgumentList "-NoExit","-Command","Set-Location '$root'; pnpm --filter tickets preview" -WindowStyle Normal

Write-Host "Starting 'auth' preview in a new PowerShell window..."
Start-Process -FilePath "powershell" -ArgumentList "-NoExit","-Command","Set-Location '$root'; pnpm --filter auth preview" -WindowStyle Normal

Write-Host "Waiting for remoteEntry URLs (tickets -> http://localhost:6003/assets/remoteEntry.js, auth -> http://localhost:5002/assets/remoteEntry.js)..."
try {
    & pnpm exec -- wait-on http://localhost:6003/assets/remoteEntry.js http://localhost:5002/assets/remoteEntry.js
} catch {
    Write-Warning "wait-on failed or was interrupted. Press Enter to continue when remotes are ready or Ctrl+C to abort."
    Read-Host
}

Write-Host "Starting 'shell' preview in a new PowerShell window..."
Start-Process -FilePath "powershell" -ArgumentList "-NoExit","-Command","Set-Location '$root'; pnpm --filter shell preview" -WindowStyle Normal

Write-Host "All preview commands issued."
