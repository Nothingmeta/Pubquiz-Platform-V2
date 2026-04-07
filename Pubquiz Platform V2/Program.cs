using Microsoft.EntityFrameworkCore;
using Pubquiz_Platform.Data;
using Pubquiz_Platform_V2.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using System.IO;
using System.Runtime.InteropServices;

// Reliable Docker detection: check OS platform + container environment variable
bool isRunningInDocker = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                         Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

Console.WriteLine($"ℹ Running in Docker: {isRunningInDocker}");

// ONLY FOR LOCAL DEVELOPMENT (keep this)
var keyPath = isRunningInDocker ? null : Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "PubquizPlatform",
    "Keys"
);

var keyDirectory = !isRunningInDocker ? new DirectoryInfo(keyPath) : null;

if (keyDirectory != null && !keyDirectory.Exists)
{
    keyDirectory.Create();
    Console.WriteLine($"✓ Created key directory: {keyPath}");
}

var builder = WebApplication.CreateBuilder(args);

// CRITICAL FIX FOR DOCKER:
// Don't persist keys to filesystem in Docker. Use .NET's default in-memory protection.
// This works because:
// - The app starts, creates a key in memory
// - All encryption/decryption in the SAME app instance uses the SAME key
// - No filesystem I/O issues, no timing problems
// - For persistent deployment, use cloud key management (Azure Key Vault, AWS Secrets Manager)
if (isRunningInDocker)
{
    // Use default data protection - no filesystem persistence
    // Key lives in memory for the app lifetime
    builder.Services.AddDataProtection()
        .SetApplicationName("PubquizPlatform");
    Console.WriteLine("✓ Using DEFAULT in-memory data protection (Docker/cloud deployment)");
}
else
{
    // Local development: persist to filesystem
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(keyDirectory)
        .SetApplicationName("PubquizPlatform");
    Console.WriteLine($"✓ Using FILESYSTEM data protection: {keyPath}");
}

// Configure Authentication
builder.Services.AddAuthentication("PubquizCookie")
    .AddCookie("PubquizCookie", options =>
    {
        options.LoginPath = "/Auth/Login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
    });

builder.Services.AddAuthorization();

// Add Session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Configure antiforgery - use session-based tokens, NOT encrypted cookies
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Expiration = TimeSpan.Zero; // Use session cookie
    options.Cookie.MaxAge = null;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.SuppressXFrameOptionsHeader = false;
});

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Get connection string with intelligent Docker detection
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
string connectionString;

if (isRunningInDocker)
{
    // Always use Docker path in container
    connectionString = "Data Source=/app/data/Pubquiz.sqlite";
}
else
{
    // Use local development path
    connectionString = (rawConnectionString ?? "Data Source=Pubquiz.sqlite").Trim();
}

// Fail fast if invalid
if (!connectionString.StartsWith("Data Source="))
{
    throw new InvalidOperationException(
        $"Invalid SQLite connection string: '{connectionString}'. " +
        "Must start with 'Data Source='");
}

builder.Services.AddDbContext<ApplicationDbContext>(x => x.UseSqlite(connectionString));

var app = builder.Build();

// REMOVE the data protection pre-init code - it causes race conditions
// Just comment it out or delete it entirely

// Run database migrations on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database migration");
    }
}

// CRITICAL: Add session middleware BEFORE routing
app.UseSession();

app.MapHub<QuizLobbyHub>("/quizlobbyhub");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();


app.UseAuthentication();
app.UseAuthorization();

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Configure port for Render (uses 8080 by default)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

Console.WriteLine($"ℹ Listening on port: {port}");

app.Run();
