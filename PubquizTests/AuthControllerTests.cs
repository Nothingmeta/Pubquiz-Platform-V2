using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Pubquiz_Platform.Data;
using Pubquiz_Platform.Data.Entities;
using PubquizPlatform.Controllers;
using Pubquiz_Platform_V2.Services;
using Pubquiz_Platform_V2.ViewModels;
using System;
using System.Collections.Generic;

namespace PubquizTests
{
    public class AuthControllerTests
    {
        private ApplicationDbContext? _context;
        private AuthController? _controller;
        private IJwtTokenService? _jwtTokenService;
        private IConfiguration? _configuration;
        private IDisposable? _serviceProvider;

        [SetUp]
        public void Setup()
        {
            // Create in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"test_db_{Guid.NewGuid()}")
                .Options;
            _context = new ApplicationDbContext(options);

            // Setup configuration with test JWT settings
            var inMemorySettings = new Dictionary<string, string?>
            {
                { "Jwt:SecretKey", "test-secret-key-at-least-32-characters-long-for-256-bit" },
                { "Jwt:Issuer", "TestIssuer" },
                { "Jwt:Audience", "TestAudience" },
                { "Jwt:AccessTokenExpirationMinutes", "15" }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<JwtTokenService>();

            _jwtTokenService = new JwtTokenService(_configuration, logger);

            // Create controller
            var services = new ServiceCollection();
            services.AddDataProtection();
            _serviceProvider = services.BuildServiceProvider();
            var provider = ((IServiceProvider)_serviceProvider).GetRequiredService<IDataProtectionProvider>();

            _controller = new AuthController(_context, provider, _jwtTokenService);
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
            _serviceProvider?.Dispose();
            _controller?.Dispose();
        }

        [Test]
        public void Register_WithValidModel_CreatesUser()
        {
            // Arrange
            var model = new RegisterViewModel
            {
                Email = "test@example.com",
                Name = "Test User",
                Password = "SecurePassword123!",
                Role = "player"
            };

            // Act
            var result = _controller!.Register(model);

            // Assert
            Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
            Assert.That(_context!.Users.FirstOrDefault(u => u.Email == "test@example.com"), Is.Not.Null);
        }

        [Test]
        public void Register_WithDuplicateEmail_ReturnsSameView()
        {
            // Arrange
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

            // Act
            var result = _controller!.Register(model);

            // Assert
            Assert.That(result, Is.InstanceOf<ViewResult>());
        }

        [Test]
        public void Login_WithValidCredentials_ReturnsJsonWithAccessToken()
        {
            // Arrange
            var user = new User 
            { 
                Email = "test@example.com", 
                Name = "Test", 
                Role = "player", 
                IsTwoFactorEnabled = false 
            };
            user.Password = new Microsoft.AspNetCore.Identity.PasswordHasher<User>()
                .HashPassword(user, "password123");
            _context!.Users.Add(user);
            _context.SaveChanges();

            // Act
            var result = _controller!.Login("test@example.com", "password123");

            // Assert
            Assert.That(result, Is.InstanceOf<JsonResult>());
            var jsonResult = (JsonResult)result;
            var response = jsonResult.Value as AuthResponseViewModel;
            Assert.That(response?.Success, Is.True);
            Assert.That(response?.AccessToken, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void Login_WithInvalidCredentials_ReturnsFalse()
        {
            // Arrange
            var user = new User 
            { 
                Email = "test@example.com", 
                Name = "Test", 
                Role = "player" 
            };
            user.Password = new Microsoft.AspNetCore.Identity.PasswordHasher<User>()
                .HashPassword(user, "password123");
            _context!.Users.Add(user);
            _context.SaveChanges();

            // Act
            var result = _controller!.Login("test@example.com", "wrongpassword");

            // Assert
            Assert.That(result, Is.InstanceOf<JsonResult>());
            var response = ((JsonResult)result).Value as AuthResponseViewModel;
            Assert.That(response?.Success, Is.False);
        }

        [Test]
        public void Login_With2FAEnabled_ReturnsPreAuthToken()
        {
            // Arrange
            var user = new User 
            { 
                Email = "test@example.com", 
                Name = "Test", 
                Role = "player",
                IsTwoFactorEnabled = true,
                ProtectedTwoFactorSecret = "protected_secret"
            };
            user.Password = new Microsoft.AspNetCore.Identity.PasswordHasher<User>()
                .HashPassword(user, "password123");
            _context!.Users.Add(user);
            _context.SaveChanges();

            // Act
            var result = _controller!.Login("test@example.com", "password123");

            // Assert
            var response = ((JsonResult)result).Value as AuthResponseViewModel;
            Assert.That(response?.PreAuthToken, Is.Not.Null.And.Not.Empty);
            Assert.That(response?.Success, Is.True);
        }
    }
}
