param(
    [Parameter(Position = 0)]
    [string]$Service = $null,

    [int]$PostgresHostPort = 5432
)

$ComposeFile = Join-Path $PSScriptRoot "docker-compose.dbservers.yml"
$env:POSTGRES_HOST_PORT = $PostgresHostPort

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