[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    # Activar cuando la URL ya sea la definitiva por HTTPS. En un smoke test
    # local por HTTP se puede omitir para revisar salud, login y cabeceras.
    [switch]$RequireHttps,

    [int]$TimeoutSeconds = 15
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$BaseUrl = $BaseUrl.TrimEnd('/')
if ($BaseUrl -notmatch '^https?://') {
    throw "BaseUrl debe comenzar con http:// o https://."
}

if ($RequireHttps -and $BaseUrl -notmatch '^https://') {
    throw "Con -RequireHttps, BaseUrl debe usar https://."
}

$errores = [System.Collections.Generic.List[string]]::new()
$advertencias = [System.Collections.Generic.List[string]]::new()

# Realiza una petición y devuelve la respuesta. Si la conexión falla, el error
# se agrega a la lista para que el script termine mostrando todo junto.
function Invoke-Peticion {
    param([string]$Ruta)

    $url = "$BaseUrl$Ruta"
    try {
        return Invoke-WebRequest `
            -Uri $url `
            -UseBasicParsing `
            -TimeoutSec $TimeoutSeconds `
            -MaximumRedirection 5 `
            -ErrorAction Stop
    }
    catch {
        $errores.Add("No se pudo consultar $url. $($_.Exception.Message)")
        return $null
    }
}

# Lee una cabecera HTTP sin depender de mayúsculas/minúsculas del servidor.
function Get-ValorCabecera {
    param(
        $Respuesta,
        [string]$Nombre
    )

    if ($null -eq $Respuesta) {
        return $null
    }

    $valor = $Respuesta.Headers[$Nombre]
    if ($null -eq $valor) {
        return $null
    }

    return ($valor -join ", ")
}

# 1. Salud básica: confirma que el proceso está vivo.
$salud = Invoke-Peticion -Ruta "/health"
if ($null -ne $salud) {
    if ([int]$salud.StatusCode -ne 200) {
        $errores.Add("/health respondió $([int]$salud.StatusCode) en lugar de 200.")
    }
    elseif ($salud.Content -notmatch '"status"\s*:\s*"Healthy"') {
        $errores.Add("/health no devolvió el estado Healthy esperado.")
    }
}

# 2. Salud completa: además de vivir, la aplicación debe alcanzar SQL Server.
$saludLista = Invoke-Peticion -Ruta "/health/ready"
if ($null -ne $saludLista) {
    if ([int]$saludLista.StatusCode -ne 200) {
        $errores.Add("/health/ready respondió $([int]$saludLista.StatusCode) en lugar de 200.")
    }
    elseif ($saludLista.Content -notmatch '"database"\s*:\s*"reachable"') {
        $errores.Add("/health/ready no confirmó que la base esté accesible.")
    }
}

# 3. Login: la pantalla debe cargar y no debe contener restos de la demo.
$login = Invoke-Peticion -Ruta "/login"
if ($null -ne $login) {
    if ([int]$login.StatusCode -ne 200) {
        $errores.Add("/login respondió $([int]$login.StatusCode) en lugar de 200.")
    }

    $textosDemo = @(
        "SonrisaDigitalAdmin123",
        "login-demo",
        "admin@sonrisadigital.com",
        "Credenciales de demostración"
    )

    foreach ($texto in $textosDemo) {
        if ($login.Content -match [regex]::Escape($texto)) {
            $errores.Add("La pantalla de login todavía contiene el texto de demo: $texto")
        }
    }
}

# 4. Cabeceras de seguridad: se revisan sobre /health porque también pasan por
# el middleware global y no dependen de que el login tenga sesión.
$cabecerasObligatorias = @(
    "X-Content-Type-Options",
    "X-Frame-Options",
    "Referrer-Policy",
    "Permissions-Policy",
    "Content-Security-Policy"
)

foreach ($cabecera in $cabecerasObligatorias) {
    $valor = Get-ValorCabecera -Respuesta $salud -Nombre $cabecera
    if ([string]::IsNullOrWhiteSpace($valor)) {
        $errores.Add("Falta la cabecera de seguridad: $cabecera")
    }
}

# 5. HSTS solo se exige cuando la URL es la definitiva por HTTPS. En HTTP local
# no corresponde porque el navegador no debe recordar un host de prueba.
if ($RequireHttps) {
    $hsts = Get-ValorCabecera -Respuesta $salud -Nombre "Strict-Transport-Security"
    if ([string]::IsNullOrWhiteSpace($hsts) -or $hsts -notmatch "max-age=") {
        $errores.Add("Falta Strict-Transport-Security con max-age en la respuesta HTTPS.")
    }
}
else {
    $advertencias.Add("No se validó HSTS porque -RequireHttps no está activo.")
}

if ($advertencias.Count -gt 0) {
    foreach ($advertencia in $advertencias) {
        Write-Warning $advertencia
    }
}

if ($errores.Count -gt 0) {
    Write-Host "Verificación de producción con errores:" -ForegroundColor Red
    foreach ($mensajeError in $errores) {
        Write-Host " - $mensajeError" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Verificación de producción correcta para $BaseUrl." -ForegroundColor Green
Write-Host "Salud, base de datos, login y cabeceras de seguridad responden como se espera."
