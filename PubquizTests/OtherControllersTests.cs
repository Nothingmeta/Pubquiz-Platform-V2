using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Pubquiz_Platform.Data;
using Pubquiz_Platform.Data.Entities;
using Pubquiz_Platform_V2.Controllers;
using Pubquiz_Platform_V2.Models;
using System.Linq;
using System.Security.Claims;

namespace PubquizTests
{
    public class OtherControllersTests
    {
        private ApplicationDbContext CreateInMemoryContext(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(name)
                .Options;
            return new ApplicationDbContext(options);
        }

        private QuizController CreateQuizControllerWithUser(ApplicationDbContext ctx, int userId, string userName = "tester")
        {
            var controller = new QuizController(ctx);
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] {
                new Claim("UserId", userId.ToString()),
                new Claim(ClaimTypes.Name, userName)
            }, "TestAuth"));
            controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext() { User = user }
            };
            return controller;
        }

        [Test]
        public void QuizController_CreateQuick_CreatesQuizAndQuestion()
        {
            using var ctx = CreateInMemoryContext("qc1");
            var controller = CreateQuizControllerWithUser(ctx, 42);

            var vm = new Pubquiz_Platform_V2.ViewModels.QuizCreateViewModel { QuizName = "Unit Test Quiz" };
            var result = controller.CreateQuick(vm) as JsonResult;
            Assert.IsNotNull(result);
            var quiz = ctx.Quizzes.Include(q => q.Questions).FirstOrDefault();
            Assert.IsNotNull(quiz);
            Assert.That(quiz.QuizMasterId, Is.EqualTo(42));
            Assert.IsTrue(quiz.Questions.Any());
        }

        [Test]
        public void QuizController_Manage_Unauthorized_WithoutClaim()
        {
            using var ctx = CreateInMemoryContext("m1");
            var controller = new QuizController(ctx);
            controller.ControllerContext = new ControllerContext() { HttpContext = new DefaultHttpContext() };
            var result = controller.Manage();
            Assert.IsInstanceOf<UnauthorizedResult>(result);
        }

        [Test]
        public void QuizController_Manage_ReturnsQuizzesForUser()
        {
            using var ctx = CreateInMemoryContext("m2");
            // seed a quiz for user id 7
            ctx.Quizzes.Add(new Quiz { QuizName = "Q1", QuizSlug = "q1", QuizMasterId = 7 });
            ctx.SaveChanges();

            var controller = CreateQuizControllerWithUser(ctx, 7);
            var result = controller.Manage() as ViewResult;
            Assert.IsNotNull(result);
            var model = result.Model as System.Collections.IEnumerable;
            Assert.IsNotNull(model);
        }
    }
}
