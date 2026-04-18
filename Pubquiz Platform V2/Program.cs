using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pubquiz_Platform.Data;
using Pubquiz_Platform.Data.Entities;
using Pubquiz_Platform_V2.Models;
using Pubquiz_Platform_V2.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// JWT Configuration
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"];

if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("JWT SecretKey is not configured in appsettings.json");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Handle JWT from Authorization header AND cookies (for Razor Pages)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Try Authorization header first
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (!string.IsNullOrEmpty(token))
            {
                context.Token = token;
                return Task.CompletedTask;
            }

            // Fall back to cookie for Razor Pages
            if (context.Request.Cookies.TryGetValue("auth_token", out var cookieToken))
            {
                context.Token = cookieToken;
            }

            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            // Redirect to login for ALL browser requests (except API calls)
            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.Redirect("/Auth/Login");
                context.HandleResponse();
            }
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            // Log authentication failures
            Console.WriteLine($"Authentication failed: {context.Exception?.Message}");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddSingleton<ISecretCryptoService, SecureStringCryptoService>();

builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Register JWT Services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

// Configure Database - Support both SQLite and PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (builder.Environment.IsDevelopment())
{
    // Use SQLite in development
    builder.Services.AddDbContext<ApplicationDbContext>(x => 
        x.UseSqlite(connectionString ?? "Data Source=Pubquiz.sqlite"));
}
else
{
    // Use PostgreSQL in production/docker
    builder.Services.AddDbContext<ApplicationDbContext>(x => 
        x.UseNpgsql(connectionString));
}

// Add Antiforgery services
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

var app = builder.Build();

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; base-uri 'self'; frame-ancestors 'self'; object-src 'none'; form-action 'self'; " +
        "img-src 'self' data:; connect-src 'self' ws: wss:; script-src 'self' https://cdnjs.cloudflare.com 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'";
    await next();
});

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var databaseProvider = dbContext.Database.ProviderName;

        Console.WriteLine($"Database provider: {databaseProvider}");
        Console.WriteLine("Attempting database migration...");

        dbContext.Database.Migrate();
        Console.WriteLine("✅ Database migration completed successfully.");

        SeedDockerTestUser(dbContext, configuration);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database migration failed: {ex.Message}");
    }
}

static void SeedDockerTestUser(ApplicationDbContext dbContext, IConfiguration configuration)
{
    var enabled = configuration.GetValue<bool>("Testing:SeedDockerTestUser");
    if (!enabled)
    {
        return;
    }

    var email = configuration["Testing:DockerTestUser:Email"] ?? "zap@test.local";
    var password = configuration["Testing:DockerTestUser:Password"] ?? "ZAP-ChangeMe123!";
    var name = configuration["Testing:DockerTestUser:Name"] ?? "ZAP Test User";
    var role = configuration["Testing:DockerTestUser:Role"] ?? "quizmaster";

    var user = dbContext.Users.FirstOrDefault(u => u.Email == email);
    if (user == null)
    {
        user = new User
        {
            Email = email
        };

        dbContext.Users.Add(user);
    }

    user.Name = name;
    user.Role = role;
    user.IsTwoFactorEnabled = false;
    user.ProtectedTwoFactorSecret = null;
    user.ProtectedRecoveryCodes = null;
    user.TwoFactorFailedCount = 0;
    user.TwoFactorLockoutEnd = null;

    var hasher = new PasswordHasher<User>();
    user.Password = hasher.HashPassword(user, password);

    dbContext.SaveChanges();

    Console.WriteLine($"✅ Seeded Docker test user: {email}");
}

// Enable detailed error messages in development
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();  // Production only
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Add a simple root redirect - redirect before routing to avoid auth checks
app.MapGet("/", async context =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        context.Response.Redirect("/Home/Index");
    }
    else
    {
        context.Response.Redirect("/Auth/Login");
    }

    await Task.CompletedTask;
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<QuizLobbyHub>("/quizlobbyhub");

// Add a simple health check endpoint
app.MapGet("/health", () => Results.Ok("Application is running"));

try
{
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"Application failed to start: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    throw;
}
