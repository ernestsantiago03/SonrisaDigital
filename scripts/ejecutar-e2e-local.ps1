# ============================================================================
# Ejecuta las pruebas E2E de Playwright contra una base SQL Server temporal.
#
# Qué hace, en orden:
#   1. Crea una base con nombre único (no toca SonrisaDigital).
#   2. Aplica las migraciones de EF Core.
#   3. Arranca la aplicación con un administrador temporal.
#   4. Espera a que /health/ready responda.
#   5. Ejecuta Playwright.
#   6. Detiene la aplicación y elimina la base temporal.
#
# Requisitos:
#   - SQL Server local disponible (por defecto MSI\SQLEXPRESS).
#   - Node.js y las dependencias de tests/SonrisaDigital.E2E instaladas.
#   - Navegador de Playwright instalado (npm run install:browsers).
# ============================================================================

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$KeepDatabase,
    [switch]$Headed
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$e2eDir = Join-Path $repo 'tests\SonrisaDigital.E2E'
$appSettingsPath = Join-Path $repo 'src\SonrisaDigital.UI\appsettings.Development.json'

if (-not (Test-Path $appSettingsPath)) {
    throw "No se encontró $appSettingsPath. Configura primero la conexión de desarrollo."
}

if (-not (Test-Path (Join-Path $e2eDir 'node_modules'))) {
    Write-Host 'Instalando dependencias de Playwright...'
    Push-Location $e2eDir
    try { & npm install --no-audit --no-fund } finally { Pop-Location }
}

# Se toma la conexión local y solo se cambia el nombre de la base.
$configuracion = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
$cadenaBase = [string]$configuracion.ConnectionStrings.DefaultConnection
if ([string]::IsNullOrWhiteSpace($cadenaBase)) {
    throw 'appsettings.Development.json no tiene ConnectionStrings.DefaultConnection.'
}

$constructor = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $cadenaBase
$servidor = $constructor.DataSource
$sufijo = Get-Date -Format 'yyyyMMddHHmmss'
$nombreBase = "SonrisaDigital_E2E_$sufijo"
$constructor['Database'] = $nombreBase
$cadenaPrueba = $constructor.ConnectionString

# Credenciales temporales: viven solo mientras dura la prueba.
$correoAdmin = 'e2e.admin@sonrisadigital.test'
$claveAdmin = "E2E_$([Guid]::NewGuid().ToString('N').Substring(0, 12))!aA1"

# Cuenta de Dueño temporal para probar el selector de clínica y el aislamiento.
$correoDueno = 'e2e.dueno@sonrisadigital.test'
$claveDueno = "E2E_$([Guid]::NewGuid().ToString('N').Substring(0, 12))!aA1"

# Se elige un puerto libre para no chocar con una instancia abierta.
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$puerto = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
$urlBase = "http://localhost:$puerto"

$carpetaResultados = Join-Path $e2eDir 'test-results'
New-Item -ItemType Directory -Path $carpetaResultados -Force | Out-Null
$logSalida = Join-Path $carpetaResultados 'app.out.log'
$logError = Join-Path $carpetaResultados 'app.err.log'

$variables = @(
    'ASPNETCORE_ENVIRONMENT',
    'ASPNETCORE_URLS',
    'ConnectionStrings__DefaultConnection',
    'Authentication__AdminEmail',
    'Authentication__AdminPassword',
    'Authentication__LoginPermitLimit',
    'Dueno__Email',
    'Dueno__Password',
    'SONRISADIGITAL_E2E_BASE_URL',
    'SONRISADIGITAL_E2E_EMAIL',
    'SONRISADIGITAL_E2E_PASSWORD',
    'SONRISADIGITAL_E2E_DUENO_EMAIL',
    'SONRISADIGITAL_E2E_DUENO_PASSWORD'
)
$valoresOriginales = @{}
foreach ($nombre in $variables) {
    $valoresOriginales[$nombre] = [Environment]::GetEnvironmentVariable($nombre, 'Process')
}

