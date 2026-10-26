[CmdletBinding()]
param(
    [string]$RutaResultados = "tests/SonrisaDigital.Tests/TestResults",
    [ValidateRange(0.0, 1.0)]
    [double]$MinimoLinea = 0.60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$archivo = Get-ChildItem -LiteralPath $RutaResultados -Recurse -Filter "coverage.cobertura.xml" -File |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $archivo) {
    throw "No se encontró coverage.cobertura.xml en '$RutaResultados'."
}

[xml]$cobertura = Get-Content -LiteralPath $archivo.FullName
$linea = [double]::Parse(
    $cobertura.coverage.'line-rate',
    [System.Globalization.CultureInfo]::InvariantCulture)
$rama = [double]::Parse(
    $cobertura.coverage.'branch-rate',
    [System.Globalization.CultureInfo]::InvariantCulture)

$lineaPorcentaje = [math]::Round($linea * 100, 2)
$ramaPorcentaje = [math]::Round($rama * 100, 2)
$minimoPorcentaje = [math]::Round($MinimoLinea * 100, 2)

Write-Host "Cobertura de líneas: $lineaPorcentaje% (mínimo $minimoPorcentaje%)"
Write-Host "Cobertura de ramas:  $ramaPorcentaje%"

if ($linea -lt $MinimoLinea) {
    throw "La cobertura de líneas ($lineaPorcentaje%) está por debajo del mínimo ($minimoPorcentaje%)."
}
