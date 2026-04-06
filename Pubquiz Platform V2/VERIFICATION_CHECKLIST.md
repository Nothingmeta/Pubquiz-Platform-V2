# Docker Implementation Verification Checklist

Use this checklist to verify the Docker setup is working correctly.

## Pre-Deployment Verification

### File Structure
- [ ] `Dockerfile` exists in root
- [ ] `docker-compose.yml` exists in root
- [ ] `appsettings.Development.json` configured for volumes
- [ ] `.dockerignore` updated
- [ ] `Program.cs` has data protection and migration code
- [ ] `docker-manage.ps1` exists (PowerShell)
- [ ] `docker-manage.bat` exists (Batch)

### Build Verification
```powershell
# Run this command
dotnet build

# Expected: Build successful
```
- [ ] Build completes without errors
- [ ] No compilation errors in Program.cs
- [ ] No warnings about data protection

## Initial Deployment

### 1. Build Docker Image
```powershell
docker-compose build
```

Expected output:
```
Building pubquiz-app
Step 1/20 : FROM mcr.microsoft.com/dotnet/sdk:8.0
...
Step 20/20 : CMD ["--urls", "http://+:5000"]
Successfully built [image hash]
```

- [ ] Build completes successfully
- [ ] No dependency resolution errors
- [ ] Image is created

### 2. Start Container
```powershell
docker-compose up -d
```

Expected output:
```
Creating pubquiz-db-setup ... done
Creating pubquiz-platform ... done
```

- [ ] Both services start
- [ ] No immediate errors
- [ ] Container is running

### 3. Check Container Status
```powershell
docker-compose ps
```

Expected output:
```
NAME                 STATUS              PORTS
pubquiz-db-setup     Exited (0)          (exit code 0)
pubquiz-platform     Up 20s (healthy)    0.0.0.0:5000->5000/tcp
```

- [ ] pubquiz-platform shows "healthy"
- [ ] Port 5000 is mapped
- [ ] Exit code is 0 (success)

### 4. Verify Database File Created
```powershell
docker-compose exec pubquiz-app ls -la /app/data/
```

Expected output:
```
total 64
drwxr-xr-x  2 root root  4096 Apr  5 12:34 .
drwxr-xr-x  1 root root  4096 Apr  5 12:34 ..
-rw-r--r--  1 root root 61440 Apr  5 12:34 Pubquiz.sqlite
```

- [ ] `Pubquiz.sqlite` file exists
- [ ] File size > 0
- [ ] Owned by root (or appropriate user)

### 5. Verify Data Protection Keys
```powershell
docker-compose exec pubquiz-app ls -la /app/keys/
```

Expected output:
```
total 28
drwxr-xr-x  2 root root  4096 Apr  5 12:34 .
drwxr-xr-x  1 root root  4096 Apr  5 12:34 ..
-rw-r--r--  1 root root  2048 Apr  5 12:34 key-*.xml
```

- [ ] At least one `.xml` key file exists
- [ ] Files are readable
- [ ] Directory is writable

## Application Testing

### 6. Access Application
- [ ] Open browser to `http://localhost:5000`
- [ ] Page loads without errors
- [ ] CSS/JS files load (check DevTools)
- [ ] No "Connection refused" errors

### 7. Register Account
```
URL: http://localhost:5000/Auth/Register
```

- [ ] Registration form appears
- [ ] Fields visible (Email, Name, Password, Role)
- [ ] Form submits without errors
- [ ] Redirected to login page
- [ ] No database errors in logs

**Verify:**
```powershell
docker-compose logs pubquiz-app | Select-String -Pattern "ERROR|Exception" -NotMatch
```

### 8. Login Without 2FA
```
URL: http://localhost:5000/Auth/Login
```

- [ ] Login form appears
- [ ] Enter registered email and password
- [ ] Submit form
- [ ] **Should redirect to EnableTwoFactor**
- [ ] 2FA setup page shows QR code
- [ ] No error messages

### 9. Enable 2FA
- [ ] Scan QR code with authenticator app (Google Authenticator, Authy)
- [ ] Copy 6-digit code from app
- [ ] Enter code in browser
- [ ] Form submits
- [ ] **Should show recovery codes**
- [ ] Save recovery codes
- [ ] Click "I've saved the codes"
- [ ] Should redirect to dashboard/home page

### 10. Test 2FA Login
1. Logout from application
2. Login again with same email/password
3. Should redirect to 2FA code prompt
4. Enter 6-digit code from authenticator
5. Should successfully log in

- [ ] 2FA prompt appears
- [ ] Code validation works
- [ ] Login succeeds with correct code
- [ ] Login fails with incorrect code (try 3 times)
- [ ] After 5 failures, lockout is enforced

