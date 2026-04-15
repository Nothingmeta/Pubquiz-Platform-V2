using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Pubquiz_Platform_V2.Services;
using System.Collections.Generic;
using System.Linq;

namespace PubquizTests
{
    public class JwtTokenServiceTests
    {
        private IConfiguration BuildConfig(string issuer = "TestIssuer", string audience = "TestAudience")
        {
            var settings = new Dictionary<string, string?>
            {
                { "Jwt:SecretKey", "test-secret-key-at-least-32-characters-long-for-256-bit" },
                { "Jwt:Issuer", issuer },
                { "Jwt:Audience", audience },
                { "Jwt:AccessTokenExpirationMinutes", "15" }
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        [Test]
        public void GenerateAccessToken_ContainsExpectedClaims()
        {
            var config = BuildConfig();
            var service = new JwtTokenService(config, NullLogger<JwtTokenService>.Instance);

            var token = service.GenerateAccessToken(42, "test@example.com", "Test User", "quizmaster");
            var principal = service.ValidateToken(token);

            Assert.That(principal, Is.Not.Null);
            Assert.That(principal!.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value, Is.EqualTo("42"));
            Assert.That(principal.Claims.FirstOrDefault(c => c.Type == "TokenType")?.Value, Is.EqualTo("Access"));
            Assert.That(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value, Is.EqualTo("test@example.com"));
            Assert.That(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name)?.Value, Is.EqualTo("Test User"));
            Assert.That(principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value, Is.EqualTo("quizmaster"));
        }

        [Test]
        public void ValidatePreAuthToken_WithPreAuthToken_ReturnsValidUserId()
        {
            var config = BuildConfig();
            var service = new JwtTokenService(config, NullLogger<JwtTokenService>.Instance);

            var token = service.GeneratePreAuthToken(99);
            var result = service.ValidatePreAuthToken(token);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.UserId, Is.EqualTo(99));
        }

        [Test]
        public void ValidatePreAuthToken_WithAccessToken_ReturnsInvalid()
        {
            var config = BuildConfig();
            var service = new JwtTokenService(config, NullLogger<JwtTokenService>.Instance);

            var accessToken = service.GenerateAccessToken(11, "a@b.com", "A", "player");
            var result = service.ValidatePreAuthToken(accessToken);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.UserId, Is.EqualTo(0));
        }

        [Test]
        public void ValidateToken_WithMismatchedIssuer_ReturnsNull()
        {
            var issuerA = BuildConfig(issuer: "IssuerA");
            var issuerB = BuildConfig(issuer: "IssuerB");

            var serviceA = new JwtTokenService(issuerA, NullLogger<JwtTokenService>.Instance);
            var serviceB = new JwtTokenService(issuerB, NullLogger<JwtTokenService>.Instance);

            var token = serviceA.GenerateAccessToken(1, "test@example.com", "Test", "player");
            var principal = serviceB.ValidateToken(token);

            Assert.That(principal, Is.Null);
        }
    }
}