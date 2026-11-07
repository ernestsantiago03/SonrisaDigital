# ============================================================================
# Ejecuta las pruebas E2E de Playwright contra dos bases SQL Server temporales.
#
# Qué hace, en orden:
#   1. Crea dos bases con nombre único (no toca SonrisaDigital):
#        - Base principal: arranca sin segundo factor para el resto de las pruebas.
#        - Base MFA: arranca con Seguridad:MfaDuenoObligatorio=true.
#   2. Aplica las migraciones de EF Core a ambas.
#   3. Arranca las dos aplicaciones y espera a que /health/ready responda.
#   4. Ejecuta Playwright (proyectos "chromium" y "mfa").
#   5. Detiene las aplicaciones y elimina las bases temporales.
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
$nombreBaseMfa = "SonrisaDigital_E2E_${sufijo}_Mfa"

function Nueva-Cadena([string]$nombre) {
    $copia = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $cadenaBase
    $copia['Database'] = $nombre
    return $copia.ConnectionString
}

$cadenaPrueba = Nueva-Cadena $nombreBase
$cadenaPruebaMfa = Nueva-Cadena $nombreBaseMfa

# Credenciales temporales: viven solo mientras dura la prueba.
$correoAdmin = 'e2e.admin@sonrisadigital.test'
$claveAdmin = "E2E_$([Guid]::NewGuid().ToString('N').Substring(0, 12))!aA1"

# Cuenta de Dueño temporal para probar el selector de clínica y el aislamiento.
$correoDueno = 'e2e.dueno@sonrisadigital.test'
$claveDueno = "E2E_$([Guid]::NewGuid().ToString('N').Substring(0, 12))!aA1"

# Cuenta de Dueño de la instancia con segundo factor obligatorio.
$correoDuenoMfa = 'e2e.dueno.mfa@sonrisadigital.test'
$claveDuenoMfa = "E2E_$([Guid]::NewGuid().ToString('N').Substring(0, 12))!aA1"

function Obtener-PuertoLibre() {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $puerto = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    $listener.Stop()
    return $puerto
}

# Se eligen puertos libres para no chocar con una instancia abierta.
$puerto = Obtener-PuertoLibre
$puertoMfa = Obtener-PuertoLibre
while ($puertoMfa -eq $puerto) { $puertoMfa = Obtener-PuertoLibre }
$urlBase = "http://localhost:$puerto"
$urlBaseMfa = "http://localhost:$puertoMfa"

$carpetaResultados = Join-Path $e2eDir 'test-results'
New-Item -ItemType Directory -Path $carpetaResultados -Force | Out-Null
$logSalida = Join-Path $carpetaResultados 'app.out.log'
$logError = Join-Path $carpetaResultados 'app.err.log'
$logSalidaMfa = Join-Path $carpetaResultados 'app-mfa.out.log'
$logErrorMfa = Join-Path $carpetaResultados 'app-mfa.err.log'

$variables = @(
    'ASPNETCORE_ENVIRONMENT',
    'ASPNETCORE_URLS',
    'ConnectionStrings__DefaultConnection',
    'Authentication__AdminEmail',
    'Authentication__AdminPassword',
    'Authentication__LoginPermitLimit',
    'Authentication__LoginCuentaPermitLimit',
    'Dueno__Email',
    'Dueno__Password',
    'SONRISADIGITAL_E2E_BASE_URL',
    'SONRISADIGITAL_E2E_EMAIL',
    'SONRISADIGITAL_E2E_PASSWORD',
    'SONRISADIGITAL_E2E_DUENO_EMAIL',
    'SONRISADIGITAL_E2E_DUENO_PASSWORD',
    'SONRISADIGITAL_E2E_MFA_BASE_URL',
    'SONRISADIGITAL_E2E_MFA_DUENO_EMAIL',
    'SONRISADIGITAL_E2E_MFA_DUENO_PASSWORD',
    'Seguridad__MfaDuenoObligatorio'
)
$valoresOriginales = @{}
foreach ($nombre in $variables) {
    $valoresOriginales[$nombre] = [Environment]::GetEnvironmentVariable($nombre, 'Process')
}

function Aplicar-Migraciones([string]$cadena, [string]$nombre) {
    Write-Host "Creando base temporal $nombre..."
    & dotnet ef database update `
        --project src/SonrisaDigital.Data `
        --startup-project src/SonrisaDigital.UI `
        --configuration $Configuration `
        --connection $cadena
    if ($LASTEXITCODE -ne 0) { throw "No se pudieron aplicar las migraciones a $nombre." }
}

function Iniciar-Aplicacion([string]$url, [string]$rutaSalida, [string]$rutaError) {
    return Start-Process `
        -FilePath 'dotnet' `
        -ArgumentList @(
            'run',
            '--project', 'src/SonrisaDigital.UI/SonrisaDigital.UI.csproj',
            '--configuration', $Configuration,
            '--no-build',
            '--no-launch-profile'
        ) `
        -WorkingDirectory $repo `
        -RedirectStandardOutput $rutaSalida `
        -RedirectStandardError $rutaError `
        -PassThru
}

