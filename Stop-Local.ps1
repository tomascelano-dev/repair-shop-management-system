$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
foreach ($name in @('api','frontend')) {
    $pidFile = Join-Path $taskRoot "logs\$name.pid"
    if (Test-Path -LiteralPath $pidFile) {
        $processId = [int](Get-Content -LiteralPath $pidFile -Raw).Trim()
        $ownedProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $processId"
        if ($ownedProcess -and $ownedProcess.CommandLine.Contains($taskRoot)) { Stop-Process -Id $processId; Write-Host "$name detenido." }
        elseif ($ownedProcess) { Write-Warning "PID reutilizado por otro programa: no se detuvo $processId." }
    }
}
Push-Location $taskRoot
try { & docker compose --env-file .env.local -f compose.local.yml stop } finally { Pop-Location }
Write-Host 'Los datos y fotos se conservan.'
