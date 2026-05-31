# Reutilización de URL y Helpers para Lectivo API
# No ejecutar este script directamente. Se importa con: . "$PSScriptRoot/_common.ps1"

$BaseUrl = "http://localhost:5000/api"
$AdminEmail = "elena.castro@ceip-miguel-hernandez.es"

function Get-ApiHeaders {
    return @{
        "X-User-Email" = $AdminEmail
        "Content-Type" = "application/json"
    }
}

function Invoke-Api {
    param (
        [Parameter(Mandatory=$true)]
        [string]$Method,
        [Parameter(Mandatory=$true)]
        [string]$Uri,
        [object]$Body = $null
    )

    $url = "$BaseUrl/$Uri"
    $headers = Get-ApiHeaders
    $params = @{
        Method = $Method
        Uri = $url
        Headers = $headers
    }

    if ($null -ne $Body) {
        # Si ya es un String JSON, usarlo directamente; si no, serializar
        if ($Body -is [string]) {
            $params.Body = $Body
        } else {
            $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
        }
    }

    try {
        $response = Invoke-RestMethod @params
        return $response
    } catch {
        $statusCode = "N/A"
        if ($null -ne $_.Exception.Response) {
            $statusCode = $_.Exception.Response.StatusCode
            try {
                $streamReader = [System.IO.StreamReader]($_.Exception.Response.GetResponseStream())
                $errBody = $streamReader.ReadToEnd()
                Write-Host "API Error Body: $errBody" -ForegroundColor Red
            } catch {}
        }
        Write-Error "API Request Failed ($Method $url) - Status Code: $statusCode - Error: $($_.Exception.Message)"
        throw $_
    }
}

function Wait-ForApi {
    param (
        [int]$TimeoutSeconds = 60
    )

    Write-Host "Esperando a que la API responda en $BaseUrl..." -ForegroundColor Cyan
    $elapsed = 0
    $ready = $false
    while ($elapsed -lt $TimeoutSeconds) {
        try {
            $response = Invoke-WebRequest -Uri "$BaseUrl/auth/me" -Headers @{ "X-User-Email" = $AdminEmail } -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) {
                $ready = $true
                break
            }
        } catch {
            # Ignorar fallos de conexión temporalmente
        }
        Start-Sleep -Seconds 2
        $elapsed += 2
    }

    if ($ready) {
        Write-Host "¡API lista y respondiendo correctamente!" -ForegroundColor Green
    } else {
        Write-Error "Timeout esperando a que la API respondiera."
        exit 1
    }
}
