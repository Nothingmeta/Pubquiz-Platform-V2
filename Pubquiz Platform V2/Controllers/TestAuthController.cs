using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Pubquiz_Platform.Data;
using Pubquiz_Platform.Data.Entities;
using Pubquiz_Platform_V2.Services;

namespace Pubquiz_Platform_V2.Controllers
{
    [ApiController]
    [Route("test")]
    public class TestAuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<User> _passwordHasher = new();

        public TestAuthController(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService refreshTokenService,
            IConfiguration configuration)
        {
            _context = context;
            _env = env;
            _jwtTokenService = jwtTokenService;
            _refreshTokenService = refreshTokenService;
            _configuration = configuration;
        }

        // Development-only helper to create/find a user and sign them in.
        // Accepts form or JSON: { email, password, name (optional), role (optional) }
        [HttpPost("create-and-signin")]
        public IActionResult CreateAndSignIn(
            [FromForm] string email,
            [FromForm] string password,
            [FromForm] string? name = null,
            [FromForm] string? role = "user")
        {
            if (!_env.IsDevelopment())
                return Forbid();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return BadRequest("email and password are required.");

            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                user = new User
                {
                    Email = email,
                    Name = string.IsNullOrWhiteSpace(name) ? email : name!,
                    Role = string.IsNullOrWhiteSpace(role) ? "user" : role!
                };

                user.Password = _passwordHasher.HashPassword(user, password);
                _context.Users.Add(user);
                _context.SaveChanges();
            }
            else
            {
                var verify = _passwordHasher.VerifyHashedPassword(user, user.Password, password);
                if (verify == PasswordVerificationResult.Failed)
                {
                    user.Password = _passwordHasher.HashPassword(user, password);
                    _context.SaveChanges();
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    user.Name = name!;
                }

                if (!string.IsNullOrWhiteSpace(role))
                {
                    user.Role = role!;
                }

                _context.SaveChanges();
            }

            IssueAuthCookies(user);

            return Ok(new { userId = user.UserId, email = user.Email });
        }

        private CookieOptions BuildAuthCookieOptions(TimeSpan lifetime) => new()
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.Add(lifetime)
        };

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
    }
}