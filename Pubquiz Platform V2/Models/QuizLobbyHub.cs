using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Pubquiz_Platform.Data;
using Pubquiz_Platform.Data.Entities;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;

namespace Pubquiz_Platform_V2.Models
{
    [Authorize]
    public class QuizLobbyHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<QuizLobbyHub> _logger;

        // Thread-safe dictionary for lobby players
        private static readonly ConcurrentDictionary<string, HashSet<string>> LobbyPlayers = new();

        // Per-lobby quiz runtime state
        private static readonly ConcurrentDictionary<string, LobbyState> LobbyStates = new();

        public QuizLobbyHub(ApplicationDbContext context, ILogger<QuizLobbyHub> logger)
        {
            _context = context;
            _logger = logger;
        }

        public override Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst("UserId")?.Value;
            var userName = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            
            _logger.LogInformation($"User {userId} ({userName}) connected to QuizLobbyHub");
            return base.OnConnectedAsync();
        }

        public async Task JoinLobby(string lobbyCode, string playerName)
        {
            // Verify the user is authenticated and the name matches their user claim
            var userId = Context.User?.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("Error", "Not authenticated");
                return;
            }

            var players = LobbyPlayers.GetOrAdd(lobbyCode, _ => new HashSet<string>());
            lock (players)
            {
                players.Add(playerName);
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyCode);
            await Clients.Group(lobbyCode).SendAsync("PlayerListUpdated", players.ToList());

            // If quiz already started for this lobby, send current question to the joining client
            if (LobbyStates.TryGetValue(lobbyCode, out var state))
            {
                var q = state.GetCurrentQuestion();
                if (q != null)
                {
                    var payload = BuildQuestionPayload(state, q);
                    await Clients.Caller.SendAsync("ShowQuestion", payload);
                    // also send current answered count
                    await Clients.Caller.SendAsync("AnswersCountUpdated", state.AnswersForCurrent.Count, state.Players.Count);
                }
            }
        }

        public async Task LeaveLobby(string lobbyCode, string playerName, bool isQuizMaster = false)
        {
            var players = LobbyPlayers.GetOrAdd(lobbyCode, _ => new HashSet<string>());
            lock (players)
            {
                players.Remove(playerName);
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, lobbyCode);
            
            // Only close the lobby if the quizmaster leaves
            if (isQuizMaster)
            {
                // Clear lobby state when quizmaster leaves
                LobbyStates.TryRemove(lobbyCode, out _);
                
                // Mark lobby as inactive in database
                var lobby = await _context.Lobbies
                    .FirstOrDefaultAsync(l => l.LobbyCode == lobbyCode);
                
                if (lobby != null)
                {
                    lobby.IsActive = false;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Lobby {lobbyCode} marked as inactive");
                }
                
                // Clear players from lobby
                LobbyPlayers.TryRemove(lobbyCode, out _);
                
                // Notify remaining players that lobby is closed
                await Clients.Group(lobbyCode).SendAsync("LobbyClosed");
            }
            else
            {
                // If a regular player leaves, just update the player list
                await Clients.Group(lobbyCode).SendAsync("PlayerListUpdated", players.ToList());
            }
        }

        // Start quiz: only the quizmaster may start
        public async Task StartQuiz(string lobbyCode, string starterName)
        {
            var lobby = await _context.Lobbies
                .Include(l => l.Quiz)
                    .ThenInclude(q => q.Questions)
                .Include(l => l.Quiz)
                    .ThenInclude(q => q.QuizMaster)
                .FirstOrDefaultAsync(l => l.LobbyCode == lobbyCode);

            if (lobby == null || lobby.Quiz == null || lobby.Quiz.QuizMaster == null)
            {
                return;
            }

            // Verify starter is quizmaster
            if (lobby.Quiz.QuizMaster.Name != starterName)
            {
                return;
            }

            // Build runtime state (questions ordered by QuestionNumber)
            var questions = lobby.Quiz.Questions?.OrderBy(q => q.QuestionNumber).ToList() ?? new List<Question>();
            var state = new LobbyState
            {
                QuizName = lobby.Quiz.QuizName,
                Questions = questions,
                CurrentIndex = 0,
                AnswersForCurrent = new Dictionary<string, int?>(),
                HasRevealedAnswers = false,
                Scores = new Dictionary<string, int?>()
            };

            // Initialize players set from LobbyPlayers if present
            if (LobbyPlayers.TryGetValue(lobbyCode, out var players))
            {
                // Exclude the quizmaster identity from the runtime players list so the quizmaster isn't
                // counted in totals or shown as "no answer". Preserve casing-insensitive match.
                var qm = lobby.Quiz.QuizMaster.Name ?? "";
                state.Players = players
                    .Where(p => !string.Equals(p, qm, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else
            {
                state.Players = new List<string>();
            }

            // Initialize scores (zero) for players snapshot
            foreach (var p in state.Players)
            {
                if (!state.Scores.ContainsKey(p))
                    state.Scores[p] = 0;
            }

            LobbyStates[lobbyCode] = state;

            // Mark lobby active (optional)
            lobby.IsActive = true;
            await _context.SaveChangesAsync();

            // Notify clients to navigate to play; include lobbyCode so client can build URL
            await Clients.Group(lobbyCode).SendAsync("QuizStarted", lobbyCode);

            // Immediately send the first question to the group
            await SendCurrentQuestionToGroup(lobbyCode);
        }

        // Players invoke this to submit their answer for the current question
        public async Task SubmitAnswer(string lobbyCode, string playerName, int questionId, int answerIndex)
        {
            if (!LobbyStates.TryGetValue(lobbyCode, out var state))
                return;

            // Prevent quizmaster from submitting (if the name matches)
            var lobby = await _context.Lobbies
                .Include(l => l.Quiz)
                    .ThenInclude(q => q.QuizMaster)
                .FirstOrDefaultAsync(l => l.LobbyCode == lobbyCode);

            if (lobby != null && lobby.Quiz?.QuizMaster != null && lobby.Quiz.QuizMaster.Name == playerName)
            {
                // ignore submissions from quizmaster identity
                return;
            }

            lock (state.Lock)
            {
                // if answers already revealed, ignore further submissions
                if (state.HasRevealedAnswers)
                    return;

                var currentQuestion = state.GetCurrentQuestion();
                if (currentQuestion == null || currentQuestion.QuestionId != questionId)
                    return;

                // record answer (allow overwriting)
                state.AnswersForCurrent[playerName] = answerIndex;
            }

            // Broadcast answered count
            int answered;
            int total;
            lock (state.Lock)
            {
                answered = state.AnswersForCurrent.Count;
                total = state.Players.Count;
            }
            await Clients.Group(lobbyCode).SendAsync("AnswersCountUpdated", answered, total);
            // Optional: announce who answered (for visual tick)
            await Clients.Group(lobbyCode).SendAsync("PlayerAnswered", playerName);
        }

        // NextQuestion toggles reveal/advance:
        // - If answers not revealed => reveal results.
        // - If already revealed => advance to next question.
        public async Task NextQuestion(string lobbyCode)
        {
            Console.WriteLine("Next Question Pressed");

            // Load lobby and related data early so we can initialize state if needed
            var lobby = await _context.Lobbies
                .Include(l => l.Quiz)
                    .ThenInclude(q => q.QuizMaster)
                .Include(l => l.Quiz)
                    .ThenInclude(q => q.Questions)
                .FirstOrDefaultAsync(l => l.LobbyCode == lobbyCode);

            var callerName = Context.User?.Identity?.Name;

            // Attempt to get runtime state; if missing and caller is quizmaster try to initialize (convenience)
            if (!LobbyStates.TryGetValue(lobbyCode, out var state))
            {
                if (lobby != null && lobby.Quiz != null && lobby.Quiz.QuizMaster != null
                    && !string.IsNullOrEmpty(callerName) && lobby.Quiz.QuizMaster.Name == callerName)
                {
                    // Initialize runtime state (same as StartQuiz) so quizmaster can start by pressing Next
                    var questions = lobby.Quiz.Questions?.OrderBy(q => q.QuestionNumber).ToList() ?? new List<Question>();
                    state = new LobbyState
                    {
                        QuizName = lobby.Quiz.QuizName,
                        Questions = questions,
                        CurrentIndex = 0,
                        AnswersForCurrent = new Dictionary<string, int?>(),
                        HasRevealedAnswers = false,
                        Scores = new Dictionary<string, int?>()
                    };

                    if (LobbyPlayers.TryGetValue(lobbyCode, out var players))
                    {
                        var qm = lobby.Quiz.QuizMaster.Name ?? "";
                        state.Players = players
                            .Where(p => !string.Equals(p, qm, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }
                    else
                    {
                        state.Players = new List<string>();
                    }

                    foreach (var p in state.Players)
                    {
                        if (!state.Scores.ContainsKey(p))
                            state.Scores[p] = 0;
                    }

                    LobbyStates[lobbyCode] = state;

                    // Mark lobby active (optional)
                    lobby.IsActive = true;
                    await _context.SaveChangesAsync();

                    // Notify clients and send first question
                    await Clients.Group(lobbyCode).SendAsync("QuizStarted", lobbyCode);
                    await SendCurrentQuestionToGroup(lobbyCode);

                    // Also send the question and counts directly to the caller to avoid a race
                    // where the caller hasn't joined the group yet and would miss the group broadcast.
                    var q = state.GetCurrentQuestion();
                    if (q != null)
                    {
                        var payload = BuildQuestionPayload(state, q);
                        await Clients.Caller.SendAsync("ShowQuestion", payload);
                        await Clients.Caller.SendAsync("AnswersCountUpdated", state.AnswersForCurrent.Count, state.Players.Count);
                    }

                    return;
                }

                // No runtime state and cannot initialize: nothing to do
                return;
            }

            if (lobby == null || lobby.Quiz?.QuizMaster == null)
                return;

            if (!string.IsNullOrEmpty(callerName) && lobby.Quiz.QuizMaster.Name != callerName)
            {
                // Not quizmaster
                return;
            }

            // If answers not revealed: reveal results for current question
            if (!state.HasRevealedAnswers)
            {
                List<string> correctPlayers;
                List<string> incorrectPlayers;
                List<string> noAnswerPlayers;
                Dictionary<string, int?> answersMap;

                lock (state.Lock)
                {
                    var currentQuestion = state.GetCurrentQuestion();
                    const int correctIndex = 0; // server ordering: correct is index 0
                    answersMap = new Dictionary<string, int?>(state.AnswersForCurrent);

                    correctPlayers = state.Players
                        .Where(p => state.AnswersForCurrent.ContainsKey(p) && state.AnswersForCurrent[p] == correctIndex)
                        .ToList();

                    incorrectPlayers = state.Players
                        .Where(p => state.AnswersForCurrent.ContainsKey(p) && state.AnswersForCurrent[p] != correctIndex)
                        .ToList();

                    noAnswerPlayers = state.Players
                        .Where(p => !state.AnswersForCurrent.ContainsKey(p))
                        .ToList();

                    // Update scores for correct players
                    foreach (var cp in correctPlayers)
                    {
                        if (!state.Scores.ContainsKey(cp))
                            state.Scores[cp] = 0;
                        state.Scores[cp] = (state.Scores[cp] ?? 0) + 1;
                    }

                    state.HasRevealedAnswers = true;
                }

                // Broadcast results payload (used by clients to color answers)
                var resultsPayload = new
                {
                    questionId = lobby.Quiz.Questions?.OrderBy(q => q.QuestionNumber).ElementAtOrDefault(state.CurrentIndex)?.QuestionId ?? 0,
                    questionText = lobby.Quiz.Questions?.OrderBy(q => q.QuestionNumber).ElementAtOrDefault(state.CurrentIndex)?.QuestionText ?? "",
                    correctIndex = 0,
                    answersMap = answersMap,
                    correctPlayers,
                    incorrectPlayers,
                    noAnswerPlayers
                };

                await Clients.Group(lobbyCode).SendAsync("ShowResults", resultsPayload);
                // Also broadcast same overview as before for convenience
                var overview = new
                {
                    questionText = resultsPayload.questionText,
                    correctPlayers,
                    incorrectPlayers,
                    noAnswerPlayers
                };
                await Clients.Group(lobbyCode).SendAsync("ShowOverview", overview);

                return;
            }

            // If answers already revealed => advance to next question
            lock (state.Lock)
            {
                state.CurrentIndex++;
                state.AnswersForCurrent = new Dictionary<string, int?>();
                state.HasRevealedAnswers = false;
            }

            if (state.CurrentIndex < state.Questions.Count)
            {
                await SendCurrentQuestionToGroup(lobbyCode);
            }
            else
            {
                // No more questions: compute final leaderboard and finish
                // Build leaderboard (name + score)
                List<(string Name, int Score)> leaderboard;
                lock (state.Lock)
                {
                    // Ensure everyone in Players has a score entry
                    foreach (var p in state.Players)
                    {
                        if (!state.Scores.ContainsKey(p))
                            state.Scores[p] = 0;
                    }

                    leaderboard = state.Scores.Select(kv => (Name: kv.Key, Score: kv.Value ?? 0))
                                              .OrderByDescending(x => x.Score)
                                              .ThenBy(x => x.Name)
                                              .ToList();
                }

                // Compute dense ranks
                var leaderboardWithRank = new List<object>();
                int currentRank = 0;
                int? prevScore = null;
                foreach (var item in leaderboard)
                {
                    if (prevScore == null || item.Score != prevScore)
                    {
                        currentRank++;
                        prevScore = item.Score;
                    }
                    leaderboardWithRank.Add(new { name = item.Name, score = item.Score, rank = currentRank });
                }

                var finalPayload = new
                {
                    leaderboard = leaderboardWithRank,
                    totalPlayers = leaderboard.Count,
                    totalQuestions = state.Questions?.Count ?? 0
                };

                // Send final scores to group
                await Clients.Group(lobbyCode).SendAsync("ShowScores", finalPayload);

                // Also mark lobby inactive, notify clients to close lobby and cleanup runtime state
                if (lobby != null)
                {
                    lobby.IsActive = false;
                    await _context.SaveChangesAsync();

                    // Notify all players to leave the lobby (same event used in Lobby.cshtml)
                    await Clients.Group(lobbyCode).SendAsync("LobbyClosed");
                }

                // Optionally also notify that quiz finished (keeps backward compatibility)
                await Clients.Group(lobbyCode).SendAsync("QuizFinished");

                // Cleanup runtime state
                LobbyStates.TryRemove(lobbyCode, out _);
            }
        }

        // Returns the current question payload or null
        public Task<object?> GetCurrentQuestion(string lobbyCode)
        {
            if (!LobbyStates.TryGetValue(lobbyCode, out var state))
                return Task.FromResult<object?>(null);

            var q = state.GetCurrentQuestion();
            if (q == null)
                return Task.FromResult<object?>(null);

            var payload = BuildQuestionPayload(state, q);
            return Task.FromResult<object?>(payload);
        }

        // helper to send current question for a lobby (with counts)
        private Task SendCurrentQuestionToGroup(string lobbyCode)
        {
            if (!LobbyStates.TryGetValue(lobbyCode, out var state))
                return Task.CompletedTask;

            var q = state.GetCurrentQuestion();
            if (q == null)
                return Task.CompletedTask;

            var payload = BuildQuestionPayload(state, q);

            return Clients.Group(lobbyCode).SendAsync("ShowQuestion", payload);
        }

        private object BuildQuestionPayload(LobbyState state, Question q)
        {
            var answers = new List<string>();
            if (!string.IsNullOrEmpty(q.CorrectAnswer)) answers.Add(q.CorrectAnswer);
            if (!string.IsNullOrEmpty(q.FalseAnswer1)) answers.Add(q.FalseAnswer1);
            if (!string.IsNullOrEmpty(q.FalseAnswer2)) answers.Add(q.FalseAnswer2);
            if (!string.IsNullOrEmpty(q.FalseAnswer3)) answers.Add(q.FalseAnswer3);

            return new
            {
                quizName = state.QuizName,
                currentQuestionText = q.QuestionText,
                answers = answers,
                questionId = q.QuestionId,
                totalPlayers = state.Players.Count,
                answeredCount = state.AnswersForCurrent.Count
            };
        }

        // Internal runtime state per lobby
        private class LobbyState
        {
            public string QuizName { get; set; }
            public List<Question> Questions { get; set; } = new();
            public int CurrentIndex { get; set; } = 0; // 0-based
            public List<string> Players { get; set; } = new();
            public Dictionary<string, int?> AnswersForCurrent { get; set; } = new();
            public Dictionary<string, int?> Scores { get; set; } = new();
            public bool HasRevealedAnswers { get; set; } = false;
            public object Lock { get; } = new();

            public Question GetCurrentQuestion()
            {
                if (Questions == null || CurrentIndex < 0 || CurrentIndex >= Questions.Count)
                    return null;
                return Questions[CurrentIndex];
            }
        }
    }
}