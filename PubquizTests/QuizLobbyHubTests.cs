using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Pubquiz_Platform.Data;
using Pubquiz_Platform.Data.Entities;
using Pubquiz_Platform_V2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace PubquizTests
{
    public class QuizLobbyHubTests
    {
        private ApplicationDbContext CreateContext(string name)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(name)
                .Options;

            return new ApplicationDbContext(options);
        }

        [SetUp]
        public void SetUp()
        {
            ClearHubStaticState();
        }

        [Test]
        public async Task JoinLobby_Unauthenticated_SendsErrorToCaller()
        {
            using var ctx = CreateContext(Guid.NewGuid().ToString());

            var callerProxy = CreateSingleClientProxyMock();
            var groupProxy = CreateClientProxyMock();

            var clients = new Mock<IHubCallerClients>();
            clients.SetupGet(c => c.Caller).Returns(callerProxy.Object);
            clients.Setup(c => c.Group("ABC123")).Returns(groupProxy.Object);

            var groups = new Mock<IGroupManager>();

            var hub = CreateHub(ctx, clients, groups, "conn-1", null);

            await hub.JoinLobby("ABC123", "Alice");

            callerProxy.Verify(
                p => p.SendCoreAsync(
                    "Error",
                    It.Is<object?[]>(args => IsSingleValueArgs(args, "Not authenticated")),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            groups.Verify(
                g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task LeaveLobby_AsQuizMaster_MarksLobbyInactive_AndBroadcastsLobbyClosed()
        {
            using var ctx = CreateContext(Guid.NewGuid().ToString());

            var quizMaster = new User
            {
                Email = "qm@example.com",
                Name = "QuizMaster",
                Password = "hash",
                Role = "quizmaster"
            };

            var quiz = new Quiz
            {
                QuizName = "Test Quiz",
                QuizSlug = "test-quiz",
                QuizMaster = quizMaster,
                Questions = new List<Question>()
            };

            var lobby = new Lobby
            {
                LobbyCode = "ROOM01",
                Quiz = quiz,
                IsActive = true
            };

            ctx.Lobbies.Add(lobby);
            ctx.SaveChanges();

            var callerProxy = CreateSingleClientProxyMock();
            var groupProxy = CreateClientProxyMock();

            var clients = new Mock<IHubCallerClients>();
            clients.SetupGet(c => c.Caller).Returns(callerProxy.Object);
            clients.Setup(c => c.Group("ROOM01")).Returns(groupProxy.Object);

            var groups = new Mock<IGroupManager>();
            groups.Setup(g => g.RemoveFromGroupAsync("conn-2", "ROOM01", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var hubUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("UserId", "1"),
                new Claim(ClaimTypes.Name, "QuizMaster")
            }, "TestAuth"));

            var hub = CreateHub(ctx, clients, groups, "conn-2", hubUser);

            await hub.LeaveLobby("ROOM01", "QuizMaster", isQuizMaster: true);

            var reloaded = ctx.Lobbies.First(l => l.LobbyCode == "ROOM01");
            Assert.That(reloaded.IsActive, Is.False);

            groupProxy.Verify(
                p => p.SendCoreAsync(
                    "LobbyClosed",
                    It.Is<object?[]>(args => args.Length == 0),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task StartQuiz_WithQuizMasterStarter_BroadcastsQuizStartedAndFirstQuestion()
        {
            using var ctx = CreateContext(Guid.NewGuid().ToString());

            var quizMaster = new User
            {
                Email = "qm@example.com",
                Name = "QuizMaster",
                Password = "hash",
                Role = "quizmaster"
            };

            var quiz = new Quiz
            {
                QuizName = "General Knowledge",
                QuizSlug = "general-knowledge",
                QuizMaster = quizMaster,
                Questions = new List<Question>
                {
                    new Question
                    {
                        QuestionText = "Capital of France?",
                        CorrectAnswer = "Paris",
                        FalseAnswer1 = "Berlin",
                        FalseAnswer2 = "Rome",
                        FalseAnswer3 = "Madrid",
                        QuestionNumber = 1
                    }
                }
            };

            var lobby = new Lobby
            {
                LobbyCode = "ROOM02",
                Quiz = quiz,
                IsActive = false
            };

            ctx.Lobbies.Add(lobby);
            ctx.SaveChanges();

            var callerProxy = CreateSingleClientProxyMock();
            var groupProxy = CreateClientProxyMock();

            var clients = new Mock<IHubCallerClients>();
            clients.SetupGet(c => c.Caller).Returns(callerProxy.Object);
            clients.Setup(c => c.Group("ROOM02")).Returns(groupProxy.Object);

            var groups = new Mock<IGroupManager>();

            var hubUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("UserId", "2"),
                new Claim(ClaimTypes.Name, "QuizMaster")
            }, "TestAuth"));

            var hub = CreateHub(ctx, clients, groups, "conn-3", hubUser);

            await hub.StartQuiz("ROOM02", "QuizMaster");

            var reloaded = ctx.Lobbies.First(l => l.LobbyCode == "ROOM02");
            Assert.That(reloaded.IsActive, Is.True);

            groupProxy.Verify(
                p => p.SendCoreAsync(
                    "QuizStarted",
                    It.Is<object?[]>(args => IsSingleValueArgs(args, "ROOM02")),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            var showQuestionInvocation = groupProxy.Invocations
                .Single(i => i.Arguments.Count == 3 && i.Arguments[0] as string == "ShowQuestion");

            var payloadArgs = (object?[])showQuestionInvocation.Arguments[1]!;
            var payload = payloadArgs[0]!;

            Assert.That(GetPropertyValue<string>(payload, "quizName"), Is.EqualTo("General Knowledge"));
            Assert.That(GetPropertyValue<string>(payload, "currentQuestionText"), Is.EqualTo("Capital of France?"));
            
            var expectedQuestionId = ctx.Questions.Single().QuestionId;
            Assert.That(GetPropertyValue<int>(payload, "questionId"), Is.EqualTo(expectedQuestionId));
            Assert.That(GetPropertyValue<int>(payload, "totalPlayers"), Is.EqualTo(0));
            Assert.That(GetPropertyValue<int>(payload, "answeredCount"), Is.EqualTo(0));
        }

        private static QuizLobbyHub CreateHub(
            ApplicationDbContext ctx,
            Mock<IHubCallerClients> clients,
            Mock<IGroupManager> groups,
            string connectionId,
            ClaimsPrincipal? user)
        {
            var hubContext = new Mock<HubCallerContext>();
            hubContext.SetupGet(c => c.ConnectionId).Returns(connectionId);
            hubContext.SetupGet(c => c.User).Returns(user);

            return new QuizLobbyHub(ctx, NullLogger<QuizLobbyHub>.Instance)
            {
                Clients = clients.Object,
                Groups = groups.Object,
                Context = hubContext.Object
            };
        }

        private static Mock<ISingleClientProxy> CreateSingleClientProxyMock()
        {
            var proxy = new Mock<ISingleClientProxy>();
            proxy.Setup(p => p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            return proxy;
        }

        private static Mock<IClientProxy> CreateClientProxyMock()
        {
            var proxy = new Mock<IClientProxy>();
            proxy.Setup(p => p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            return proxy;
        }

        private static bool IsSingleValueArgs(object?[] args, object? expected)
            => args.Length == 1 && Equals(args[0], expected);

        private static T? GetPropertyValue<T>(object instance, string propertyName)
        {
            var value = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(instance);

            return value is T typed ? typed : default;
        }

        private static void ClearHubStaticState()
        {
            var hubType = typeof(QuizLobbyHub);
            var fields = new[] { "LobbyPlayers", "LobbyStates" };

            foreach (var fieldName in fields)
            {
                var field = hubType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
                var value = field?.GetValue(null);
                value?.GetType().GetMethod("Clear")?.Invoke(value, null);
            }
        }
    }
}