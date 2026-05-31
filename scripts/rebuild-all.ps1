# Script Orquestador Completo: Reconstrucción y Sembrado
# Ejecuta todo el flujo: reset-db -> dotnet run -> wait-forapi -> seed-dataset

. "$PSScriptRoot/_common.ps1"
$ProjectRoot = Resolve-Path "$PSScriptRoot/.."

Write-Host "==========================================================" -ForegroundColor Magenta
Write-Host "       LECTIVO: ORQUEstación DE RECONSTRUCCIÓN TOTAL" -ForegroundColor Magenta
Write-Host "==========================================================" -ForegroundColor Magenta

# 1. Reiniciar contenedor de Base de Datos SQL Server
& pwsh -NoProfile -File "$PSScriptRoot/reset-db.ps1"
if ($LASTEXITCODE -ne 0) { throw "reset-db falló con código $LASTEXITCODE." }

# 2. Comprobar e iniciar la API
Write-Host "`n[Orquestador] Comprobando disponibilidad del puerto de la API..." -ForegroundColor Cyan
$portInUse = Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue

if ($null -ne $portInUse) {
    Write-Host "  - El puerto 5000 ya está ocupado. Se utilizará la instancia de la API existente." -ForegroundColor Yellow
} else {
    Write-Host "  - Arrancando la API de dotnet en una nueva ventana..." -ForegroundColor Cyan
    $apiDir = Resolve-Path "$ProjectRoot/apps/api"
    try {
        Start-Process dotnet -ArgumentList "run" -WorkingDirectory $apiDir
        Write-Host "  - Terminal de API lanzada correctamente de forma externa." -ForegroundColor Gray
    } catch {
        Write-Warning "  - No se pudo iniciar dotnet run automáticamente."
        Write-Host "  -> Por favor, abre otra terminal, ve a 'apps/api' y ejecuta 'dotnet run'." -ForegroundColor Yellow
    }
}

# 3. Esperar que la API esté lista (esto da tiempo a la migración EF inicial)
Write-Host "`n[Orquestador] Esperando inicialización y migraciones de la API..." -ForegroundColor Cyan
Wait-ForApi -TimeoutSeconds 60

# 4. Sembrar el dataset de 18 grupos
Write-Host "`n[Orquestador] Lanzando el sembrador de datos..." -ForegroundColor Cyan
& pwsh -NoProfile -File "$PSScriptRoot/seed-dataset.ps1"
if ($LASTEXITCODE -ne 0) { throw "seed-dataset falló con código $LASTEXITCODE." }

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " ¡ENTORNO DE PRUEBAS RECONSTRUIDO Y SEMBRADO CON ÉXITO!" -ForegroundColor Green
Write-Host " Ya puedes abrir el frontend de Lectivo y empezar tus pruebas." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
