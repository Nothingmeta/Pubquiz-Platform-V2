# 🚀 Quick Start Guide - Pubquiz Platform V2 Docker

## One-Command Setup (Recommended)

Open PowerShell in the project root and run:

```powershell
docker-compose up -d
```

The application will be running at **http://localhost:5000** 🎉

## Using Management Scripts

### Windows Batch (Simplest)

```cmd
# Double-click or run from Command Prompt:
docker-manage.bat up        # Start in background
docker-manage.bat logs      # View logs
docker-manage.bat status    # Check health
docker-manage.bat down      # Stop
```

### PowerShell (More Features)

```powershell
# Start
.\docker-manage.ps1 -Command up

# View logs
.\docker-manage.ps1 -Command logs

# Check status
.\docker-manage.ps1 -Command status

# Backup data
.\docker-manage.ps1 -Command backup
```

## Manual Docker Commands

```powershell
# Build image (first time only, or after code changes)
docker-compose build

# Start application
docker-compose up

# Start in background
docker-compose up -d

# View logs
docker-compose logs -f

# Stop
docker-compose down

# Stop and remove volumes (DELETES ALL DATA)
docker-compose down -v
```

## Testing the Setup

### 1. **Verify Container is Running**
```powershell
docker-compose ps
# Should show pubquiz-app with "healthy" status
```

### 2. **Access Application**
- Open browser to: http://localhost:5000

### 3. **Test Database**
```powershell
docker-compose exec pubquiz-app ls -la /app/data
# Should show Pubquiz.sqlite file
```

### 4. **Test Data Protection Keys**
```powershell
docker-compose exec pubquiz-app ls -la /app/keys
# Should show key files for 2FA
```

### 5. **Test Complete Login Flow**

1. Register new account at http://localhost:5000/Auth/Register
2. Login at http://localhost:5000/Auth/Login
3. Setup 2FA (scan QR code or use recovery codes)
4. Logout and login again
5. Verify 2FA code works

### 6. **Test Data Persistence**

1. Login and create some data (quiz, etc.)
2. Stop container: `docker-compose down`
3. Start again: `docker-compose up -d`
4. Data should still be there! ✓

## Troubleshooting

### "Port 5000 in use"
```powershell
# Kill process using port 5000 (Windows):
netstat -ano | findstr :5000
taskkill /PID <PID> /F

# Or change port in docker-compose.yml:
# ports:
#   - "5001:5000"   # Use 5001 instead
```

### "Database locked" errors
```powershell
# SQLite sometimes locks. Restart container:
docker-compose restart pubquiz-app
```

### "2FA codes not working"
```powershell
# Verify keys volume exists and persists:
docker volume inspect pubquiz-keys

# If missing, data protection keys were lost.
# Backup keys before stopping containers:
docker cp pubquiz-platform:/app/keys ./backup/
```

### "Health check failing"
```powershell
# View full logs:
docker-compose logs pubquiz-app

# Exec into container to diagnose:
docker-compose exec pubquiz-app /bin/bash
# Then check /app/data and /app/keys permissions
```

## What Was Configured

✅ **Program.cs Updates**
- Data protection keys persist to `/app/keys`
- Database migrates automatically on startup
- Authentication cookies properly configured
- Antiforgery protection enabled

✅ **Docker Configuration**
- Multi-stage build (optimized image size)
- Health checks enabled
- Automatic database migrations
- Port 5000 (HTTP)

✅ **Volumes**
- `pubquiz-data` - SQLite database persistence
- `pubquiz-keys` - Encryption keys for 2FA (CRITICAL!)

✅ **Environment**
- Development environment enabled
- Logging configured
- Connection string points to `/app/data/Pubquiz.sqlite`

## Important Notes

⚠️ **Backup Your Keys!**
```powershell
docker-compose exec pubquiz-app ls -la /app/keys
# Save these files if they exist!
```

The data protection keys are critical. If lost:
- 2FA secrets cannot be decrypted
- Users cannot login with 2FA enabled
- Recovery codes won't work

Always backup before major operations:
```powershell
docker-manage.ps1 -Command backup
# or
docker-manage.bat backup
```

## Next Steps

1. ✅ Run `docker-compose up -d` to start
2. ✅ Test login flow with 2FA
3. ✅ Create a backup of keys once 2FA is working
4. ✅ Test container restart to verify persistence
5. ✅ Review logs for any errors: `docker-compose logs`

## Performance

Expected startup time: **30-40 seconds**
- First access may take longer (EF migrations)
- Subsequent starts faster

## Need Help?

Check the logs:
```powershell
docker-compose logs -f pubquiz-app
```

View container details:
```powershell
docker inspect pubquiz-platform
```

Test connectivity:
```powershell
docker-compose exec pubquiz-app curl http://localhost:5000
```

---

**You're ready to go! 🚀**

Start with: `docker-compose up -d`
