# Quick Commands Reference

## Start Using Docker Now

### Option 1: Batch File (Windows Command Prompt)
```cmd
cd C:\Users\the_s\source\repos\Pubquiz Platform V2
docker-manage.bat up
```

Then open: **http://localhost:5000**

### Option 2: PowerShell
```powershell
cd 'C:\Users\the_s\source\repos\Pubquiz Platform V2'
.\docker-manage.ps1 -Command up
```

Then open: **http://localhost:5000**

### Option 3: Docker Compose (Direct)
```powershell
cd 'C:\Users\the_s\source\repos\Pubquiz Platform V2'
docker-compose up -d
```

Then open: **http://localhost:5000**

---

## Common Commands

| Task | Command |
|------|---------|
| **Start** | `docker-manage.bat up` or `docker-compose up -d` |
| **View Logs** | `docker-manage.bat logs` or `docker-compose logs -f` |
| **Stop** | `docker-manage.bat down` or `docker-compose down` |
| **Restart** | `docker-manage.bat restart` or `docker-compose restart` |
| **Check Health** | `docker-manage.bat status` or `docker-compose ps` |
| **Backup Data** | `docker-manage.bat backup` or `docker-manage.ps1 -Command backup` |
| **Shell Access** | `docker-manage.ps1 -Command shell` |

---

## What Happens After Starting

1. **Container builds** (first time only, ~2 min)
2. **Volumes created** for data and encryption keys
3. **Database initialized** (automatic migrations)
4. **Application starts** on http://localhost:5000
5. **Ready to use** in 30-40 seconds

---

## Test Steps (After Starting)

1. ✅ Open http://localhost:5000
2. ✅ Click "Register"
3. ✅ Create account
4. ✅ Follow 2FA setup
5. ✅ Scan QR code
6. ✅ Enter TOTP code
7. ✅ Done!

---

## If Something Goes Wrong

```powershell
# View logs
docker-compose logs -f pubquiz-app

# Check container status
docker-compose ps

# Restart
docker-compose restart

# Reset everything (careful - deletes data)
docker-compose down -v
docker-compose up -d
```

---

## Files You Need to Know About

- **`Dockerfile`** - How the image is built
- **`docker-compose.yml`** - Container configuration
- **`DOCKER_QUICKSTART.md`** - Fast start guide
- **`DOCKER_README.md`** - Full documentation
- **`VERIFICATION_CHECKLIST.md`** - Testing guide
- **`docker-manage.bat`** - Windows command tool
- **`docker-manage.ps1`** - PowerShell tool

---

## Success Indicators

✅ You're good when:
- Container shows "healthy" in `docker-compose ps`
- Database file exists: `/app/data/Pubquiz.sqlite`
- Can login with 2FA
- Data persists after restart

---

## First Time? Start Here

```bash
# 1. Build and start
docker-compose up -d

# 2. Wait 30-40 seconds for healthy
docker-compose ps

# 3. Open browser
# http://localhost:5000

# 4. Register and setup 2FA

# 5. Test by restarting
docker-compose down
docker-compose up -d

# 6. Login again - data should still be there!
```

---

**Ready? Run: `docker-compose up -d` 🚀**
