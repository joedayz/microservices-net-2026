param(
    [string]$Namespace = "microservices"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command kubectl -ErrorAction SilentlyContinue)) {
    Write-Error "kubectl no esta instalado o no esta en PATH."
    exit 1
}

$processes = @()

function Start-PortForward {
    param(
        [string]$Service,
        [int]$LocalPort,
        [int]$RemotePort
    )

    $portMap = "$LocalPort`:$RemotePort"
    Write-Host "Iniciando: svc/$Service $portMap (ns=$Namespace)"

    $proc = Start-Process -FilePath "kubectl" `
        -ArgumentList @("-n", $Namespace, "port-forward", "svc/$Service", $portMap) `
        -NoNewWindow `
        -PassThru

    $script:processes += $proc
}

try {
    Start-PortForward -Service "gateway" -LocalPort 5010 -RemotePort 80
    Start-PortForward -Service "product-service" -LocalPort 5001 -RemotePort 5001
    Start-PortForward -Service "order-service" -LocalPort 5003 -RemotePort 5003

    Write-Host ""
    Write-Host "Port-forwards activos. Presiona Ctrl+C para detenerlos."

    while ($true) {
        Start-Sleep -Seconds 2

        foreach ($proc in $processes) {
            if ($proc.HasExited) {
                throw "Un port-forward se detuvo (PID: $($proc.Id))."
            }
        }
    }
}
finally {
    Write-Host ""
    Write-Host "Deteniendo port-forwards..."

    foreach ($proc in $processes) {
        if (-not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
