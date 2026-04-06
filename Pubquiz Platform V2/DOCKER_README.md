# Docker Setup for Pubquiz Platform V2

This directory contains Docker configuration for running the Pubquiz Platform V2 application in containers.

## Prerequisites

- Docker Desktop installed (Windows 10+ or macOS)
- Docker CLI tools available
- Ports 5000 available on your machine

## Quick Start

### Build and Run

```powershell
# Navigate to the project root directory
cd "C:\Users\the_s\source\repos\Pubquiz Platform V2"

# Build the Docker image
docker-compose build

# Start the application
docker-compose up

# Application will be available at: http://localhost:5000
```

### Stopping the Container

```powershell
docker-compose down
```

### View Logs

```powershell
# Real-time logs
docker-compose logs -f

# Logs for specific service
docker-compose logs -f pubquiz-app
```

## Architecture

### Services

**pubquiz-app**
- Main ASP.NET Core 8 application
- Listens on port 5000 (HTTP)
- Uses SQLite database persisted in volume
- Data protection keys persisted in volume for 2FA security

**pubquiz-db-setup**
- Initialization service
- Creates volume directories with proper permissions
- Runs once during container startup

### Volumes

**pubquiz-data**
- Persists SQLite database file (`Pubquiz.sqlite`)
- Ensures data survives container restarts
- Location in container: `/app/data`

**pubquiz-keys**
- Persists ASP.NET Core data protection keys
- **CRITICAL** for 2FA functionality (secrets and recovery codes must be decryptable)
- Location in container: `/app/keys`

## Configuration

### Connection String

The application uses the connection string from `appsettings.Development.json`:

```json
"ConnectionStrings": {
    "DefaultConnection": "Data Source=/app/data/Pubquiz.sqlite"
}
```

This points to the persistent volume mount location.

### Authentication & 2FA

**Configured Settings:**
- Cookie persistence: `HttpOnly = true` ✓
- Secure policy: `CookieSecurePolicy.SameAsRequest` ✓
- SameSite attribute: `SameSiteMode.Lax` ✓
- Sliding expiration: 30 days ✓
- Data protection keys: Persisted to volume ✓

**Flow:**
1. User login → credentials validated
2. If 2FA enabled → redirected to 2FA code input
3. User enters TOTP code or recovery code
4. Code verified using data protection keys
5. User signed in and cookie set
6. Cookie persists across container restarts due to key persistence

### Database Migrations

Migrations run automatically on application startup:
- Uses Entity Framework Core to apply pending migrations
- Creates tables if they don't exist
- Safe for repeated container starts

## Troubleshooting

### Issue: Authentication/2FA Fails After Container Restart

**Cause:** Data protection keys not persisting
**Solution:** Verify volumes are mounted correctly

```powershell
docker-compose exec pubquiz-app ls -la /app/keys
docker-compose exec pubquiz-app ls -la /app/data
```

### Issue: Database File Not Found

**Cause:** Volume not properly mounted or created
**Solution:** 

```powershell
# Remove volumes and recreate
docker-compose down -v
docker-compose up

# Verify volume exists
docker volume ls | findstr pubquiz
```

### Issue: "System.IO.DirectoryNotFoundException"

**Cause:** Volume directories don't exist
**Solution:** Ensure `pubquiz-db-setup` service runs successfully

```powershell
docker-compose logs pubquiz-db-setup
```

### Issue: Port 5000 Already in Use

**Solution:** Change port in `docker-compose.yml`

```yaml
ports:
  - "5001:5000"  # Maps port 5001 on host to 5000 in container
```

Then access at `http://localhost:5001`

### Issue: Cannot Connect to Application

**Check Health:**
```powershell
docker-compose ps  # Should show "healthy" status
```

**Check Logs:**
```powershell
docker-compose logs pubquiz-app
```

## Development Workflow

### Local Changes

If you modify code locally:

```powershell
# Rebuild the image
docker-compose build --no-cache

# Restart with new image
docker-compose up
```

### Accessing Container Shell

```powershell
# Interactive shell in app container
docker-compose exec pubquiz-app /bin/bash

# Or with sh if bash not available
docker-compose exec pubquiz-app sh
```

### Database Inspection

```powershell
# Access SQLite database in container
docker-compose exec pubquiz-app sqlite3 /app/data/Pubquiz.sqlite

# List tables
sqlite> .tables

# Exit
sqlite> .exit
```

## Security Notes

⚠️ **Current Setup (Development)**
- HTTP only (no HTTPS)
- Debug logging enabled
- SQLite (file-based database)
- Self-contained, no external services

✅ **For Production:**
- Implement HTTPS with real certificates (Let's Encrypt)
- Use environment-based secrets
- Consider SQL Server instead of SQLite
- Implement reverse proxy (Nginx, Traefik)
- Use health checks and restart policies
- Implement log aggregation
- Use private networks for sensitive services

## Volume Management

### View Volume Details

```powershell
docker volume inspect pubquiz-data
docker volume inspect pubquiz-keys
```

### Backup Data

```powershell
# Copy database from volume
docker cp pubquiz-platform:/app/data/Pubquiz.sqlite ./backup/

# Backup keys (important!)
docker cp pubquiz-platform:/app/keys ./backup/keys/
```

### Clear Data (Reset Database)

```powershell
# Remove volumes - THIS DELETES ALL DATA
docker-compose down -v

# Restart to create fresh database
docker-compose up
```

## Performance Tips

- Use `docker-compose up -d` to run in background
- Use `docker-compose ps` to check container status
- Monitor logs to identify performance issues
- Use `--no-cache` flag during builds if layers need updating

## Next Steps

1. Run `docker-compose up` to start the application
2. Access http://localhost:5000
3. Register a new account
4. Follow the 2FA setup process
5. Verify login flow works correctly
6. Test container restart to ensure data persists

## Additional Resources

- [Docker Documentation](https://docs.docker.com/)
- [ASP.NET Core Docker Documentation](https://docs.microsoft.com/en-us/dotnet/core/docker/)
- [SQLite in .NET](https://docs.microsoft.com/en-us/dotnet/standard/data/sqlite/)
