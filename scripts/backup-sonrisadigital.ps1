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
    [switch]$Compress,
    # Cifra el respaldo (base y archivos) con AES-256 derivado de una contraseña.
    [switch]$Cifrar,
    # Contraseña de cifrado. Si se omite, se toma de SONRISADIGITAL_BACKUP_PASSWORD.
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

function Get-HmacArchivo {
    param(
        [Parameter(Mandatory = $true)][string]$Ruta,
        [Parameter(Mandatory = $true)][byte[]]$Clave,
        [Parameter(Mandatory = $true)][long]$Longitud
    )

    $hmac = [Security.Cryptography.IncrementalHash]::CreateHMAC([Security.Cryptography.HashAlgorithmName]::SHA256, $Clave)
    $fs = [IO.File]::OpenRead($Ruta)
    try {
        $restante = $Longitud
        $buffer = New-Object byte[] (1 * 1024 * 1024)
        while ($restante -gt 0) {
            $n = [int][Math]::Min($buffer.Length, $restante)
            $leidos = $fs.Read($buffer, 0, $n)
            if ($leidos -le 0) {
                throw "El archivo cambió mientras se calculaba su integridad."
            }
            $hmac.AppendData($buffer, 0, $leidos)
            $restante -= $leidos
        }
        return $hmac.GetHashAndReset()
    }
    finally {
        $fs.Dispose()
        $hmac.Dispose()
    }
}

function Test-IgualesConstante {
    param([byte[]]$A, [byte[]]$B)

    if ($A.Length -ne $B.Length) {
        return $false
    }

    $diferencia = 0
    for ($i = 0; $i -lt $A.Length; $i++) {
        $diferencia = $diferencia -bor ($A[$i] -bxor $B[$i])
    }
    return $diferencia -eq 0
}

# Formato: "SDENC2" | salt (16) | IV (16) | cifrado AES-256-CBC | HMAC-SHA256 (32) sobre todo lo anterior.
# La clave AES y la clave HMAC salen de la misma contraseña, con PBKDF2-SHA256 y claves distintas.
function Protect-File {
    param(
        [Parameter(Mandatory = $true)][string]$InputPath,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [Parameter(Mandatory = $true)][string]$Password
    )

    $cabecera = [Text.Encoding]::ASCII.GetBytes("SDENC2")
    $salt = New-Object byte[] 16
    $iv = New-Object byte[] 16
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($salt)
    $rng.GetBytes($iv)
    $rng.Dispose()

    $pbkdf2 = New-Object Security.Cryptography.Rfc2898DeriveBytes($Password, $salt, 100000, [Security.Cryptography.HashAlgorithmName]::SHA256)
    $claves = $pbkdf2.GetBytes(64)
    $claveCifrado = New-Object byte[] 32
    [Array]::Copy($claves, 0, $claveCifrado, 0, 32)
    $claveMac = New-Object byte[] 32
    [Array]::Copy($claves, 32, $claveMac, 0, 32)

    $aes = [Security.Cryptography.Aes]::Create()
    $aes.Key = $claveCifrado
    $aes.IV = $iv
    $encryptor = $aes.CreateEncryptor()

    $completado = $false
    $outStream = $null
    $inStream = $null
    try {
        $outStream = [IO.File]::Create($OutputPath)
        $outStream.Write($cabecera, 0, $cabecera.Length)
        $outStream.Write($salt, 0, 16)
        $outStream.Write($iv, 0, 16)

        $cryptoStream = New-Object Security.Cryptography.CryptoStream($outStream, $encryptor, [Security.Cryptography.CryptoStreamMode]::Write)
        $inStream = [IO.File]::OpenRead($InputPath)
        $buffer = New-Object byte[] (1 * 1024 * 1024)
        while (($read = $inStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $cryptoStream.Write($buffer, 0, $read)
        }
        $cryptoStream.FlushFinalBlock()
        $inStream.Dispose()
        $inStream = $null
        $outStream.Dispose()
        $outStream = $null

        $etiqueta = Get-HmacArchivo -Ruta $OutputPath -Clave $claveMac -Longitud (Get-Item -LiteralPath $OutputPath).Length
        $salida = [IO.File]::Open($OutputPath, [IO.FileMode]::Append, [IO.FileAccess]::Write)
        try {
            $salida.Write($etiqueta, 0, $etiqueta.Length)
        }
        finally {
            $salida.Dispose()
        }
        $completado = $true
    }
    finally {
        # Se cierran los streams de archivo directamente: el CryptoStream ya vació su bloque final
        # en el caso correcto, y en un fallo no debe impedir liberar los archivos.
        if ($inStream) { $inStream.Dispose() }
        if ($outStream) { $outStream.Dispose() }
        # Un cifrado incompleto no debe quedar en la carpeta de respaldos.
        if (-not $completado -and (Test-Path -LiteralPath $OutputPath)) {
            Remove-Item -LiteralPath $OutputPath -Force -ErrorAction SilentlyContinue
        }
        $aes.Dispose()
    }
}
if ($SqlUser -and [string]::IsNullOrWhiteSpace($env:SQLCMDPASSWORD)) {
    throw "Para usar autenticación SQL defina la variable SQLCMDPASSWORD. No pase la contraseña por parámetro."
}

if ($Cifrar -and [string]::IsNullOrWhiteSpace($ClaveRespaldo)) {
    $ClaveRespaldo = $env:SONRISADIGITAL_BACKUP_PASSWORD
}

if ($Cifrar -and [string]::IsNullOrWhiteSpace($ClaveRespaldo)) {
    throw "Para cifrar el respaldo indique -ClaveRespaldo o defina SONRISADIGITAL_BACKUP_PASSWORD."
}

if ([string]::IsNullOrWhiteSpace($BackupDirectory)) {
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
    $patron = "${Database}_FULL_*"
    Get-ChildItem -LiteralPath $BackupDirectory -Filter "$patron*" -File |
        Where-Object { $_.LastWriteTime -lt $limite } |
        ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Force
            Write-Host "Eliminado por retención: $($_.Name)"
        }

    $tamanoMb = [math]::Round($archivo.Length / 1MB, 2)
}
else {
    Write-Warning "No se calcularon SHA256 ni retención porque el archivo está en una carpeta protegida."
}

