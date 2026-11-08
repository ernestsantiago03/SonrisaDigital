<#
.SYNOPSIS
    Elimina artefactos de GitHub Actions para liberar la cuota de almacenamiento.

.DESCRIPTION
    La cuenta Free de GitHub incluye 500 MB de artefactos. Cuando se agota, los
    jobs fallan con "Artifact storage quota has been hit" y la cuota tarda entre
    6 y 12 horas en recalcularse después de borrar.

    Este script lista los artefactos del repositorio y los elimina con la API
    REST. Requiere un token con permiso de escritura sobre Actions:

      - Token clásico: scope "repo" (repositorio privado).
      - Token fine-grained: permiso "Actions: Read and write" en el repositorio.

.PARAMETER Repositorio
    Repositorio en formato propietario/nombre.

.PARAMETER Token
    Token de GitHub. Por defecto toma $env:GITHUB_TOKEN.

.PARAMETER AntiguedadDias
    0 elimina todo. Con 7, solo elimina artefactos creados hace 7 días o más.

.PARAMETER SoloListar
    Muestra qué se eliminaría sin borrar nada.

.EXAMPLE
    $env:GITHUB_TOKEN = "ghp_..."
    ./scripts/limpiar-artefactos.ps1 -SoloListar

.EXAMPLE
    $env:GITHUB_TOKEN = "ghp_..."
    ./scripts/limpiar-artefactos.ps1
#>
[CmdletBinding()]
param(
    [string]$Repositorio = "ernestsantiago03/SonrisaDigital",
    [string]$Token = $env:GITHUB_TOKEN,
    [ValidateRange(0, 3650)]
    [int]$AntiguedadDias = 0,
    [switch]$SoloListar
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Token)) {
    throw @"
Falta el token de GitHub.

Crea un token con permiso de escritura sobre Actions y defínelo así:

    `$env:GITHUB_TOKEN = "ghp_..."

También puedes pasarlo con -Token.
"@
}

$cabeceras = @{
    Authorization          = "Bearer $Token"
    Accept                 = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
    "User-Agent"           = "SonrisaDigital-limpieza-artefactos"
}

function Formatear-Tamano([long]$Bytes) {
    if ($Bytes -ge 1GB) { return "{0:N2} GB" -f ($Bytes / 1GB) }
    if ($Bytes -ge 1MB) { return "{0:N2} MB" -f ($Bytes / 1MB) }
    if ($Bytes -ge 1KB) { return "{0:N2} KB" -f ($Bytes / 1KB) }
    return "$Bytes B"
}

Write-Host "Repositorio: $Repositorio"

$artefactos = @()
$pagina = 1

do {
    $uri = "https://api.github.com/repos/$Repositorio/actions/artifacts?per_page=100&page=$pagina"
    $respuesta = Invoke-RestMethod -Uri $uri -Headers $cabeceras -Method Get
    $lote = @($respuesta.artifacts)
    $artefactos += $lote
    $pagina++
} while ($lote.Count -eq 100)

if ($artefactos.Count -eq 0) {
    Write-Host "No hay artefactos en el repositorio."
    return
}

$totalBytes = ($artefactos | Measure-Object -Property size_in_bytes -Sum).Sum
Write-Host "Artefactos: $($artefactos.Count) ($(Formatear-Tamano $totalBytes))"
Write-Host ""

$artefactos |
    Group-Object -Property name |
    Sort-Object { ($_.Group | Measure-Object -Property size_in_bytes -Sum).Sum } -Descending |
    ForEach-Object {
        $bytes = ($_.Group | Measure-Object -Property size_in_bytes -Sum).Sum
        Write-Host ("  {0,-32} {1,4} ejecuciones  {2}" -f $_.Name, $_.Count, (Formatear-Tamano $bytes))
    }

Write-Host ""

$limite = (Get-Date).ToUniversalTime().AddDays(-$AntiguedadDias)
$objetivo = @($artefactos | Where-Object {
    $creado = [datetime]::Parse($_.created_at, $null, [System.Globalization.DateTimeStyles]::AdjustToUniversal)
    $creado -le $limite
})

if ($objetivo.Count -eq 0) {
    Write-Host "Ningún artefacto cumple el criterio de antigüedad (>= $AntiguedadDias días)."
    return
}

$objetivoBytes = ($objetivo | Measure-Object -Property size_in_bytes -Sum).Sum
Write-Host "A eliminar: $($objetivo.Count) artefactos ($(Formatear-Tamano $objetivoBytes))"

if ($SoloListar) {
    $objetivo |
        Sort-Object created_at |
        ForEach-Object {
            Write-Host ("  [simulación] {0} ({1}, creado {2:yyyy-MM-dd})" -f $_.name, (Formatear-Tamano $_.size_in_bytes), ([datetime]$_.created_at))
        }
    Write-Host "No se eliminó nada. Ejecuta sin -SoloListar para borrar."
    return
}

$eliminados = 0
$fallidos = 0

foreach ($artefacto in $objetivo) {
    try {
        Invoke-RestMethod `
            -Uri "https://api.github.com/repos/$Repositorio/actions/artifacts/$($artefacto.id)" `
            -Headers $cabeceras `
            -Method Delete | Out-Null
        $eliminados++
        Write-Host ("  Eliminado: {0} ({1})" -f $artefacto.name, (Formatear-Tamano $artefacto.size_in_bytes))
    }
    catch {
        $fallidos++
        Write-Warning ("No se pudo eliminar {0} (id {1}): {2}" -f $artefacto.name, $artefacto.id, $_.Exception.Message)
    }

    Start-Sleep -Milliseconds 150
}

Write-Host ""
Write-Host "Eliminados: $eliminados. Fallidos: $fallidos."
Write-Host "GitHub recalcula la cuota cada 6-12 horas. Si vuelves a ejecutar el"
Write-Host "pipeline antes de ese plazo, puede seguir apareciendo el error de cuota."
