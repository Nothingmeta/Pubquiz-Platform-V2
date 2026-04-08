using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pubquiz_Platform.Data;
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

builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Register JWT Service
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(x => x.UseSqlite(connectionString));

var app = builder.Build();

// Enable detailed error messages in development
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();  // ← MOVE THIS HERE (production only)
    app.UseForwardedHeaders();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Add a simple root redirect - redirect before routing to avoid auth checks
app.MapGet("/", async context =>
{
    // Check if authenticated by looking for token
    var hasToken = context.Request.Cookies.TryGetValue("auth_token", out var token) && !string.IsNullOrEmpty(token);
    
    if (hasToken)
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