Write-Host "Respaldo verificado correctamente."

# Cifrado opcional del respaldo de la base.
$rutaRespaldoFinal = $rutaRespaldo
if ($Cifrar) {
    if (-not $archivo) {
        throw "No se puede cifrar: la cuenta actual no puede leer '$rutaRespaldo'. Use -BackupDirectory con una carpeta accesible para el operador."
    }
    $rutaCifrada = "$rutaRespaldo.enc"
    Write-Host "Cifrando el respaldo de la base..."
    Protect-File -InputPath $rutaRespaldo -OutputPath $rutaCifrada -Password $ClaveRespaldo
    Remove-Item -LiteralPath $rutaRespaldo -Force
    if (Test-Path -LiteralPath "$rutaRespaldo.sha256") {
        Remove-Item -LiteralPath "$rutaRespaldo.sha256" -Force
    }
    $hash = (Get-FileHash -LiteralPath $rutaCifrada -Algorithm SHA256).Hash
    Set-Content -LiteralPath "$rutaCifrada.sha256" -Value "$hash  $(Split-Path $rutaCifrada -Leaf)" -Encoding ascii
    $rutaRespaldoFinal = $rutaCifrada
    Write-Host "Respaldo cifrado: $rutaCifrada"
}

# La base y los archivos deben quedar del mismo momento: si uno de los dos falta, el respaldo no es válido.
$rutaArchivos = ""
$hashArchivos = ""
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

    if ($Cifrar) {
        $rutaZipCifrada = "$rutaArchivos.enc"
        Write-Host "Cifrando el respaldo de archivos..."
        Protect-File -InputPath $rutaArchivos -OutputPath $rutaZipCifrada -Password $ClaveRespaldo
        Remove-Item -LiteralPath $rutaArchivos -Force
        Remove-Item -LiteralPath "$rutaArchivos.sha256" -Force -ErrorAction SilentlyContinue
        $hashArchivos = (Get-FileHash -LiteralPath $rutaZipCifrada -Algorithm SHA256).Hash
        Set-Content -LiteralPath "$rutaZipCifrada.sha256" -Value "$hashArchivos  $(Split-Path $rutaZipCifrada -Leaf)" -Encoding ascii
        $rutaArchivos = $rutaZipCifrada
        Write-Host "Archivos cifrados: $rutaZipCifrada"
    }

    $limiteArchivos = (Get-Date).AddDays(-$RetentionDays)
    Get-ChildItem -LiteralPath $BackupDirectory -Filter "Archivos_*" -File |
        Where-Object { $_.LastWriteTime -lt $limiteArchivos } |
        ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Force
            Remove-Item -LiteralPath "$($_.FullName).sha256" -Force -ErrorAction SilentlyContinue
            Write-Host "Eliminado por retención: $($_.Name)"
        }
}

# Manifiesto con los hashes de ambos componentes para verificar integridad posterior.
$manifiesto = Join-Path $BackupDirectory "manifest_$fecha.json"
@{
    Fecha = (Get-Date).ToString("o")
    BaseDatos = $Database
    Base = @{
        Archivo = (Split-Path $rutaRespaldoFinal -Leaf)
        HashSHA256 = $hash
        Cifrado = [bool]$Cifrar
    }
    Archivos = if ($rutaArchivos) { @{
        Archivo = (Split-Path $rutaArchivos -Leaf)
        HashSHA256 = $hashArchivos
        Cifrado = [bool]$Cifrar
    } } else { $null }
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifiesto -Encoding utf8

[pscustomobject]@{
    BaseDatos = $Database
    Archivo = $rutaRespaldoFinal
    TamanoMB = $tamanoMb
    HashSHA256 = $hash
    ArchivoAccesible = [bool]$archivo
    Verificado = $true
    Cifrado = [bool]$Cifrar
    ArchivosRespaldo = $rutaArchivos
    HashArchivosSHA256 = $hashArchivos
    Manifiesto = $manifiesto
}
