# Pubquiz Platform V2 Docker Management Script
# Run from the project root directory

param(
    [ValidateSet('build', 'up', 'down', 'logs', 'restart', 'shell', 'clean', 'status', 'backup')]
    [string]$Command = 'status'
)

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = $scriptPath

function Build-Docker {
    Write-Host "Building Docker image..." -ForegroundColor Green
    docker-compose build
    Write-Host "Build complete!" -ForegroundColor Green
}

function Start-Docker {
    Write-Host "Starting application..." -ForegroundColor Green
    docker-compose up
}

function Start-DockerBackground {
    Write-Host "Starting application in background..." -ForegroundColor Green
    docker-compose up -d
    Write-Host "Application started!" -ForegroundColor Green
    Write-Host "Access at: http://localhost:5000" -ForegroundColor Cyan
}

function Stop-Docker {
    Write-Host "Stopping application..." -ForegroundColor Yellow
    docker-compose down
    Write-Host "Application stopped!" -ForegroundColor Green
}

function Show-Logs {
    Write-Host "Showing logs (Ctrl+C to exit)..." -ForegroundColor Green
    docker-compose logs -f
}

function Restart-Docker {
    Write-Host "Restarting application..." -ForegroundColor Yellow
    docker-compose restart
    Write-Host "Application restarted!" -ForegroundColor Green
}

function Enter-Shell {
    Write-Host "Entering container shell..." -ForegroundColor Green
    docker-compose exec pubquiz-app /bin/bash
}

function Clean-Docker {
    Write-Host "Cleaning up Docker resources..." -ForegroundColor Yellow
    $confirm = Read-Host "This will remove containers and volumes. Continue? (yes/no)"
    
    if ($confirm -eq 'yes') {
        docker-compose down -v
        Write-Host "Cleanup complete!" -ForegroundColor Green
    } else {
        Write-Host "Cleanup cancelled." -ForegroundColor Yellow
    }
}

function Show-Status {
    Write-Host "Container Status:" -ForegroundColor Green
    docker-compose ps
    Write-Host ""
    Write-Host "Volume Status:" -ForegroundColor Green
    docker volume ls | findstr pubquiz
    Write-Host ""
    Write-Host "Health Check:" -ForegroundColor Green
    $health = docker-compose exec pubquiz-app curl -s http://localhost:5000/health 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Application is healthy" -ForegroundColor Green
    } else {
        Write-Host "✗ Application health check failed" -ForegroundColor Red
    }
}

function Backup-Data {
    Write-Host "Backing up database and keys..." -ForegroundColor Green
    
    $backupDir = Join-Path $projectRoot "backup"
    if (-not (Test-Path $backupDir)) {
        New-Item -ItemType Directory -Path $backupDir | Out-Null
    }
    
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupSubDir = Join-Path $backupDir $timestamp
    New-Item -ItemType Directory -Path $backupSubDir | Out-Null
    
    Write-Host "Backup directory: $backupSubDir" -ForegroundColor Cyan
    
    # Backup database
    Write-Host "Backing up database..." -ForegroundColor Yellow
    docker cp pubquiz-platform:/app/data/Pubquiz.sqlite "$backupSubDir\" 2>$null
    
    # Backup keys
    Write-Host "Backing up encryption keys..." -ForegroundColor Yellow
    docker cp pubquiz-platform:/app/keys "$backupSubDir\" 2>$null
    
    Write-Host "Backup complete!" -ForegroundColor Green
    Get-ChildItem $backupSubDir
}

# Execute command
switch ($Command) {
    'build' { Build-Docker }
    'up' { Start-DockerBackground }
    'down' { Stop-Docker }
    'logs' { Show-Logs }
    'restart' { Restart-Docker }
    'shell' { Enter-Shell }
    'clean' { Clean-Docker }
    'status' { Show-Status }
    'backup' { Backup-Data }
    default { Show-Status }
}
