@echo off
REM Pubquiz Platform V2 Docker Management Batch Script
REM Usage: docker-manage.bat [command]
REM Commands: build, up, down, logs, restart, shell, clean, status, backup

setlocal enabledelayedexpansion

set "COMMAND=%1"
if "%COMMAND%"=="" set "COMMAND=status"

REM Get the script directory
set "SCRIPT_DIR=%~dp0"

echo.
echo [Pubquiz Platform V2 - Docker Management]
echo Command: %COMMAND%
echo.

goto %COMMAND%

:build
echo Building Docker image...
docker-compose build
goto end

:up
echo Starting application in background...
docker-compose up -d
echo.
echo Application started!
echo Access at: http://localhost:5000
goto end

:down
echo Stopping application...
docker-compose down
echo Application stopped!
goto end

:logs
echo Showing logs (Ctrl+C to exit)...
docker-compose logs -f pubquiz-app
goto end

:restart
echo Restarting application...
docker-compose restart
echo Application restarted!
goto end

:shell
echo Entering container shell...
docker-compose exec pubquiz-app /bin/bash
goto end

:clean
setlocal
set /p CONFIRM="This will remove containers and volumes. Continue? (yes/no): "
if /i "%CONFIRM%"=="yes" (
    docker-compose down -v
    echo Cleanup complete!
) else (
    echo Cleanup cancelled.
)
endlocal
goto end

:status
echo Container Status:
docker-compose ps
echo.
echo Volume Status:
docker volume ls | findstr pubquiz
echo.
echo Health Check:
docker-compose exec pubquiz-app curl -s http://localhost:5000/health >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] Application is healthy
) else (
    echo [FAIL] Application health check failed
)
goto end

:backup
setlocal
echo Backing up database and keys...

for /f "tokens=2-4 delims=/ " %%a in ('date /t') do (set mydate=%%c%%a%%b)
for /f "tokens=1-2 delims=/:" %%a in ('time /t') do (set mytime=%%a%%b)
set "TIMESTAMP=%mydate%_%mytime%"

set "BACKUP_DIR=%SCRIPT_DIR%backup"
set "BACKUP_SUBDIR=%BACKUP_DIR%\%TIMESTAMP%"

if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"
mkdir "%BACKUP_SUBDIR%"

echo Backup directory: %BACKUP_SUBDIR%
echo Backing up database...
docker cp pubquiz-platform:/app/data/Pubquiz.sqlite "%BACKUP_SUBDIR%\" 2>nul

echo Backing up encryption keys...
docker cp pubquiz-platform:/app/keys "%BACKUP_SUBDIR%\" 2>nul

echo Backup complete!
endlocal
goto end

:default
echo Unknown command: %COMMAND%
echo.
echo Available commands:
echo   build   - Build Docker image
echo   up      - Start application in background
echo   down    - Stop application
echo   logs    - Show application logs
echo   restart - Restart application
echo   shell   - Open container shell
echo   clean   - Remove containers and volumes
echo   status  - Show container status
echo   backup  - Backup database and keys
goto end

:end
echo.
