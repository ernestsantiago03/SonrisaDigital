[CmdletBinding()]
param(
    [string]$OutputPath = "",
    [string]$Configuration = "Release",
    [string]$Project = "src/SonrisaDigital.Data/SonrisaDigital.Data.csproj",
    [string]$StartupProject = "src/SonrisaDigital.UI/SonrisaDigital.UI.csproj"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# La raíz del repositorio es la carpeta padre de scripts/. Se usa para que el
# script funcione igual si se ejecuta desde la raíz o desde otra ubicación.
$raiz = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $raiz "artefactos/migraciones-sonrisadigital.sql"
}

# Se resuelve a ruta absoluta antes de crear la carpeta para que dotnet ef no
# dependa del directorio actual.
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$directorioSalida = Split-Path -Parent $OutputPath

if (-not (Test-Path -LiteralPath $directorioSalida)) {
    New-Item -ItemType Directory -Path $directorioSalida -Force | Out-Null
}

Push-Location $raiz

try {
    # La versión de dotnet-ef está fijada en .config/dotnet-tools.json. Restaurar
    # evita que un equipo con otra versión genere un script distinto.
    Write-Host "Restaurando herramientas locales de .NET..."
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "No se pudieron restaurar las herramientas locales de .NET."
    }

    # --idempotent permite ejecutar el script varias veces sin repetir cambios.
    # Es la opción recomendada cuando el DBA aplica el esquema en producción.
    Write-Host "Generando script idempotente de migraciones..."
    & dotnet ef migrations script `
        --idempotent `
        --project $Project `
        --startup-project $StartupProject `
        --configuration $Configuration `
        --output $OutputPath

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef terminó con código $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $OutputPath)) {
        throw "dotnet ef no generó el archivo esperado: $OutputPath"
    }

    $archivo = Get-Item -LiteralPath $OutputPath
    $tamanoKb = [math]::Round($archivo.Length / 1KB, 2)

    # La migración del índice de citas es la última de 6A. Si no aparece, el
    # script quedó generado con un modelo anterior o la migración no se creó.
    $migracionEsperada = "20261025015324_AddCitaPacienteFechaIndex"
    if (-not (Select-String -LiteralPath $OutputPath -SimpleMatch $migracionEsperada -Quiet)) {
        Write-Warning "El script no contiene la migración esperada: $migracionEsperada"
    }

    Write-Host "Script generado: $($archivo.FullName) ($tamanoKb KB)"
    Write-Host "Revísalo antes de entregarlo al DBA. No se debe versionar en el repositorio."

    [pscustomobject]@{
        Archivo = $archivo.FullName
        TamanoKB = $tamanoKb
        MigracionEsperada = $migracionEsperada
    }
}
finally {
    Pop-Location
}