function Esperar-Aplicacion($proceso, [string]$url, [string]$rutaSalida, [string]$rutaError) {
    for ($intento = 0; $intento -lt 60; $intento++) {
        if ($proceso.HasExited) { break }

        # Se usa cmd para que un fallo de conexión de curl no se convierta en
        # una excepción de PowerShell y permita seguir esperando.
        $salud = & cmd.exe /c "curl.exe -fsS --max-time 2 $url/health/ready 2>nul"
        if ($LASTEXITCODE -eq 0 -and $salud -match '"status":"Healthy"') {
            return
        }

        Start-Sleep -Seconds 2
    }

    if (Test-Path $rutaSalida) { Get-Content $rutaSalida -Tail 40 }
    if (Test-Path $rutaError) { Get-Content $rutaError -Tail 40 }
    throw "La aplicación no respondió en $url. Revisa los logs en $carpetaResultados."
}

$procesoApp = $null
$procesoAppMfa = $null
$codigoSalida = 1

try {
    Write-Host 'Restaurando herramientas locales de .NET...'
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron restaurar las herramientas de .NET.' }

    Aplicar-Migraciones $cadenaPrueba $nombreBase
    Aplicar-Migraciones $cadenaPruebaMfa $nombreBaseMfa

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Authentication__AdminEmail = $correoAdmin
    $env:Authentication__AdminPassword = $claveAdmin
    # La suite inicia sesión más de 5 veces desde localhost en menos de un minuto.
    $env:Authentication__LoginPermitLimit = '50'
    # Las pruebas inician sesión varias veces con la misma cuenta en poco tiempo.
    $env:Authentication__LoginCuentaPermitLimit = '100'

    Write-Host 'Arrancando la aplicación (sin segundo factor)...'
    $env:ASPNETCORE_URLS = $urlBase
    $env:ConnectionStrings__DefaultConnection = $cadenaPrueba
    $env:Dueno__Email = $correoDueno
    $env:Dueno__Password = $claveDueno
    # Solo para las pruebas E2E: el Dueño de prueba entra sin segundo factor. En producción la
    # validación de configuración rechaza este valor.
    $env:Seguridad__MfaDuenoObligatorio = 'false'
    $procesoApp = Iniciar-Aplicacion $urlBase $logSalida $logError
    Esperar-Aplicacion $procesoApp $urlBase $logSalida $logError

    Write-Host 'Arrancando la aplicación con segundo factor obligatorio...'
    $env:ASPNETCORE_URLS = $urlBaseMfa
    $env:ConnectionStrings__DefaultConnection = $cadenaPruebaMfa
    $env:Dueno__Email = $correoDuenoMfa
    $env:Dueno__Password = $claveDuenoMfa
    $env:Seguridad__MfaDuenoObligatorio = 'true'
    $procesoAppMfa = Iniciar-Aplicacion $urlBaseMfa $logSalidaMfa $logErrorMfa
    Esperar-Aplicacion $procesoAppMfa $urlBaseMfa $logSalidaMfa $logErrorMfa

    Write-Host 'Ejecutando Playwright...'
    $env:SONRISADIGITAL_E2E_BASE_URL = $urlBase
    $env:SONRISADIGITAL_E2E_EMAIL = $correoAdmin
    $env:SONRISADIGITAL_E2E_PASSWORD = $claveAdmin
    $env:SONRISADIGITAL_E2E_DUENO_EMAIL = $correoDueno
    $env:SONRISADIGITAL_E2E_DUENO_PASSWORD = $claveDueno
    $env:SONRISADIGITAL_E2E_MFA_BASE_URL = $urlBaseMfa
    $env:SONRISADIGITAL_E2E_MFA_DUENO_EMAIL = $correoDuenoMfa
    $env:SONRISADIGITAL_E2E_MFA_DUENO_PASSWORD = $claveDuenoMfa

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
    foreach ($proceso in @($procesoApp, $procesoAppMfa)) {
        if ($proceso -and -not $proceso.HasExited) {
            & taskkill.exe /PID $proceso.Id /T /F | Out-Null
        }
    }

    foreach ($nombre in @($nombreBase, $nombreBaseMfa)) {
        if ($KeepDatabase) {
            Write-Host "Base temporal conservada: $nombre"
        }
        else {
            Write-Host "Eliminando base temporal $nombre..."
            & sqlcmd -S $servidor -E -C -d master -Q "IF DB_ID(N'$nombre') IS NOT NULL BEGIN ALTER DATABASE [$nombre] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$nombre]; END" | Out-Null
        }
    }

    foreach ($nombre in $variables) {
        [Environment]::SetEnvironmentVariable($nombre, $valoresOriginales[$nombre], 'Process')
    }
}

exit $codigoSalida
