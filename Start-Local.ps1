param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$apiDirectory = Join-Path $taskRoot 'RepairShop\src\RepairShop.Api'
$frontendDirectory = Join-Path $taskRoot 'frontend'
$logDirectory = Join-Path $taskRoot 'logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
Push-Location $taskRoot
try {
    & docker compose --env-file .env.local -f compose.local.yml up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo iniciar PostgreSQL. Revisá Docker Desktop.' }
    $apiListener = Get-NetTCPConnection -LocalPort 5282 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    $uiListener = Get-NetTCPConnection -LocalPort 5182 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    foreach ($listener in @($apiListener, $uiListener)) {
        if ($null -ne $listener) {
            $owner = Get-CimInstance Win32_Process -Filter "ProcessId = $($listener.OwningProcess)"
            if (-not $owner.CommandLine.Contains($taskRoot)) { throw "El puerto $($listener.LocalPort) lo usa otro programa. No se lo modificó." }
        }
    }
    if (-not $SkipBuild -and -not $apiListener) {
        & dotnet build RepairShop/RepairShop.sln --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de la API.' }
    }
    if (-not $SkipBuild -and -not $uiListener) {
        Push-Location $frontendDirectory
        try {
            if (-not (Test-Path node_modules)) { & npm.cmd ci; if ($LASTEXITCODE -ne 0) { throw 'Falló npm ci.' } }
            & npm.cmd run build
            if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de la interfaz.' }
        } finally { Pop-Location }
    }
    if (-not $apiListener) {
        $previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
        $previousConnection = $env:ConnectionStrings__RepairShopDb
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $localSettings = Get-Content -LiteralPath (Join-Path $apiDirectory 'appsettings.Development.Local.json') -Raw | ConvertFrom-Json
        $env:ConnectionStrings__RepairShopDb = $localSettings.ConnectionStrings.RepairShopDb
        try {
            $apiArgs = @(('"{0}"' -f (Join-Path $apiDirectory 'bin\Debug\net8.0\RepairShop.Api.dll')), '--urls', 'http://127.0.0.1:5282')
            $apiProcess = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList $apiArgs -WorkingDirectory $apiDirectory -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logDirectory 'api.log') -RedirectStandardError (Join-Path $logDirectory 'api.error.log') -PassThru
            Set-Content -LiteralPath (Join-Path $logDirectory 'api.pid') -Value $apiProcess.Id
        } finally { $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment; $env:ConnectionStrings__RepairShopDb = $previousConnection }
    }
    if (-not $uiListener) {
        $uiArgs = @(('"{0}"' -f (Join-Path $frontendDirectory 'node_modules\vite\bin\vite.js')), '--host', '127.0.0.1', '--port', '5182', '--strictPort')
        $uiProcess = Start-Process -FilePath (Get-Command node).Source -ArgumentList $uiArgs -WorkingDirectory $frontendDirectory -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logDirectory 'frontend.log') -RedirectStandardError (Join-Path $logDirectory 'frontend.error.log') -PassThru
        Set-Content -LiteralPath (Join-Path $logDirectory 'frontend.pid') -Value $uiProcess.Id
    }
    foreach ($url in @('http://127.0.0.1:5282/readyz', 'http://127.0.0.1:5182')) {
        $ready = $false
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            try { $response = Invoke-WebRequest -Uri $url -TimeoutSec 2; if ($response.StatusCode -eq 200) { $ready = $true; break } } catch { Start-Sleep -Seconds 1 }
        }
        if (-not $ready) { throw "No respondió $url. Revisá los archivos en logs." }
    }
    Write-Host 'RepairShop v2 disponible: http://127.0.0.1:5182'
    Write-Host 'Los procesos quedan corriendo en segundo plano. Para detenerlos: .\Stop-Local.ps1'
} finally { Pop-Location }
