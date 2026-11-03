[CmdletBinding()]
param(
    [string]$Server = ".\SQLEXPRESS",
    [string]$Database = "SonrisaDigital",
    [string]$BackupDirectory = "",
    # Carpeta de radiografías y fotos (Archivos:Directorio). Se respalda con la misma fecha que la base.
    [string]$ArchivosDirectory = "",
    [ValidateRange(1, 3650)]
    [int]$RetentionDays = 30,
    [string]$SqlUser = "",
    [string]$SqlCmd = "sqlcmd",
    [switch]$Compress
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function ConvertTo-SqlLiteral {
    param([string]$Value)
    return $Value.Replace("'", "''")
}

function Invoke-SqlcmdSeguro {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Query,
        [switch]$NoHeaders
    )

    $argumentos = @("-S", $Server, "-C", "-b", "-Q", $Query)

    if ($NoHeaders) {
        $argumentos += @("-h", "-1", "-W")
    }

    if ($SqlUser) {
        $argumentos += @("-U", $SqlUser)
    }
    else {
        $argumentos += "-E"
    }

    $salida = & $SqlCmd @argumentos 2>&1
    if ($LASTEXITCODE -ne 0) {
        $detalle = ($salida | Out-String).Trim()
        throw "sqlcmd terminó con código $LASTEXITCODE al ejecutar la operación de respaldo.`n$detalle"
    }

    return $salida
}

function Obtener-RutaInstancia {
    param([string]$Propiedad)

    $consulta = "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY(N'$Propiedad') AS nvarchar(4000));"
    $salida = Invoke-SqlcmdSeguro -NoHeaders -Query $consulta
    $linea = $salida | Where-Object { $_ -and $_.Trim() } | Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($linea) -or $linea.Trim() -eq "NULL") {
        return ""
    }

    return $linea.Trim()
}

if ($SqlUser -and [string]::IsNullOrWhiteSpace($env:SQLCMDPASSWORD)) {
    throw "Para usar autenticación SQL defina la variable SQLCMDPASSWORD. No pase la contraseña por parámetro."
}

if ([string]::IsNullOrWhiteSpace($BackupDirectory)) {
    # Se usa la carpeta de respaldos de la instancia para que la cuenta de
    # servicio de SQL Server tenga permiso de escritura sin configuración extra.
    $BackupDirectory = Obtener-RutaInstancia -Propiedad "InstanceDefaultBackupPath"
}

if ([string]::IsNullOrWhiteSpace($BackupDirectory)) {
    throw "No se pudo obtener la carpeta de respaldos de SQL Server. Indique -BackupDirectory con una ruta escribible por la cuenta de servicio."
}

if (-not (Test-Path -LiteralPath $BackupDirectory)) {
    New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null
}

$baseSql = $Database.Replace("]", "]]")
$existe = Invoke-SqlcmdSeguro -NoHeaders -Query "SET NOCOUNT ON; SELECT DB_ID(N'$(ConvertTo-SqlLiteral $Database)');"
$existeTexto = ($existe | Where-Object { $_ -and $_.Trim() } | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($existeTexto) -or $existeTexto.Trim() -eq "NULL") {
    throw "La base de datos '$Database' no existe en '$Server'."
}

$fecha = Get-Date -Format "yyyyMMdd-HHmmss"
$nombreArchivo = "${Database}_FULL_$fecha.bak"
$rutaRespaldo = Join-Path $BackupDirectory $nombreArchivo
$rutaSql = ConvertTo-SqlLiteral $rutaRespaldo

$opciones = "INIT, CHECKSUM, STATS = 10"
if ($Compress) {
    $opciones += ", COMPRESSION"
}

Write-Host "Respaldando '$Database' en '$rutaRespaldo'..."
$consultaRespaldo = @"
BACKUP DATABASE [$baseSql]
TO DISK = N'$rutaSql'
WITH $opciones;
"@
Invoke-SqlcmdSeguro -Query $consultaRespaldo | Out-Host

$archivo = $null
try {
    $archivo = Get-Item -LiteralPath $rutaRespaldo -ErrorAction Stop
}
catch {
    Write-Warning "SQL Server creó el respaldo, pero la cuenta actual no puede leerlo en '$rutaRespaldo'."
}

Write-Host "Verificando la integridad del respaldo..."
$consultaVerificacion = "RESTORE VERIFYONLY FROM DISK = N'$rutaSql' WITH CHECKSUM;"
Invoke-SqlcmdSeguro -Query $consultaVerificacion | Out-Host

$hash = $null
$tamanoMb = $null

if ($archivo) {
    $hash = (Get-FileHash -LiteralPath $rutaRespaldo -Algorithm SHA256).Hash
    $archivoHash = "$rutaRespaldo.sha256"
    Set-Content -LiteralPath $archivoHash -Value "$hash  $nombreArchivo" -Encoding ascii

    $limite = (Get-Date).AddDays(-$RetentionDays)
    $patron = "${Database}_FULL_*.bak"
    Get-ChildItem -LiteralPath $BackupDirectory -Filter $patron -File |
        Where-Object { $_.LastWriteTime -lt $limite } |
        ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Force
            $hashViejo = "$($_.FullName).sha256"
            if (Test-Path -LiteralPath $hashViejo) {
                Remove-Item -LiteralPath $hashViejo -Force
            }
            Write-Host "Eliminado por retención: $($_.Name)"
        }

    $tamanoMb = [math]::Round($archivo.Length / 1MB, 2)
}
else {
    Write-Warning "No se calcularon SHA256 ni retención porque el archivo está en una carpeta protegida. Use -BackupDirectory con una carpeta compartida y permisos de lectura para el operador."
}

Write-Host "Respaldo verificado correctamente."

# La base y los archivos deben quedar del mismo momento: si uno de los dos falta, el respaldo no es válido.
$rutaArchivos = ""
$hashArchivos = $null
if (-not [string]::IsNullOrWhiteSpace($ArchivosDirectory)) {
    if (-not (Test-Path -LiteralPath $ArchivosDirectory -PathType Container)) {
        throw "La carpeta de archivos '$ArchivosDirectory' no existe. No se creó un respaldo incompleto."
    }

    $nombreZip = "Archivos_$fecha.zip"
    $rutaArchivos = Join-Path $BackupDirectory $nombreZip
    Write-Host "Respaldando la carpeta de archivos en '$rutaArchivos'..."
    Compress-Archive -Path (Join-Path $ArchivosDirectory "*") -DestinationPath $rutaArchivos -CompressionLevel Optimal -Force

    $hashArchivos = (Get-FileHash -LiteralPath $rutaArchivos -Algorithm SHA256).Hash
    Set-Content -LiteralPath "$rutaArchivos.sha256" -Value "$hashArchivos  $nombreZip" -Encoding ascii

    $limiteArchivos = (Get-Date).AddDays(-$RetentionDays)
    Get-ChildItem -LiteralPath $BackupDirectory -Filter "Archivos_*.zip" -File |
        Where-Object { $_.LastWriteTime -lt $limiteArchivos } |
        ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Force
            Remove-Item -LiteralPath "$($_.FullName).sha256" -Force -ErrorAction SilentlyContinue
            Write-Host "Eliminado por retención: $($_.Name)"
        }
}

[pscustomobject]@{
    BaseDatos = $Database
    Archivo = $rutaRespaldo
    TamanoMB = $tamanoMb
    HashSHA256 = $hash
    ArchivoAccesible = [bool]$archivo
    Verificado = $true
    ArchivosRespaldo = $rutaArchivos
    HashArchivosSHA256 = $hashArchivos
}
