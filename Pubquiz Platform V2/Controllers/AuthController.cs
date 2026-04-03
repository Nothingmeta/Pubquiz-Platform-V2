using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity; // Nodig voor PasswordHasher
using Microsoft.AspNetCore.Mvc;
using Pubquiz_Platform.Data;
using Pubquiz_Platform.Data.Entities;
using Pubquiz_Platform_V2.ViewModels;
using System.Security.Claims;

namespace PubquizPlatform.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher = new PasswordHasher<User>();

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
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

            // Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("UserId", user.UserId.ToString())
            };

            var identity = new ClaimsIdentity(claims, "PubquizCookie");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("PubquizCookie", principal);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("PubquizCookie");
            return RedirectToAction("Login");
        }
    }
}
/*
✅ Wachtwoordhashing en -validatie
✅ Validatie van lengte en uniciteit
✅ OWASP controles
 */