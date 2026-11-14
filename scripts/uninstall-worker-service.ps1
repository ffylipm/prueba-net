<#
.SYNOPSIS
    Detiene y elimina el Windows Service del Worker. No borra los archivos publicados ni la base.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\uninstall-worker-service.ps1
#>
param(
    [string]$ServiceName = "PruebaWorker"
)

$ErrorActionPreference = "Stop"

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Ejecuta este script en PowerShell como administrador."
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $service) {
    Write-Host "El servicio $ServiceName no está instalado."
    return
}

if ($service.Status -ne "Stopped") {
    Stop-Service -Name $ServiceName -Force
}

sc.exe delete $ServiceName | Out-Null
Write-Host "Servicio $ServiceName eliminado."
