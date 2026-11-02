$ComposeFile = "docker-compose.dbservers.yml"

Write-Host "Stopping all database services..."
docker compose -f $ComposeFile down