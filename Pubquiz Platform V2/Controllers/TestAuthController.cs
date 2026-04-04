using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Pubquiz_Platform.Data;
using Pubquiz_Platform.Data.Entities;

namespace Pubquiz_Platform_V2.Controllers
{
    [ApiController]
    [Route("test")]
    public class TestAuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly PasswordHasher<User> _passwordHasher = new();

        public TestAuthController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // Development-only helper to create/find a user and sign them in.
        // Accepts form or JSON: { email, password, name (optional), role (optional) }
        [HttpPost("create-and-signin")]
        public async Task<IActionResult> CreateAndSignIn([FromForm] string email, [FromForm] string password, [FromForm] string? name = null, [FromForm] string? role = "user")
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
                // If user exists but password mismatch, overwrite for test convenience
                var verify = _passwordHasher.VerifyHashedPassword(user, user.Password, password);
                if (verify == PasswordVerificationResult.Failed)
                {
                    user.Password = _passwordHasher.HashPassword(user, password);
                    _context.SaveChanges();
                }
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Name ?? user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "user"),
                new Claim("UserId", user.UserId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "PubquizCookie");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("PubquizCookie", principal);

            return Ok(new { userId = user.UserId, email = user.Email });
        }
    }
}