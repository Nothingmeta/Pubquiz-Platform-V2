using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity; // Nodig voor PasswordHasher
using Microsoft.AspNetCore.Mvc;
using OtpNet;
using QRCoder;
using Pubquiz_Platform.Data;
using Pubquiz_Platform.Data.Entities;
using Pubquiz_Platform_V2.ViewModels;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PubquizPlatform.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher = new PasswordHasher<User>();
        private readonly IDataProtector _protector;

        // rate-limit parameters
        private const int MaxFailedTwoFactorAttempts = 5;
        private static readonly TimeSpan TwoFactorLockoutDuration = TimeSpan.FromMinutes(10);

        public AuthController(ApplicationDbContext context, IDataProtectionProvider dataProtectionProvider)
        {
            _context = context;
            _protector = dataProtectionProvider.CreateProtector("TwoFactorSecrets_v1");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register() => View();

        [HttpPost]
        [AllowAnonymous]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (_context.Users.Any(u => u.Name == model.Email))
            {
                ModelState.AddModelError("Email", "Dit e-mailadres is al geregistreerd.");
                return View(model);
            }

            // Geen truncatie of beperking op karakters nodig (Unicode toegestaan)

            // Hash wachtwoord
            var user = new User
            {
                Email = model.Email,
                Name = model.Name,
                Role = model.Role
            };
            user.Password = _passwordHasher.HashPassword(user, model.Password);

            _context.Users.Add(user);
            _context.SaveChanges();

            return RedirectToAction("Login");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login() => View();

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (email == null || password == null)
            {
                ViewBag.Error = "Email of Wachtwoord is niet ingevuld.";
                return View();
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                ViewBag.Error = "Ongeldige inloggegevens.";
                return View();
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.Password, password);
            if (result == PasswordVerificationResult.Failed)
            {
                ViewBag.Error = "Ongeldige inloggegevens.";
                return View();
            }

            // If user has 2FA enabled, redirect to code input
            if (user.IsTwoFactorEnabled)
            {
                TempData["2fa_userid"] = user.UserId.ToString();
                TempData.Keep("2fa_userid");
                return RedirectToAction(nameof(TwoFactor));
            }

            // If 2FA is NOT enabled, require the user to enable it now.
            // Store pre-authenticated user id in TempData so EnableTwoFactor can use it.
            TempData["preauth_userid"] = user.UserId.ToString();
            TempData.Keep("preauth_userid");
            return RedirectToAction(nameof(EnableTwoFactor));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult TwoFactor()
        {
            if (!TempData.ContainsKey("2fa_userid"))
            {
                return RedirectToAction("Login");
            }

            // keep for next POST
            TempData.Keep("2fa_userid");
            var vm = new TwoFactorVerifyViewModel();
            return View(vm);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> TwoFactor(TwoFactorVerifyViewModel model)
        {
            if (!TempData.ContainsKey("2fa_userid"))
            {
                return RedirectToAction("Login");
            }

            var idStr = TempData["2fa_userid"]!.ToString();
            if (!int.TryParse(idStr, out var userId)) return RedirectToAction("Login");

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null) return RedirectToAction("Login");

            // Check lockout
            if (user.TwoFactorLockoutEnd.HasValue && user.TwoFactorLockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                ViewBag.Error = $"Account locked. Try again at {user.TwoFactorLockoutEnd.Value:u}.";
                TempData.Keep("2fa_userid");
                return View(model);
            }

            // First try TOTP if secret exists
            if (!string.IsNullOrEmpty(user.ProtectedTwoFactorSecret))
            {
                try
                {
                    var secret = _protector.Unprotect(user.ProtectedTwoFactorSecret);
                    var bytes = Base32Encoding.ToBytes(secret);
                    var totp = new Totp(bytes);
                    var sanitized = (model.Code ?? string.Empty).Replace(" ", "").Replace("-", "");
                    bool isValid = totp.VerifyTotp(sanitized, out long timeStepMatched, new VerificationWindow(2, 2));

                    if (isValid)
                    {
                        // success -> reset counters and sign in
                        user.TwoFactorFailedCount = 0;
                        user.TwoFactorLockoutEnd = null;
                        _context.SaveChanges();

                        await SignInUser(user);
                        return RedirectToAction("Index", "Home");
                    }
                }
                catch
                {
                    // fallthrough to recovery code check
                }
            }

            // Recovery codes check (one-time use)
            if (!string.IsNullOrEmpty(user.ProtectedRecoveryCodes))
            {
                try
                {
                    var recovered = _protector.Unprotect(user.ProtectedRecoveryCodes);
                    var codes = recovered.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    var sanitized = (model.Code ?? string.Empty).Replace(" ", "").Replace("-", "");
                    var matched = codes.FirstOrDefault(c => string.Equals(c, sanitized, StringComparison.OrdinalIgnoreCase));
                    if (matched != null)
                    {
                        // consume code
                        codes.Remove(matched);
                        user.ProtectedRecoveryCodes = codes.Any() ? _protector.Protect(string.Join(",", codes)) : null;
                        user.TwoFactorFailedCount = 0;
                        user.TwoFactorLockoutEnd = null;
                        _context.SaveChanges();

                        await SignInUser(user);
                        return RedirectToAction("Index", "Home");
                    }
                }
                catch
                {
                    // ignore and treat as failed
                }
            }

            // failed attempt -> increment and maybe lockout
            user.TwoFactorFailedCount++;
            if (user.TwoFactorFailedCount >= MaxFailedTwoFactorAttempts)
            {
                user.TwoFactorLockoutEnd = DateTimeOffset.UtcNow.Add(TwoFactorLockoutDuration);
                user.TwoFactorFailedCount = 0; // reset counter after lockout
            }
            _context.SaveChanges();

            ViewBag.Error = "Invalid code.";
            TempData.Keep("2fa_userid");
            return View(model);
        }

        [HttpGet]
        public IActionResult EnableTwoFactor()
        {
            // Support two entry points:
            //  - already signed in user (User claim present)
            //  - pre-authenticated user coming from Login (TempData["preauth_userid"])
            string? userIdStr = null;

            if (User?.Identity?.IsAuthenticated == true)
            {
                userIdStr = User.FindFirst("UserId")?.Value;
            }
            else if (TempData.ContainsKey("preauth_userid"))
            {
                userIdStr = TempData["preauth_userid"]!.ToString();
                // keep so POST can still read it
                TempData.Keep("preauth_userid");
            }
            else
            {
                return Forbid();
            }

            if (!int.TryParse(userIdStr, out var userId)) return Forbid();

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null) return Forbid();

            // Generate secret and QR
            var secretBytes = KeyGeneration.GenerateRandomKey(20);
            var secretBase32 = Base32Encoding.ToString(secretBytes);

            var issuer = Uri.EscapeDataString("PubquizPlatform");
            var label = Uri.EscapeDataString(user.Email);
            var otpauth = $"otpauth://totp/{issuer}:{label}?secret={secretBase32}&issuer={issuer}&digits=6";

            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(otpauth, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrData);
            var qrBytes = qrCode.GetGraphic(20);
            var dataUri = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}";

            var vm = new EnableTwoFactorViewModel
            {
                QrCodeDataUri = dataUri,
                ManualEntryKey = secretBase32
            };

            // Temporarily store the plain secret in TempData for verification on POST
            TempData["2fa_temp_secret"] = secretBase32;
            TempData.Keep("2fa_temp_secret");

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> EnableTwoFactor(string code)
        {
            // Determine target user: either authenticated user or preauth via TempData
            string? userIdStr = null;
            bool cameFromPreauth = false;

            if (User?.Identity?.IsAuthenticated == true)
            {
                userIdStr = User.FindFirst("UserId")?.Value;
            }
            else if (TempData.ContainsKey("preauth_userid"))
            {
                userIdStr = TempData["preauth_userid"]!.ToString();
                cameFromPreauth = true;
            }
            else
            {
                return Forbid();
            }

            if (!int.TryParse(userIdStr, out var userId)) return Forbid();

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null) return Forbid();

            if (!TempData.ContainsKey("2fa_temp_secret"))
            {
                ModelState.AddModelError("", "Setup session expired. Start again.");
                return RedirectToAction(nameof(EnableTwoFactor));
            }

            var secret = TempData["2fa_temp_secret"]!.ToString()!;
            var bytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(bytes);
            var sanitized = (code ?? string.Empty).Replace(" ", "").Replace("-", "");
            bool isValid = totp.VerifyTotp(sanitized, out long _, new VerificationWindow(2, 2));

            if (!isValid)
            {
                ModelState.AddModelError("", "Invalid code. Scan again or re-enter code.");
                TempData.Keep("2fa_temp_secret");
                if (cameFromPreauth) TempData.Keep("preauth_userid");
                return RedirectToAction(nameof(EnableTwoFactor));
            }

            // Protect and store secret
            user.ProtectedTwoFactorSecret = _protector.Protect(secret);
            user.IsTwoFactorEnabled = true;

            // Generate recovery codes (one-time display)
            var recoveryCodes = GenerateRecoveryCodes(8);
            user.ProtectedRecoveryCodes = _protector.Protect(string.Join(",", recoveryCodes));

            _context.SaveChanges();

            // If user came from login (preauth), sign them in now:
            if (cameFromPreauth)
            {
                await SignInUser(user);
            }

            // pass recovery codes to view via TempData so they are shown once
            TempData["recovery_codes"] = JsonSerializer.Serialize(recoveryCodes);

            return RedirectToAction(nameof(ShowRecoveryCodes));
        }

        [HttpGet]
        public IActionResult ShowRecoveryCodes()
        {
            if (!TempData.ContainsKey("recovery_codes"))
            {
                return RedirectToAction("Index", "Home");
            }

            var serialized = TempData["recovery_codes"]!.ToString();
            var codes = JsonSerializer.Deserialize<List<string>>(serialized!) ?? new List<string>();
            return View(codes);
        }

        private async Task SignInUser(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("UserId", user.UserId.ToString())
            };

            var identity = new ClaimsIdentity(claims, "PubquizCookie");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("PubquizCookie", principal);
        }

        private static List<string> GenerateRecoveryCodes(int count)
        {
            var codes = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                codes.Add(GenerateSingleRecoveryCode());
            }
            return codes;
        }

        private static string GenerateSingleRecoveryCode()
        {
            // produce a short human-friendly code like "4F7G-9K2P"
            Span<byte> bytes = stackalloc byte[6];
            RandomNumberGenerator.Fill(bytes);
            // base32-ish text: use hex but uppercase and insert dash
            var s = Convert.ToBase64String(bytes).Replace('+', 'A').Replace('/', 'B').Replace("=", string.Empty);
            s = new string(s.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (s.Length < 8) s = s.PadRight(8, 'X');
            return s.Substring(0, 4) + "-" + s.Substring(4, 4);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("PubquizCookie");
            return RedirectToAction("Login");
        }
    }
}