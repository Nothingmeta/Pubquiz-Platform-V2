using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OtpNet;
using QRCoder;
using Pubquiz_Platform.Data;
using Pubquiz_Platform.Data.Entities;
using Pubquiz_Platform_V2.ViewModels;
using Pubquiz_Platform_V2.Services;
using System.Security.Cryptography;

namespace PubquizPlatform.Controllers
{
    [Route("Auth/[action]")]  // ← This makes Auth/Login, Auth/Register, etc.
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher = new PasswordHasher<User>();
        private readonly IDataProtector _protector;
        private readonly IJwtTokenService _jwtTokenService;

        private const int MaxFailedTwoFactorAttempts = 5;
        private static readonly TimeSpan TwoFactorLockoutDuration = TimeSpan.FromMinutes(10);

        public AuthController(
            ApplicationDbContext context,
            IDataProtectionProvider dataProtectionProvider,
            IJwtTokenService jwtTokenService)
        {
            _context = context;
            _protector = dataProtectionProvider.CreateProtector("TwoFactorSecrets_v1");
            _jwtTokenService = jwtTokenService;
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
        public IActionResult Login([FromBody] LoginRequest request)  // ← Changed this line
        {
            if (string.IsNullOrEmpty(request?.Email) || string.IsNullOrEmpty(request?.Password))
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = "Email of Wachtwoord is niet ingevuld."
                });
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);
            if (user == null)
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = "Ongeldige inloggegevens."
                });
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.Password, request.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = "Ongeldige inloggegevens."
                });
            }

            // If user has 2FA enabled, return pre-auth token
            if (user.IsTwoFactorEnabled)
            {
                var preAuthToken = _jwtTokenService.GeneratePreAuthToken(user.UserId);
                return Json(new AuthResponseViewModel
                {
                    Success = true,
                    Message = "2FA required",
                    PreAuthToken = preAuthToken,
                    UserId = user.UserId
                });
            }

            // If 2FA not enabled, require user to enable it before full access
            var setupToken = _jwtTokenService.GeneratePreAuthToken(user.UserId);
            return Json(new AuthResponseViewModel
            {
                Success = true,
                Message = "2FA setup required",
                PreAuthToken = setupToken,
                UserId = user.UserId
            });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult TwoFactor() => View();

        [HttpPost]
        [AllowAnonymous]
        public IActionResult TwoFactor([FromBody] TwoFactorVerifyViewModel model)
        {
            if (string.IsNullOrEmpty(model.PreAuthToken))
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = "Pre-auth token missing"
                });
            }

            var (isValid, userId) = _jwtTokenService.ValidatePreAuthToken(model.PreAuthToken);
            if (!isValid)
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = "Invalid or expired pre-auth token"
                });
            }

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            // Check lockout
            if (user.TwoFactorLockoutEnd.HasValue && user.TwoFactorLockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = $"Account locked. Try again at {user.TwoFactorLockoutEnd.Value:u}."
                });
            }

            // Verify TOTP
            if (!string.IsNullOrEmpty(user.ProtectedTwoFactorSecret))
            {
                try
                {
                    var secret = _protector.Unprotect(user.ProtectedTwoFactorSecret);
                    var bytes = Base32Encoding.ToBytes(secret);
                    var totp = new Totp(bytes);
                    var sanitized = (model.Code ?? string.Empty).Replace(" ", "").Replace("-", "");
                    bool totpValid = totp.VerifyTotp(sanitized, out _, new VerificationWindow(2, 2));

                    if (totpValid)
                    {
                        user.TwoFactorFailedCount = 0;
                        user.TwoFactorLockoutEnd = null;
                        _context.SaveChanges();

                        var accessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Email, user.Name, user.Role);
                        return Json(new AuthResponseViewModel
                        {
                            Success = true,
                            Message = "Login successful",
                            AccessToken = accessToken
                        });
                    }
                }
                catch { }
            }

            // Verify Recovery Code
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
                        codes.Remove(matched);
                        user.ProtectedRecoveryCodes = codes.Any() ? _protector.Protect(string.Join(",", codes)) : null;
                        user.TwoFactorFailedCount = 0;
                        user.TwoFactorLockoutEnd = null;
                        _context.SaveChanges();

                        var accessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Email, user.Name, user.Role);
                        return Json(new AuthResponseViewModel
                        {
                            Success = true,
                            Message = "Login successful",
                            AccessToken = accessToken
                        });
                    }
                }
                catch { }
            }

            // Failed attempt
            user.TwoFactorFailedCount++;
            if (user.TwoFactorFailedCount >= MaxFailedTwoFactorAttempts)
            {
                user.TwoFactorLockoutEnd = DateTimeOffset.UtcNow.Add(TwoFactorLockoutDuration);
                user.TwoFactorFailedCount = 0;
            }
            _context.SaveChanges();

            return Json(new AuthResponseViewModel
            {
                Success = false,
                Message = "Invalid code."
            });
        }

        [HttpGet]
        public IActionResult EnableTwoFactor()
        {
            string? userIdStr = User?.FindFirst("UserId")?.Value;
            
            // If not authenticated, check for pre-auth token from cookie
            if (string.IsNullOrEmpty(userIdStr))
            {
                // Check if user has pre-auth token in cookie
                if (!Request.Cookies.TryGetValue("preAuthToken", out var preAuthToken))
                {
                    return Forbid();
                }

                // Validate the pre-auth token
                var (isValid, userId) = _jwtTokenService.ValidatePreAuthToken(preAuthToken);
                if (!isValid)
                {
                    return Forbid();
                }

                userIdStr = userId.ToString();
            }

            if (!int.TryParse(userIdStr, out var parsedUserId)) 
                return Forbid();

            var user = _context.Users.FirstOrDefault(u => u.UserId == parsedUserId);
            if (user == null) 
                return Forbid();

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
                ManualEntryKey = secretBase32,
                TempSecret = secretBase32
            };

            return View(vm);
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult EnableTwoFactor([FromBody] TwoFactorSetupViewModel model)
        {
            string? userIdStr = User?.FindFirst("UserId")?.Value;
            int userId = 0;
            
            // If not authenticated, check for pre-auth token
            if (string.IsNullOrEmpty(userIdStr))
            {
                if (!Request.Cookies.TryGetValue("preAuthToken", out var preAuthToken))
                {
                    return Json(new AuthResponseViewModel
                    {
                        Success = false,
                        Message = "Pre-auth token missing"
                    });
                }

                var (isValid, parsedUserId) = _jwtTokenService.ValidatePreAuthToken(preAuthToken);
                if (!isValid)
                {
                    return Json(new AuthResponseViewModel
                    {
                        Success = false,
                        Message = "Invalid pre-auth token"
                    });
                }

                userId = parsedUserId;
                userIdStr = userId.ToString();
            }
            else
            {
                if (!int.TryParse(userIdStr, out userId))
                {
                    return Json(new AuthResponseViewModel
                    {
                        Success = false,
                        Message = "Invalid user ID"
                    });
                }
            }

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            if (string.IsNullOrEmpty(model.Secret))
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = "Secret not provided"
                });
            }

            var bytes = Base32Encoding.ToBytes(model.Secret);
            var totp = new Totp(bytes);
            var sanitized = (model.Code ?? string.Empty).Replace(" ", "").Replace("-", "");
            bool totpValid = totp.VerifyTotp(sanitized, out _, new VerificationWindow(2, 2));  // ← Changed to totpValid

            if (!totpValid)  // ← Changed to totpValid
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = "Invalid code"
                });
            }

            user.ProtectedTwoFactorSecret = _protector.Protect(model.Secret);
            user.IsTwoFactorEnabled = true;

            var recoveryCodes = GenerateRecoveryCodes(8);
            user.ProtectedRecoveryCodes = _protector.Protect(string.Join(",", recoveryCodes));

            _context.SaveChanges();

            // Generate full access token now that 2FA is set up
            var accessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Email, user.Name, user.Role);

            return Json(new TwoFactorResponseViewModel
            {
                Success = true,
                Message = "2FA enabled successfully",
                AccessToken = accessToken,
                RecoveryCodes = recoveryCodes
            });
        }

        [HttpPost]
        [Authorize]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("auth_token");
            Response.Cookies.Delete("preAuthToken");
            return RedirectToAction("Login");
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
            Span<byte> bytes = stackalloc byte[6];
            RandomNumberGenerator.Fill(bytes);
            var s = Convert.ToBase64String(bytes).Replace('+', 'A').Replace('/', 'B').Replace("=", string.Empty);
            s = new string(s.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (s.Length < 8) s = s.PadRight(8, 'X');
            return s.Substring(0, 4) + "-" + s.Substring(4, 4);
        }
    }
}