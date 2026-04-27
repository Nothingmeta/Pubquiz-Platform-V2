using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pubquiz_Platform.Data;
using Pubquiz_Platform_V2.Models;
using Pubquiz_Platform_V2.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// If running behind a reverse proxy / container, honor forwarded HTTPS/proto headers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var databaseProvider = dbContext.Database.ProviderName;
        
        Console.WriteLine($"Database provider: {databaseProvider}");
        Console.WriteLine("Attempting database migration...");
        
        dbContext.Database.Migrate();
        Console.WriteLine("✅ Database migration completed successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database migration failed: {ex.Message}");
    }
}

// Enable detailed error messages in development
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseForwardedHeaders();
    app.UseExceptionHandler("/Home/Error");
    app.UseHttpsRedirection();
    app.UseHsts();
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "base-uri 'self'; " +
            "frame-ancestors 'self'; " +
            "object-src 'none'; " +
            "img-src 'self' data:; " +
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
            "script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com; " +
            "font-src 'self' data: https://cdn.jsdelivr.net; " +
            "connect-src 'self' https: wss:;";
        
        return Task.CompletedTask;
    });

    await next();
});

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
