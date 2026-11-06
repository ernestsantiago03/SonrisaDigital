[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,
    [string]$Server = ".\SQLEXPRESS",
    [string]$SqlUser = "",
    [string]$SqlCmd = "sqlcmd",
    [string]$DataDirectory = "",
    [string]$LogDirectory = "",
    [string]$DatabaseTemporal = "",
    # Respaldo de archivos (Archivos_*.zip) de la misma fecha que el respaldo de la base.
    [string]$ArchivosBackup = "",
    # Contraseña para descifrar respaldos cifrados (.enc). Si se omite, se toma de SONRISADIGITAL_BACKUP_PASSWORD.
    [string]$ClaveRespaldo = ""
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
        [switch]$NoHeaders,
        [string]$Separator = ""
    )

    $argumentos = @("-S", $Server, "-C", "-b", "-Q", $Query)

    if ($NoHeaders) {
        $argumentos += @("-h", "-1", "-W")
    }

    if ($Separator) {
        $argumentos += @("-s", $Separator)
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
        throw "sqlcmd terminó con código $LASTEXITCODE al ejecutar la operación de restauración.`n$detalle"
    }

    return $salida
}

function Unprotect-File {
    param(
        [Parameter(Mandatory = $true)][string]$InputPath,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [Parameter(Mandatory = $true)][string]$Password
    )

    $completado = $false
    $inStream = $null
    $cryptoStream = $null
    $outStream = $null
    try {
        $inStream = [IO.File]::OpenRead($InputPath)
        $salt = New-Object byte[] 16
        $iv = New-Object byte[] 16
        if ($inStream.Read($salt, 0, 16) -ne 16 -or $inStream.Read($iv, 0, 16) -ne 16) {
            throw "El respaldo cifrado está incompleto."
        }

        $pbkdf2 = New-Object Security.Cryptography.Rfc2898DeriveBytes($Password, $salt, 100000, [Security.Cryptography.HashAlgorithmName]::SHA256)
        $key = $pbkdf2.GetBytes(32)

        $aes = [Security.Cryptography.Aes]::Create()
        $aes.Key = $key
        $aes.IV = $iv
        $decryptor = $aes.CreateDecryptor()

        $cryptoStream = New-Object Security.Cryptography.CryptoStream($inStream, $decryptor, [Security.Cryptography.CryptoStreamMode]::Read)
        $outStream = [IO.File]::Create($OutputPath)
        $buffer = New-Object byte[] (1 * 1024 * 1024)
        while (($read = $cryptoStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $outStream.Write($buffer, 0, $read)
        }
        $completado = $true
    }
    finally {
        # No se llama a Dispose del CryptoStream: en un descifrado fallido intenta vaciar el bloque
        # final, lanza otra excepción y dejaría los archivos abiertos.
        if ($inStream) { $inStream.Dispose() }
        if ($outStream) { $outStream.Dispose() }
        # Un descifrado fallido (clave incorrecta o archivo dañado) no debe dejar un .bak parcial.
        if (-not $completado -and (Test-Path -LiteralPath $OutputPath)) {
            Remove-Item -LiteralPath $OutputPath -Force -ErrorAction SilentlyContinue
        }
    }
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

if ([string]::IsNullOrWhiteSpace($ClaveRespaldo)) {
    $ClaveRespaldo = $env:SONRISADIGITAL_BACKUP_PASSWORD
}

# Descifrado transparente de respaldos cifrados (.enc).
$archivosTemporales = @()
if ($BackupFile.ToLowerInvariant().EndsWith(".enc")) {
    if ([string]::IsNullOrWhiteSpace($ClaveRespaldo)) {
        throw "El respaldo está cifrado. Indique -ClaveRespaldo o defina SONRISADIGITAL_BACKUP_PASSWORD."
    }
    # El .bak debe quedar en una carpeta que lea el servicio de SQL Server (junto al .enc), no en %TEMP%.
    $rutaDescifrada = Join-Path (Split-Path $BackupFile -Parent) ([System.IO.Path]::GetFileNameWithoutExtension($BackupFile))
    Unprotect-File -InputPath $BackupFile -OutputPath $rutaDescifrada -Password $ClaveRespaldo
    $archivosTemporales += $rutaDescifrada
    $BackupFile = $rutaDescifrada
}

if (-not [string]::IsNullOrWhiteSpace($ArchivosBackup) -and $ArchivosBackup.ToLowerInvariant().EndsWith(".enc")) {
    if ([string]::IsNullOrWhiteSpace($ClaveRespaldo)) {
        throw "El respaldo de archivos está cifrado. Indique -ClaveRespaldo o defina SONRISADIGITAL_BACKUP_PASSWORD."
    }
    $rutaZipDescifrado = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetFileNameWithoutExtension($ArchivosBackup))
    Unprotect-File -InputPath $ArchivosBackup -OutputPath $rutaZipDescifrado -Password $ClaveRespaldo
    $archivosTemporales += $rutaZipDescifrado
    $ArchivosBackup = $rutaZipDescifrado
}

# Se usa GetFullPath...
$BackupFile = [System.IO.Path]::GetFullPath($BackupFile)

if ([string]::IsNullOrWhiteSpace($DatabaseTemporal)) {
    $DatabaseTemporal = "SonrisaDigital_RestoreTest_$(Get-Date -Format 'yyyyMMddHHmmss')"
}

if ([string]::IsNullOrWhiteSpace($DataDirectory)) {
    $DataDirectory = Obtener-RutaInstancia -Propiedad "InstanceDefaultDataPath"
}

if ([string]::IsNullOrWhiteSpace($LogDirectory)) {
    $LogDirectory = Obtener-RutaInstancia -Propiedad "InstanceDefaultLogPath"
}

if ([string]::IsNullOrWhiteSpace($DataDirectory) -or [string]::IsNullOrWhiteSpace($LogDirectory)) {
    throw "No se pudo obtener la carpeta de datos de SQL Server. Indique -DataDirectory y -LogDirectory."
}

$backupSql = ConvertTo-SqlLiteral $BackupFile
$consultaLista = "RESTORE FILELISTONLY FROM DISK = N'$backupSql';"
$salidaLista = Invoke-SqlcmdSeguro -NoHeaders -Separator "|" -Query $consultaLista

$archivos = @()
foreach ($linea in $salidaLista) {
    if ([string]::IsNullOrWhiteSpace($linea)) {
        continue
    }

    $partes = $linea -split "\|"
    if ($partes.Count -lt 3) {
        continue
    }

    $tipo = $partes[2].Trim()
    if ($tipo -ne "D" -and $tipo -ne "L") {
        continue
    }

    $archivos += [pscustomobject]@{
        Logico = $partes[0].Trim()
        Tipo = $tipo
    }
}

if ($archivos.Count -eq 0) {
    throw "El respaldo no devolvió archivos de datos o de registro."
}

$movimientos = @()
$indiceDatos = 0
$indiceLogs = 0

foreach ($archivo in $archivos | Where-Object { $_.Tipo -eq "D" }) {
    $extension = "ndf"
    if ($indiceDatos -eq 0) {
        $extension = "mdf"
    }

    $destino = Join-Path $DataDirectory "$DatabaseTemporal`_$indiceDatos.$extension"
    $movimientos += "MOVE N'$(ConvertTo-SqlLiteral $archivo.Logico)' TO N'$(ConvertTo-SqlLiteral $destino)'"
    $indiceDatos++
}

foreach ($archivo in $archivos | Where-Object { $_.Tipo -eq "L" }) {
    $destino = Join-Path $LogDirectory "$DatabaseTemporal`_$indiceLogs.ldf"
    $movimientos += "MOVE N'$(ConvertTo-SqlLiteral $archivo.Logico)' TO N'$(ConvertTo-SqlLiteral $destino)'"
    $indiceLogs++
}

if ($movimientos.Count -eq 0) {
    throw "No se pudieron construir las cláusulas MOVE para la restauración."
}

$baseSql = $DatabaseTemporal.Replace("]", "]]")
$movesSql = $movimientos -join ", "
$consultaRestauracion = @"
IF DB_ID(N'$baseSql') IS NOT NULL
BEGIN
    ALTER DATABASE [$baseSql] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$baseSql];