$procesoApp = $null
$codigoSalida = 1

try {
    Write-Host 'Restaurando herramientas locales de .NET...'
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron restaurar las herramientas de .NET.' }

    Write-Host "Creando base temporal $nombreBase..."
    & dotnet ef database update `
        --project src/SonrisaDigital.Data `
        --startup-project src/SonrisaDigital.UI `
        --configuration $Configuration `
        --connection $cadenaPrueba
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron aplicar las migraciones a la base temporal.' }

    Write-Host 'Arrancando la aplicación...'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = $urlBase
    $env:ConnectionStrings__DefaultConnection = $cadenaPrueba
    $env:Authentication__AdminEmail = $correoAdmin
    $env:Authentication__AdminPassword = $claveAdmin
    # La suite inicia sesión más de 5 veces desde localhost en menos de un minuto.
    $env:Authentication__LoginPermitLimit = '50'
    $env:Dueno__Email = $correoDueno
    $env:Dueno__Password = $claveDueno

    $procesoApp = Start-Process `
        -FilePath 'dotnet' `
        -ArgumentList @(
            'run',
            '--project', 'src/SonrisaDigital.UI/SonrisaDigital.UI.csproj',
            '--configuration', $Configuration,
            '--no-build',
            '--no-launch-profile'
        ) `
        -WorkingDirectory $repo `
        -RedirectStandardOutput $logSalida `
        -RedirectStandardError $logError `
        -PassThru

    $listo = $false
    for ($intento = 0; $intento -lt 60; $intento++) {
        if ($procesoApp.HasExited) { break }

        # Se usa cmd para que un fallo de conexión de curl no se convierta en
        # una excepción de PowerShell y permita seguir esperando.
        $salud = & cmd.exe /c "curl.exe -fsS --max-time 2 $urlBase/health/ready 2>nul"
        if ($LASTEXITCODE -eq 0 -and $salud -match '"status":"Healthy"') {
            $listo = $true
            break
        }

        Start-Sleep -Seconds 2
    }

    if (-not $listo) {
        if (Test-Path $logSalida) { Get-Content $logSalida -Tail 40 }
        if (Test-Path $logError) { Get-Content $logError -Tail 40 }
        throw "La aplicación no respondió en $urlBase. Revisa los logs en $carpetaResultados."
    }

    Write-Host 'Ejecutando Playwright...'
    $env:SONRISADIGITAL_E2E_BASE_URL = $urlBase
    $env:SONRISADIGITAL_E2E_EMAIL = $correoAdmin
    $env:SONRISADIGITAL_E2E_PASSWORD = $claveAdmin
    $env:SONRISADIGITAL_E2E_DUENO_EMAIL = $correoDueno
    $env:SONRISADIGITAL_E2E_DUENO_PASSWORD = $claveDueno

    Push-Location $e2eDir
    try {
        if ($Headed) {
            & npx playwright test --headed
        }
        else {
            & npx playwright test
        }
        $codigoSalida = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($codigoSalida -ne 0) {
        throw "Playwright terminó con código $codigoSalida."
    }

    Write-Host 'Pruebas E2E completadas correctamente.'
}
finally {
    if ($procesoApp -and -not $procesoApp.HasExited) {
        & taskkill.exe /PID $procesoApp.Id /T /F | Out-Null
    }

    if ($KeepDatabase) {
        Write-Host "Base temporal conservada: $nombreBase"
    }
    else {
        Write-Host "Eliminando base temporal $nombreBase..."
        & sqlcmd -S $servidor -E -C -d master -Q "IF DB_ID(N'$nombreBase') IS NOT NULL BEGIN ALTER DATABASE [$nombreBase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$nombreBase]; END" | Out-Null
    }

    foreach ($nombre in $variables) {
        [Environment]::SetEnvironmentVariable($nombre, $valoresOriginales[$nombre], 'Process')
    }
}

exit $codigoSalida
