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
app.UseAuthentication(); // vóór app.UseAuthorization()
app.UseAuthorization();

app.MapHub<QuizLobbyHub>("/quizlobbyhub");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