END;

RESTORE DATABASE [$baseSql]
FROM DISK = N'$backupSql'
WITH REPLACE, RECOVERY, $movesSql, STATS = 10;

ALTER DATABASE [$baseSql] SET MULTI_USER;
DBCC CHECKDB(N'$baseSql') WITH NO_INFOMSGS;
"@

$restaurada = $false
try {
    Write-Host "Restaurando '$BackupFile' en la base temporal '$DatabaseTemporal'..."
    Invoke-SqlcmdSeguro -Query $consultaRestauracion | Out-Host
    $restaurada = $true
    Write-Host "Restauración completada y DBCC CHECKDB sin errores."

    if (-not [string]::IsNullOrWhiteSpace($ArchivosBackup)) {
        $rutaZip = [System.IO.Path]::GetFullPath($ArchivosBackup)
        $hashArchivo = Join-Path (Split-Path $rutaZip -Parent) "$(Split-Path $rutaZip -Leaf).sha256"
        if (-not (Test-Path -LiteralPath $hashArchivo)) {
            throw "Falta el hash SHA256 del respaldo de archivos: '$hashArchivo'."
        }

        $hashEsperado = ((Get-Content -LiteralPath $hashArchivo -TotalCount 1).Split(' ')[0]).Trim()
        $hashReal = (Get-FileHash -LiteralPath $rutaZip -Algorithm SHA256).Hash
        if ($hashEsperado -ne $hashReal) {
            throw "El respaldo de archivos no coincide con su hash. Puede estar dañado o alterado."
        }

        $carpetaArchivos = Join-Path ([System.IO.Path]::GetTempPath()) "sd-restore-$DatabaseTemporal"
        if (Test-Path -LiteralPath $carpetaArchivos) {
            Remove-Item -LiteralPath $carpetaArchivos -Recurse -Force
        }

        try {
            Expand-Archive -LiteralPath $rutaZip -DestinationPath $carpetaArchivos

            # Cada radiografía registrada en la base restaurada debe tener su archivo y su miniatura.
            $consultaRegistros = "SET NOCOUNT ON; USE [$baseSql]; SELECT ClaveArchivo, ClaveMiniatura FROM ArchivosPaciente;"
            $filasRegistros = @(Invoke-SqlcmdSeguro -NoHeaders -Separator "|" -Query $consultaRegistros |
                Where-Object { $_ -and $_.Contains("|") })

            $faltantes = @()
            foreach ($fila in $filasRegistros) {
                foreach ($clave in ($fila -split "\|")) {
                    $ruta = Join-Path $carpetaArchivos ($clave.Trim() -replace "/", "\")
                    if (-not (Test-Path -LiteralPath $ruta)) {
                        $faltantes += $clave.Trim()
                    }
                }
            }

            if ($faltantes.Count -gt 0) {
                throw "La base restaurada tiene $($faltantes.Count) archivo(s) sin respaldo, por ejemplo: $($faltantes[0])"
            }

            Write-Host "Archivos verificados: $($filasRegistros.Count) registro(s) con su archivo y miniatura."
        }
        finally {
            Remove-Item -LiteralPath $carpetaArchivos -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
finally {
    $consultaLimpieza = @"
IF DB_ID(N'$baseSql') IS NOT NULL
BEGIN
    ALTER DATABASE [$baseSql] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$baseSql];
END;
"@

    try {
        Invoke-SqlcmdSeguro -Query $consultaLimpieza | Out-Null
    }
    catch {
        Write-Warning "No se pudo eliminar la base temporal '$DatabaseTemporal': $($_.Exception.Message)"
    }

    foreach ($temporal in $archivosTemporales) {
        Remove-Item -LiteralPath $temporal -Force -ErrorAction SilentlyContinue
    }
}

[pscustomobject]@{
    Restaurada = $restaurada
    BaseTemporal = $DatabaseTemporal
    Archivo = $BackupFile
}