### 11. Test Recovery Codes
1. Logout again
2. Login with credentials
3. When asked for 2FA code, enter recovery code instead
4. Should log in successfully
5. Recovery code should be consumed (can't reuse)

- [ ] Recovery code prompt accepts code
- [ ] Code format recognized (with/without spaces/dashes)
- [ ] Code validates successfully
- [ ] Second login prompts for code again (code consumed)

## Data Persistence Testing

### 12. Container Restart Test

**Step 1: Create test data**
- [ ] Logged in as user
- [ ] Create a quiz (if applicable)
- [ ] Note the quiz ID or name

**Step 2: Stop container**
```powershell
docker-compose down
```

- [ ] Container stops cleanly
- [ ] No error messages
- [ ] Volumes remain (not deleted)

**Step 3: Verify volumes exist**
```powershell
docker volume ls | findstr pubquiz
```

Expected output:
```
pubquiz-data
pubquiz-keys
```

- [ ] Both volumes listed
- [ ] Size > 0 for pubquiz-data

**Step 4: Restart container**
```powershell
docker-compose up -d
```

- [ ] Container starts successfully
- [ ] Becomes healthy (30-40 seconds)

**Step 5: Verify data persistence**
```powershell
docker-compose exec pubquiz-app ls -la /app/data/Pubquiz.sqlite
```

- [ ] Database file still exists
- [ ] File size unchanged or larger

**Step 6: Test login**
- [ ] Access http://localhost:5000
- [ ] Login with same credentials
- [ ] 2FA prompt appears (keys were persistent!)
- [ ] Enter 2FA code
- [ ] Successfully logged in
- [ ] Previous quiz still exists

- [ ] ✅ Data persisted across restart!

### 13. Volume Inspection
```powershell
docker volume inspect pubquiz-data
```

Expected output contains:
```json
{
    "Name": "pubquiz-data",
    "Driver": "local",
    "Mountpoint": "C:\\ProgramData\\Docker\\volumes\\pubquiz-data\\_data",
    ...
}
```

- [ ] Volume driver is "local"
- [ ] Mountpoint exists on host
- [ ] Can access files via host filesystem (Windows Explorer)

## Security Verification

### 14. Cookie Verification
1. Open DevTools (F12)
2. Go to Application → Cookies
3. Click "localhost:5000"

Expected cookies:
```
Name: .PubquizCookie
Domain: localhost
Path: /
HttpOnly: ✓ (checked)
Secure: false (HTTP, so disabled)
SameSite: Lax
```

- [ ] `.PubquizCookie` present
- [ ] `HttpOnly` is enabled ✓
- [ ] `SameSite` is set to `Lax`
- [ ] `Secure` is not required (HTTP only)

### 15. Antiforgery Token Test
1. Logout
2. Login again
3. Try to submit any form (create quiz, update profile, etc.)
4. Form should submit without antiforgery errors

- [ ] Forms submit without token errors
- [ ] No "Request validation failed" messages
- [ ] Request anti-forgery token errors absent

### 16. HTTPS Redirect Test
```powershell
# In Development mode, HTTPS redirect should be disabled
# This is correct for Docker HTTP-only setup

# Verify by checking logs
docker-compose logs pubquiz-app | Select-String "HSTS|https"
```

- [ ] No HTTPS redirect in development
- [ ] No HTTP to HTTPS redirect messages
- [ ] Works fine with HTTP only

## Logs and Monitoring

### 17. Check Application Logs
```powershell
docker-compose logs pubquiz-app
```

Look for:
- [ ] No ERROR messages
- [ ] No Exception stacktraces
- [ ] EF Core migrations applied successfully
- [ ] Database connection successful
- [ ] Application listening on port 5000

### 18. Health Check
```powershell
docker-compose ps
```

- [ ] `pubquiz-app` shows `(healthy)` status
- [ ] If unhealthy, check: `docker-compose logs pubquiz-app`

### 19. Monitor Ongoing Operation
```powershell
# Real-time logs
docker-compose logs -f pubquiz-app

# Press Ctrl+C to exit
```

- [ ] Logs show normal operation
- [ ] No repeated errors
- [ ] No memory issues (look for OutOfMemory)

## Performance Testing

### 20. Startup Time
```powershell
# Note start time from compose output
docker-compose up -d

# Wait for healthy
docker-compose ps
# Measure time until (healthy) appears
```

- [ ] Startup < 60 seconds
- [ ] Typically 30-40 seconds
- [ ] Health check passes

### 21. Request Response Time
1. Open DevTools (F12)
2. Go to Network tab
3. Login
4. Check request times

- [ ] Initial page load < 2 seconds
- [ ] 2FA validation < 1 second
- [ ] No timeout errors

## Database-Specific Tests

### 22. SQLite Integrity
```powershell
docker-compose exec pubquiz-app sqlite3 /app/data/Pubquiz.sqlite ".tables"
```

Expected output shows tables:
- Users
- Quizzes
- Questions
- Lobbies

- [ ] All tables present
- [ ] No corruption messages
- [ ] Can query data

### 23. Data Integrity
```powershell
docker-compose exec pubquiz-app sqlite3 /app/data/Pubquiz.sqlite "SELECT COUNT(*) FROM Users;"
```

- [ ] Returns user count
- [ ] Should match registered users
- [ ] No SQLite errors

## Cleanup and Final Verification

### 24. Graceful Shutdown
```powershell
docker-compose down
```

- [ ] Both services stop cleanly
- [ ] No error messages
- [ ] Exit code 0

### 25. Clean Restart
```powershell
docker-compose up -d
```

- [ ] Container starts with persisted data
- [ ] All previous data intact
- [ ] 2FA still works

## Final Checklist

| Test | Status | Notes |
|------|--------|-------|
| Build completes | ☐ | |
| Container starts | ☐ | |
| Database created | ☐ | |
| Keys directory created | ☐ | |
| Application accessible | ☐ | |
| Registration works | ☐ | |
| Login works | ☐ | |
| 2FA setup works | ☐ | |
| 2FA authentication works | ☐ | |
| Recovery codes work | ☐ | |
| Data persists restart | ☐ | |
| Cookies configured | ☐ | |
| No antiforgery errors | ☐ | |
| Logs clean | ☐ | |
| Health checks pass | ☐ | |
| Database integrity | ☐ | |

## Success Criteria

✅ **All green when:**
- Container runs without errors
- Database and keys persist
- Login and 2FA work seamlessly
- Data survives container restart
- No security warnings

## Troubleshooting Reference

If any check fails, refer to:
- `DOCKER_README.md` - Comprehensive troubleshooting
- `IMPLEMENTATION_SUMMARY.md` - Technical details
- Application logs: `docker-compose logs pubquiz-app`

---

**Review this checklist before considering the setup complete!**
