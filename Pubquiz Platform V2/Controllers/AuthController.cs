using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using OtpNet;
using QRCoder;
using Pubquiz_Platform.Data;
using Pubquiz_Platform.Data.Entities;
using Pubquiz_Platform_V2.Services;
using Pubquiz_Platform_V2.ViewModels;

namespace PubquizPlatform.Controllers
{
    [Authorize]
    [Route("Auth/[action]")]
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher = new PasswordHasher<User>();
        private readonly ISecretCryptoService _crypto;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IConfiguration _configuration;

        private const int MaxFailedTwoFactorAttempts = 5;
        private static readonly TimeSpan TwoFactorLockoutDuration = TimeSpan.FromMinutes(10);

        public AuthController(
            ApplicationDbContext context,
            ISecretCryptoService crypto,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService refreshTokenService,
            IConfiguration configuration)
        {
            _context = context;
            _crypto = crypto;
            _jwtTokenService = jwtTokenService;
            _refreshTokenService = refreshTokenService;
            _configuration = configuration;
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

            if (_context.Users.Any(u => u.Email == model.Email))
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

            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login() => View();

        [HttpPost]
        [AllowAnonymous]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Email) || string.IsNullOrWhiteSpace(request?.Password))
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

            var preAuthToken = _jwtTokenService.GeneratePreAuthToken(user.UserId);
            SetPreAuthCookie(preAuthToken);

            return Json(new AuthResponseViewModel
            {
                Success = true,
                Message = user.IsTwoFactorEnabled ? "2FA required" : "2FA setup required",
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
            if (!TryGetPreAuthUser(model?.PreAuthToken, out var user, out var errorMessage) || user == null)
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = errorMessage ?? "Pre-auth token missing"
                });
            }

            if (user.TwoFactorLockoutEnd.HasValue && user.TwoFactorLockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = $"Account locked. Try again at {user.TwoFactorLockoutEnd.Value:u}."
                });
            }

            if (!string.IsNullOrEmpty(user.ProtectedTwoFactorSecret))
            {
                try
                {
                    var secret = _crypto.Decrypt(user.ProtectedTwoFactorSecret);
                    var bytes = Base32Encoding.ToBytes(secret);
                    var totp = new Totp(bytes);
                    var sanitized = (model?.Code ?? string.Empty).Replace(" ", "").Replace("-", "");

                    bool totpValid = totp.VerifyTotp(sanitized, out _, new VerificationWindow(6, 6));

                    if (totpValid)
                    {
                        user.TwoFactorFailedCount = 0;
                        user.TwoFactorLockoutEnd = null;
                        _context.SaveChanges();

                        IssueAuthCookies(user);
                        Response.Cookies.Delete("preAuthToken", new CookieOptions { Path = "/" });

                        return Json(new AuthResponseViewModel
                        {
                            Success = true,
                            Message = "Login successful"
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[2FA DEBUG] TOTP verification failed for UserId {user.UserId}: {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(user.ProtectedRecoveryCodes))
            {
                try
                {
                    var recovered = _crypto.Decrypt(user.ProtectedRecoveryCodes);
                    var codes = recovered.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    var sanitized = (model?.Code ?? string.Empty).Replace(" ", "").Replace("-", "");
                    var matched = codes.FirstOrDefault(c => string.Equals(c, sanitized, StringComparison.OrdinalIgnoreCase));

                    if (matched != null)
                    {
                        codes.Remove(matched);
                        user.ProtectedRecoveryCodes = codes.Any() ? _crypto.Encrypt(string.Join(",", codes)) : null;
                        user.TwoFactorFailedCount = 0;
                        user.TwoFactorLockoutEnd = null;
                        _context.SaveChanges();

                        IssueAuthCookies(user);
                        Response.Cookies.Delete("preAuthToken", new CookieOptions { Path = "/" });

                        return Json(new AuthResponseViewModel
                        {
                            Success = true,
                            Message = "Login successful"
                        });
                    }
                }
                catch
                {
                    // ignore
                }
            }

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

        [AllowAnonymous]
        [HttpGet]
        public IActionResult EnableTwoFactor()
        {
            if (!TryGetPreAuthUser(null, out var user, out _))
            {
                return RedirectToAction(nameof(Login));
            }

            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

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
            if (!TryGetPreAuthUser(null, out var user, out var errorMessage) || user == null)
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = errorMessage ?? "Pre-auth token missing"
                });
            }

            if (string.IsNullOrWhiteSpace(model?.Secret))
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
            bool totpValid = totp.VerifyTotp(sanitized, out _, new VerificationWindow(2, 2));

            if (!totpValid)
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = "Invalid code"
                });
            }

            user.ProtectedTwoFactorSecret = _crypto.Encrypt(model.Secret);
            user.IsTwoFactorEnabled = true;

            var recoveryCodes = GenerateRecoveryCodes(8);
            user.ProtectedRecoveryCodes = _crypto.Encrypt(string.Join(",", recoveryCodes));
            _context.SaveChanges();

            IssueAuthCookies(user);
            Response.Cookies.Delete("preAuthToken", new CookieOptions { Path = "/" });

            return Json(new TwoFactorResponseViewModel
            {
                Success = true,
                Message = "2FA enabled successfully",
                RecoveryCodes = recoveryCodes
            });
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult RefreshToken([FromBody] RefreshTokenRequest? request)
        {
            var refreshToken = request?.RefreshToken;

            if (string.IsNullOrWhiteSpace(refreshToken) &&
                Request.Cookies.TryGetValue("refresh_token", out var cookieRefreshToken))
            {
                refreshToken = cookieRefreshToken;
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = "Refresh token is required"
                });
            }

            var (isValid, userId) = _refreshTokenService.ValidateRefreshToken(refreshToken);
            if (!isValid)
            {
                return Json(new AuthResponseViewModel
                {
                    Success = false,
                    Message = "Invalid or expired refresh token"
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

            IssueAuthCookies(user);

            return Json(new AuthResponseViewModel
            {
                Success = true,
                Message = "Token refreshed successfully"
            });
        }

        [HttpPost]
        [Authorize]
        public IActionResult Logout()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                _refreshTokenService.RevokeRefreshToken(userId);
            }

            ClearAuthCookies();
            return RedirectToAction(nameof(Login));
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

        private bool TryGetPreAuthUser(string? providedToken, out User? user, out string? errorMessage)
        {
            user = null;
            errorMessage = null;

            var preAuthToken = providedToken;

            if (string.IsNullOrWhiteSpace(preAuthToken) &&
                !Request.Cookies.TryGetValue("preAuthToken", out preAuthToken))
            {
                errorMessage = "Pre-auth token missing";
                return false;
            }

            var (isValid, userId) = _jwtTokenService.ValidatePreAuthToken(preAuthToken!);
            if (!isValid)
            {
                errorMessage = "Invalid or expired pre-auth token";
                return false;
            }

            user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                errorMessage = "User not found";
                return false;
            }

            return true;
        }

        private CookieOptions BuildAuthCookieOptions(TimeSpan lifetime) => new()
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.Add(lifetime)
        };

        private void SetPreAuthCookie(string token)
        {
            Response.Cookies.Append("preAuthToken", token, BuildAuthCookieOptions(TimeSpan.FromMinutes(5)));
        }

        private void IssueAuthCookies(User user)
        {
            var accessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Email, user.Name, user.Role);
            var refreshToken = _refreshTokenService.GenerateRefreshToken();
            var refreshExpiry = DateTime.UtcNow.AddDays(int.Parse(_configuration["Jwt:RefreshTokenExpirationDays"] ?? "7"));

            _refreshTokenService.SaveRefreshToken(user.UserId, refreshToken, refreshExpiry);
            SetAuthCookies(accessToken, refreshToken);
        }

        private void SetAuthCookies(string accessToken, string refreshToken)
        {
            var accessMinutes = int.Parse(_configuration["Jwt:AccessTokenExpirationMinutes"] ?? "15");
            var refreshDays = int.Parse(_configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");

            Response.Cookies.Append("auth_token", accessToken, BuildAuthCookieOptions(TimeSpan.FromMinutes(accessMinutes)));
            Response.Cookies.Append("refresh_token", refreshToken, BuildAuthCookieOptions(TimeSpan.FromDays(refreshDays)));
        }

        private void ClearAuthCookies()
        {
            var cookieOptions = new CookieOptions { Path = "/" };
            Response.Cookies.Delete("auth_token", cookieOptions);
            Response.Cookies.Delete("refresh_token", cookieOptions);
            Response.Cookies.Delete("preAuthToken", cookieOptions);
        }
    }
}