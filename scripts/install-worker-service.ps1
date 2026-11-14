<#
.SYNOPSIS
    Publica Prueba.Worker y lo instala como Windows Service.

.DESCRIPTION
    1. Publica el Worker en InstallPath.
    2. Escribe en el appsettings.json publicado la ruta absoluta de la base SQLite
       (un servicio arranca en C:\Windows\System32, así que la ruta relativa no sirve).
    3. Crea el servicio con inicio automático y reinicio ante fallos, y lo arranca.
    Si el servicio ya existe, lo detiene y lo reemplaza.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\install-worker-service.ps1
#>
param(
    [string]$ServiceName = "PruebaWorker",
    [string]$InstallPath = "C:\Services\PruebaWorker",
    [string]$DatabasePath = (Join-Path (Split-Path $PSScriptRoot -Parent) "data\portal.db")
)

$ErrorActionPreference = "Stop"

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Ejecuta este script en PowerShell como administrador."
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot "src\Prueba.Worker\Prueba.Worker.csproj"
$exe = Join-Path $InstallPath "Prueba.Worker.exe"
$DatabasePath = [IO.Path]::GetFullPath($DatabasePath)

# Reemplazar una instalación anterior
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Deteniendo y eliminando el servicio existente $ServiceName..."
    if ($existing.Status -ne "Stopped") {
        Stop-Service -Name $ServiceName -Force
    }
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Publicando el Worker en $InstallPath..."
dotnet publish $project -c Release -r win-x64 --self-contained false -o $InstallPath
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish falló."
}

Write-Host "Configurando la base de datos: $DatabasePath"
New-Item -ItemType Directory -Force -Path (Split-Path $DatabasePath -Parent) | Out-Null
$settingsPath = Join-Path $InstallPath "appsettings.json"
$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
$settings.ConnectionStrings.Main = "Data Source=$DatabasePath"
$settings | ConvertTo-Json -Depth 10 | Set-Content -Path $settingsPath -Encoding UTF8

Write-Host "Creando el servicio $ServiceName..."
New-Service -Name $ServiceName `
    -BinaryPathName "`"$exe`"" `
    -DisplayName "Prueba Worker" `
    -Description "Procesa las solicitudes Pending contra el proveedor externo." `
    -StartupType Automatic | Out-Null

# Reinicio automático a los 60 s si el proceso cae; el contador de fallos se reinicia cada 24 h.
sc.exe failure $ServiceName reset= 86400 actions= restart/60000 | Out-Null

Start-Service -Name $ServiceName
Get-Service -Name $ServiceName | Format-Table Name, Status, StartType -AutoSize
Write-Host "Listo. Logs en el Visor de eventos > Registros de Windows > Aplicación (origen: $ServiceName)."
