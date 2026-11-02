param(
    [Parameter(Position = 0)]
    [string]$Service = $null
)

$ComposeFile = "docker-compose.dbservers.yml"

if ([string]::IsNullOrWhiteSpace($Service)) {
    Write-Host "Starting all database services..."
    docker compose -f $ComposeFile up -d
}
else {
    Write-Host "Starting database service: $Service"
    docker compose -f $ComposeFile up -d $Service
}

if ($LASTEXITCODE -ne 0) {
    throw "Docker Compose failed with exit code $LASTEXITCODE"
}