using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Pubquiz_Platform.Data;
using Pubquiz_Platform.Data.Entities;
using Pubquiz_Platform_V2.Services;
using Pubquiz_Platform_V2.ViewModels;
using PubquizPlatform.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PubquizTests
{
    public class AuthControllerTests
    {
        private ApplicationDbContext? _context;
        private AuthController? _controller;
        private IJwtTokenService? _jwtTokenService;
        private IRefreshTokenService? _refreshTokenService;
        private ISecretCryptoService? _crypto;
        private IConfiguration? _configuration;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"test_db_{Guid.NewGuid()}")
                .Options;
            _context = new ApplicationDbContext(options);

            var cryptoKey = Convert.ToBase64String(new byte[32]);

            var inMemorySettings = new Dictionary<string, string?>
            {
                { "Jwt:SecretKey", "test-secret-key-at-least-32-characters-long-for-256-bit" },
                { "Jwt:Issuer", "TestIssuer" },
                { "Jwt:Audience", "TestAudience" },
                { "Jwt:AccessTokenExpirationMinutes", "15" },
                { "Jwt:RefreshTokenExpirationDays", "7" },
                { "Crypto:MasterKey", cryptoKey }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var jwtLogger = loggerFactory.CreateLogger<JwtTokenService>();
            var refreshLogger = loggerFactory.CreateLogger<RefreshTokenService>();

            _jwtTokenService = new JwtTokenService(_configuration, jwtLogger);
            _refreshTokenService = new RefreshTokenService(_context, _configuration, refreshLogger);
            _crypto = new SecureStringCryptoService(_configuration);

            _controller = new AuthController(_context, _crypto, _jwtTokenService, _refreshTokenService, _configuration);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
            _controller?.Dispose();
        }

        [Test]
        public void Register_WithValidModel_CreatesUser()
        {
            var model = new RegisterViewModel
            {
                Email = "test@example.com",
                Name = "Test User",
                Password = "SecurePassword123!",
                Role = "player"
            };

            var result = _controller!.Register(model);

            Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
            Assert.That(_context!.Users.FirstOrDefault(u => u.Email == "test@example.com"), Is.Not.Null);
        }

        [Test]
        public void Register_WithDuplicateEmail_ReturnsSameView()
        {
            var user = new User
            {
                Email = "test@example.com",
                Name = "Existing User",
                Role = "player",
                Password = "hash"
            };
            _context!.Users.Add(user);
            _context.SaveChanges();

            var model = new RegisterViewModel
            {
                Email = "test@example.com",
                Name = "New User",
                Password = "password",
                Role = "player"
            };

            var result = _controller!.Register(model);

            Assert.That(result, Is.InstanceOf<ViewResult>());
        }

        [Test]
        public void Login_WithValidCredentials_Returns2FASetupRequired_AndSetsPreAuthCookie()
        {
            var user = new User
            {
                Email = "test@example.com",
                Name = "Test",
                Role = "player",
                IsTwoFactorEnabled = false
            };
            user.Password = new PasswordHasher<User>().HashPassword(user, "password123");
            _context!.Users.Add(user);
            _context.SaveChanges();

            var request = new LoginRequest
            {
                Email = "test@example.com",
                Password = "password123"
            };

            var result = _controller!.Login(request);

            Assert.That(result, Is.InstanceOf<JsonResult>());
            var response = ((JsonResult)result).Value as AuthResponseViewModel;

            Assert.That(response?.Success, Is.True);
            Assert.That(response?.Message, Is.EqualTo("2FA setup required"));
            Assert.That(response?.UserId, Is.EqualTo(user.UserId));
            Assert.That(HasSetCookie("preAuthToken"), Is.True);
        }

        [Test]
        public void Login_WithInvalidCredentials_ReturnsFalse()
        {
            var user = new User
            {
                Email = "test@example.com",
                Name = "Test",
                Role = "player"
            };
            user.Password = new PasswordHasher<User>().HashPassword(user, "password123");
            _context!.Users.Add(user);
            _context.SaveChanges();

            var request = new LoginRequest
            {
                Email = "test@example.com",
                Password = "wrongpassword"
            };

            var result = _controller!.Login(request);

            Assert.That(result, Is.InstanceOf<JsonResult>());
            var response = ((JsonResult)result).Value as AuthResponseViewModel;

            Assert.That(response?.Success, Is.False);
            Assert.That(HasSetCookie("preAuthToken"), Is.False);
        }

        [Test]
        public void Login_With2FAEnabled_Returns2FARequired_AndSetsPreAuthCookie()
        {
            var user = new User
            {
                Email = "test@example.com",
                Name = "Test",
                Role = "player",
                IsTwoFactorEnabled = true,
                ProtectedTwoFactorSecret = _crypto!.Encrypt("JBSWY3DPEHPK3PXP")
            };
            user.Password = new PasswordHasher<User>().HashPassword(user, "password123");
            _context!.Users.Add(user);
            _context.SaveChanges();

            var request = new LoginRequest
            {
                Email = "test@example.com",
                Password = "password123"
            };

            var result = _controller!.Login(request);

            Assert.That(result, Is.InstanceOf<JsonResult>());
            var response = ((JsonResult)result).Value as AuthResponseViewModel;

            Assert.That(response?.Success, Is.True);
            Assert.That(response?.Message, Is.EqualTo("2FA required"));
            Assert.That(response?.UserId, Is.EqualTo(user.UserId));
            Assert.That(HasSetCookie("preAuthToken"), Is.True);
        }

        [Test]
        public void RefreshToken_WithValidRefreshToken_ReturnsSuccess_AndSetsAuthCookies()
        {
            var user = new User
            {
                Email = "test@example.com",
                Name = "Test",
                Role = "player",
                Password = "hash",
                RefreshToken = "valid-refresh-token",
                RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7)
            };
            _context!.Users.Add(user);
            _context.SaveChanges();

            var request = new RefreshTokenRequest
            {
                RefreshToken = "valid-refresh-token"
            };

            var result = _controller!.RefreshToken(request);

            Assert.That(result, Is.InstanceOf<JsonResult>());
            var response = ((JsonResult)result).Value as AuthResponseViewModel;

            Assert.That(response?.Success, Is.True);
            Assert.That(response?.Message, Is.EqualTo("Token refreshed successfully"));
            Assert.That(HasSetCookie("auth_token"), Is.True);
            Assert.That(HasSetCookie("refresh_token"), Is.True);
        }

        [Test]
        public void RefreshToken_WithExpiredToken_ReturnsFalse()
        {
            var user = new User
            {
                Email = "test@example.com",
                Name = "Test",
                Role = "player",
                Password = "hash",
                RefreshToken = "expired-refresh-token",
                RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(-1)
            };
            _context!.Users.Add(user);
            _context.SaveChanges();

            var request = new RefreshTokenRequest
            {
                RefreshToken = "expired-refresh-token"
            };

            var result = _controller!.RefreshToken(request);

            Assert.That(result, Is.InstanceOf<JsonResult>());
            var response = ((JsonResult)result).Value as AuthResponseViewModel;

            Assert.That(response?.Success, Is.False);
            Assert.That(HasSetCookie("auth_token"), Is.False);
        }

        private bool HasSetCookie(string cookieName)
        {
            var headers = _controller!.ControllerContext.HttpContext.Response.Headers["Set-Cookie"];
            return headers.Any(h => h.StartsWith($"{cookieName}=", StringComparison.OrdinalIgnoreCase));
        }
    }
}