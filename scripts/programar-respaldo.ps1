<#
.SYNOPSIS
    Registra en el Programador de tareas el respaldo diario de la base y de la carpeta de archivos.

.DESCRIPTION
    Crea una tarea que ejecuta backup-sonrisadigital.ps1 todos los días a la hora indicada (por defecto
    00:00). Respalda la base y la carpeta de archivos con la misma fecha. Use un disco distinto al de la
    aplicación para que una falla del disco no se lleve también los respaldos.

.EXAMPLE
    .\scripts\programar-respaldo.ps1 -BackupDirectory "D:\Respaldos\SonrisaDigital" -ArchivosDirectory "C:\SonrisaDigital\Archivos"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ArchivosDirectory,

    [string]$Hora = "00:00",

    [string]$NombreTarea = "SonrisaDigital-Respaldo-Diario",

    [string]$Server = ".\SQLEXPRESS",
    [string]$Database = "SonrisaDigital"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$esAdministrador = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $esAdministrador) {
    throw "Ejecuta PowerShell como administrador para registrar la tarea. Sin eso, Windows rechaza la creación con 'acceso denegado'."
}

if (-not (Test-Path -LiteralPath $ArchivosDirectory -PathType Container)) {
    throw "La carpeta de archivos '$ArchivosDirectory' no existe."
}

$scriptRespaldo = Join-Path $PSScriptRoot "backup-sonrisadigital.ps1"
if (-not (Test-Path -LiteralPath $scriptRespaldo)) {
    throw "No se encontró backup-sonrisadigital.ps1 junto a este script."
}

$argumentos = "-NoProfile -ExecutionPolicy Bypass -File `"$scriptRespaldo`" " +
    "-Server `"$Server`" -Database `"$Database`" " +
    "-BackupDirectory `"$BackupDirectory`" -ArchivosDirectory `"$ArchivosDirectory`""

$accion = New-ScheduledTaskAction -Execute "powershell.exe" -Argument $argumentos
$disparador = New-ScheduledTaskTrigger -Daily -At $Hora
$configuracion = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2)

# La tarea corre con la cuenta de quien la registra, sin guardar contraseña y solo con acceso local.
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType S4U -RunLevel Limited

Register-ScheduledTask -TaskName $NombreTarea -Action $accion -Trigger $disparador `
    -Settings $configuracion -Principal $principal -Force | Out-Null

Write-Host "Tarea '$NombreTarea' registrada: respaldo diario a las $Hora."
Write-Host "Base: $Database en $Server -> $BackupDirectory"
Write-Host "Archivos: $ArchivosDirectory"
