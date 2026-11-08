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

    # Se comprueba contra todas las migraciones del proyecto, no contra una lista fija:
    # así una migración nueva que no llegue al script (por un modelo desactualizado)
    # detiene la generación en lugar de entregar un script incompleto al DBA.
    $carpetaProyecto = Join-Path $raiz (Split-Path -Parent $Project)
    $carpetaMigraciones = Join-Path $carpetaProyecto "Migrations"
    if (-not (Test-Path -LiteralPath $carpetaMigraciones)) {
        throw "No existe la carpeta de migraciones: $carpetaMigraciones"
    }

    $migraciones = @(
        Get-ChildItem -LiteralPath $carpetaMigraciones -File -Filter "*.cs" |
            Where-Object { $_.Name -notlike "*.Designer.cs" -and $_.Name -ne "ApplicationDbContextModelSnapshot.cs" } |
            ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) } |
            Sort-Object
    )

    if ($migraciones.Count -eq 0) {
        throw "No se encontraron migraciones en $carpetaMigraciones."
    }

    $faltantes = @(
        $migraciones |
            Where-Object { -not (Select-String -LiteralPath $OutputPath -SimpleMatch $_ -Quiet) }
    )

    if ($faltantes.Count -gt 0) {
        throw "El script generado no contiene estas migraciones: $($faltantes -join ', '). Revisa que el proyecto compile y que el modelo esté actualizado."
    }

    Write-Host "Script generado: $($archivo.FullName) ($tamanoKb KB)"
    Write-Host "Migraciones verificadas: $($migraciones.Count)."
    Write-Host "Revísalo antes de entregarlo al DBA. No se debe versionar en el repositorio."

    [pscustomobject]@{
        Archivo = $archivo.FullName
        TamanoKB = $tamanoKb
        Migraciones = $migraciones.Count
    }
}
finally {
    Pop-Location
}
