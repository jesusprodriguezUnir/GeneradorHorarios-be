# Script para recrear la base de datos limpia de Docker
# Borra el volumen sqlserver-data para forzar un seed C# nuevo al arrancar

$ProjectRoot = Resolve-Path "$PSScriptRoot/.."

Write-Host "==========================================================" -ForegroundColor Yellow
Write-Host "     REINICIANDO CONTENEDOR Y VOLUMEN DE BASE DE DATOS" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Yellow

# Cambiar al directorio raíz donde se encuentra docker-compose.yml
Push-Location $ProjectRoot
try {
    Write-Host "`n[1/3] Destruyendo contenedor y volumen sqlserver-data..." -ForegroundColor Cyan
    docker compose down -v
    
    Write-Host "`n[2/3] Levantando contenedor de SQL Server de forma limpia..." -ForegroundColor Cyan
    docker compose up -d
}
catch {
    Write-Error "Fallo al ejecutar comandos de Docker Compose: $($_.Exception.Message)"
    Pop-Location
    exit 1
}
finally {
    Pop-Location
}

# 3. Esperar al healthcheck
Write-Host "`n[3/3] Esperando a que el healthcheck de SQL Server sea 'healthy'..." -ForegroundColor Cyan
$timeoutSeconds = 60
$elapsed = 0
$healthy = $false

while ($elapsed -lt $timeoutSeconds) {
    $status = docker inspect --format "{{.State.Health.Status}}" lectivo-sqlserver 2>$null
    if ($status -eq "healthy") {
        $healthy = $true
        break
    }
    
    # Imprimir progreso cada 6 segundos
    if ($elapsed % 6 -eq 0) {
        Write-Host "  - Estado actual del contenedor: $($status) (Esperando...)" -ForegroundColor Gray
    }
    
    Start-Sleep -Seconds 2
    $elapsed += 2
}

if ($healthy) {
    Write-Host "`n¡Contenedor de SQL Server activo y saludable!" -ForegroundColor Green
} else {
    Write-Warning "`nEl contenedor no ha alcanzado el estado 'healthy' en 60s. Esto puede deberse a la velocidad del host. La inicialización continuará de fondo."
}
