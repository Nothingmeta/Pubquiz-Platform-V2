using Microsoft.EntityFrameworkCore;
using Pubquiz_Platform.Data;
using Pubquiz_Platform_V2.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication("PubquizCookie")
    .AddCookie("PubquizCookie", options =>
    {
        options.LoginPath = "/Auth/Login";
    });

builder.Services.AddAuthorization();
// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSignalR();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(x => x.UseSqlite(connectionString));
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseForwardedHeaders(); // Handle X-Forwarded-* headers from proxy
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<QuizLobbyHub>("/quizlobbyhub");
app.Run();
