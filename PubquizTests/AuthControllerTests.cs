using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NUnit.Framework;
using Pubquiz_Platform.Data;
using Pubquiz_Platform.Data.Entities;
using PubquizPlatform.Controllers;
using Pubquiz_Platform_V2.ViewModels;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace PubquizTests
{
    public class AuthControllerTests
    {
        private ApplicationDbContext CreateInMemoryContext(string name)
        {            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(name)
                .Options;
            return new ApplicationDbContext(options);
        }

        private AuthController CreateController(ApplicationDbContext ctx)
        {            var services = new ServiceCollection();
            services.AddDataProtection();
            services.AddSingleton<ITempDataProvider, TestTempDataProvider>();
            var sp = services.BuildServiceProvider();
            var provider = sp.GetRequiredService<IDataProtectionProvider>();
            var controller = new AuthController(ctx, provider);
            controller.ControllerContext = new ControllerContext()
            {                HttpContext = new DefaultHttpContext()
            };
            // Provide a TempDataDictionary so controller actions using TempData work in tests
            controller.TempData = new TempDataDictionary(controller.HttpContext, sp.GetRequiredService<ITempDataProvider>());
            return controller;
        }

        private class TestTempDataProvider : ITempDataProvider
        {
            private const string Key = "__TestTempData";
            public IDictionary<string, object> LoadTempData(HttpContext context)
            {
                if (context.Items.TryGetValue(Key, out var obj) && obj is IDictionary<string, object> dict)
                {
                    return new Dictionary<string, object>(dict);
                }
                return new Dictionary<string, object>();
            }

            public void SaveTempData(HttpContext context, IDictionary<string, object> values)
            {
                context.Items[Key] = new Dictionary<string, object>(values);
            }
        }

        [SetUp]
        public void Setup() { }

        [Test]
        public void Register_CreatesUser_WhenModelValid()
        {            using var ctx = CreateInMemoryContext("reg1");            var controller = CreateController(ctx);
            var model = new RegisterViewModel { Email = "u@e", Name = "User", Password = "P@ssw0rd", Role = "User" };
            var result = controller.Register(model) as RedirectToActionResult;
            Assert.IsNotNull(result);            Assert.AreEqual("Login", result.ActionName);            var user = ctx.Users.FirstOrDefault(u => u.Email == "u@e");            Assert.IsNotNull(user);        }

        [Test]
        public async Task EnableTwoFactor_GeneratesQrAndManualKey()
        {            using var ctx = CreateInMemoryContext("tf1");            var controller = CreateController(ctx);
            var user = new User { Email = "a@b", Name = "A", Password = "test", Role = "speler" };            ctx.Users.Add(user); ctx.SaveChanges();
            // simulate preauth via TempData            controller.TempData["preauth_userid"] = user.UserId.ToString();            var result = controller.EnableTwoFactor() as ViewResult;            Assert.IsNotNull(result);            var vm = result.Model as Pubquiz_Platform_V2.ViewModels.EnableTwoFactorViewModel;            Assert.IsNotNull(vm);            Assert.IsNotEmpty(vm.ManualEntryKey);        }
    }
}
