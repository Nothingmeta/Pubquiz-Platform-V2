using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pubquiz_Platform.Data;
using Pubquiz_Platform.Data.Entities;
using Pubquiz_Platform_V2.ViewModels;

namespace Pubquiz_Platform_V2.Controllers
{
    [Authorize]
    public class QuizController : Controller
    {
        private readonly ApplicationDbContext _context;
        public QuizController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Manage()
        {
            // Get current user's ID from claims
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null)
                return Unauthorized();

            int quizMasterId = int.Parse(userIdClaim.Value);

            // Query quizzes for this quizmaster
            var quizzes = _context.Quizzes
                .Where(q => q.QuizMasterId == quizMasterId)
                .Select(q => new QuizViewModel
                {
                    Id = q.QuizId,
                    QuizName = q.QuizName,
                    QuizSlug = q.QuizSlug
                })
                .ToList();

            return View(quizzes);
        }


        [HttpPost]
        public IActionResult CreateQuick([FromBody] QuizCreateViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.QuizName))
                return BadRequest();

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null)
                return Unauthorized();

            int quizMasterId = int.Parse(userIdClaim.Value);

            var slug = GenerateSlug(model.QuizName.Trim());

            // Check for uniqueness per quizmaster
            bool exists = _context.Quizzes.Any(q =>
                q.QuizMasterId == quizMasterId &&
                q.QuizSlug == slug);

            if (exists)
                return Conflict("Er bestaat al een quiz met deze naam.");

            var quiz = new Quiz
            {
                QuizName = model.QuizName.Trim(),
                QuizSlug = slug,
                QuizMasterId = quizMasterId
            };
            _context.Quizzes.Add(quiz);
            _context.SaveChanges();

            // Add a default question to the new quiz
            var question = new Question
            {
                QuizId = quiz.QuizId,
                QuestionText = "Nieuwe vraag",
                CorrectAnswer = "Standaard antwoord",
                FalseAnswer1 = "Standaard fout antwoord 1",
                FalseAnswer2 = "Standaard fout antwoord 2",
                FalseAnswer3 = "Standaard fout antwoord 3",
                QuestionNumber = 1
            };
            _context.Questions.Add(question);
            _context.SaveChanges();

            return Json(new { quizId = quiz.QuizId, quizSlug = quiz.QuizSlug });
        }

        [HttpGet("Quiz/Edit/{quizSlug}")]
        public IActionResult Edit(string quizSlug, int question = 0)
        {
            // Get current user's ID from claims
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null)
                return Unauthorized();

            int quizMasterId = int.Parse(userIdClaim.Value);

            // Find quiz and ensure ownership
            var quiz = _context.Quizzes
                .Include(q => q.Questions.OrderBy(qn => qn.QuestionNumber))
                .FirstOrDefault(q => q.QuizSlug == quizSlug && q.QuizMasterId == quizMasterId);

            if (quiz == null)
                return NotFound();

            var questions = quiz.Questions.OrderBy(q => q.QuestionNumber).ToList();

            if (questions.Count == 0)
                return NotFound("No questions found for this quiz.");

            // Clamp question index
            int questionIndex = Math.Clamp(question, 0, questions.Count - 1);
            var currentQuestion = questions[questionIndex];

            var model = new QuizEditViewModel
            {
                QuizId = quiz.QuizId,
                QuizName = quiz.QuizName,
                QuizSlug = quiz.QuizSlug,
                Questions = questions.Select(q => new QuestionEditViewModel
                {
                    QuestionId = q.QuestionId,
                    QuestionText = q.QuestionText,
                    Answers = new List<string>
        {
            q.CorrectAnswer,
            q.FalseAnswer1,
            q.FalseAnswer2,
            q.FalseAnswer3
        },
                    CorrectAnswerIndex = 0,
                    QuestionNumber = q.QuestionNumber
                }).ToList(),
                CurrentQuestionIndex = questionIndex,
                TotalQuestions = questions.Count,
                CurrentQuestion = new QuestionEditViewModel()
            }; 

            // Set correct answer index for each question
            for (int i = 0; i < model.Questions.Count; i++)
            {
                var q = questions[i];
                var answers = new List<string>
        {
            q.CorrectAnswer,
            q.FalseAnswer1,
            q.FalseAnswer2,
            q.FalseAnswer3
        };

                int correctIndex = 0; // CorrectAnswer is always at index 0
                model.Questions[i].CorrectAnswerIndex = correctIndex;
                model.Questions[i].Answers = answers;
            }

            model.CurrentQuestion = model.Questions[questionIndex];

            return View(model);
        }


        [HttpPost("Quiz/Edit/{quizSlug}")]
        [IgnoreAntiforgeryToken]
        public IActionResult Edit(string quizSlug, QuizEditViewModel model, string action)
        {
            // Get current user's ID from claims
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null)
                return Unauthorized();

            int quizMasterId = int.Parse(userIdClaim.Value);

            // Find quiz and ensure ownership
            var quiz = _context.Quizzes
                .Include(q => q.Questions.OrderBy(qn => qn.QuestionNumber))
                .FirstOrDefault(q => q.QuizSlug == quizSlug && q.QuizMasterId == quizMasterId);

            if (quiz == null)
                return NotFound();

            var questions = quiz.Questions.OrderBy(q => q.QuestionNumber).ToList();
            int questionIndex = Math.Clamp(model.CurrentQuestionIndex, 0, questions.Count - 1);
            foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
            {
                Console.WriteLine(error.ErrorMessage);
            }
            // Save/update the current question
            if (ModelState.IsValid && model.CurrentQuestion != null)
            {
                var qvm = model.CurrentQuestion;
                Question? questionEntity = null;

                if (qvm.QuestionId.HasValue)
                {
                    questionEntity = questions.FirstOrDefault(q => q.QuestionId == qvm.QuestionId.Value);
                }

                if (questionEntity == null && questionIndex < questions.Count)
                {
                    questionEntity = questions[questionIndex];
                }

                if (questionEntity != null)
                {
                    questionEntity.QuestionText = qvm.QuestionText;
                    if (qvm.Answers != null && qvm.Answers.Count == 4)
                    {
                        var correctAnswer = qvm.Answers[qvm.CorrectAnswerIndex];
                        questionEntity.CorrectAnswer = string.IsNullOrWhiteSpace(correctAnswer) ? "Standaard antwoord" : correctAnswer;
                        var falseAnswers = qvm.Answers
                            .Where((a, idx) => idx != qvm.CorrectAnswerIndex)
                            .ToList();
                        questionEntity.FalseAnswer1 = falseAnswers.Count > 0 && !string.IsNullOrWhiteSpace(falseAnswers[0]) ? falseAnswers[0] : "Standaard fout antwoord 1";
                        questionEntity.FalseAnswer2 = falseAnswers.Count > 1 && !string.IsNullOrWhiteSpace(falseAnswers[1]) ? falseAnswers[1] : "Standaard fout antwoord 2";
                        questionEntity.FalseAnswer3 = falseAnswers.Count > 2 && !string.IsNullOrWhiteSpace(falseAnswers[2]) ? falseAnswers[2] : "Standaard fout antwoord 3";
                    }
                    else
                    {
                        // Fallback if answers are missing or incomplete
                        questionEntity.CorrectAnswer = "Standaard antwoord";
                        questionEntity.FalseAnswer1 = "Standaard fout antwoord 1";
                        questionEntity.FalseAnswer2 = "Standaard fout antwoord 2";
                        questionEntity.FalseAnswer3 = "Standaard fout antwoord 3";
                    }
                    _context.SaveChanges();
                }
            }

            // Navigation logic
            if (action == "next")
            {
                if (questionIndex < questions.Count - 1)
                {
                    questionIndex++;
                }
                else
                {
                    // Only create a new question if on the last one and pressing next
                    int nextNumber = questions.Count > 0 ? questions.Max(q => q.QuestionNumber) + 1 : 1;
                    var newQuestion = new Question
                    {
                        QuizId = quiz.QuizId,
                        QuestionText = "Nieuwe vraag",
                        CorrectAnswer = "Standaard antwoord",
                        FalseAnswer1 = "Standaard fout antwoord 1",
                        FalseAnswer2 = "Standaard fout antwoord 2",
                        FalseAnswer3 = "Standaard fout antwoord 3",
                        QuestionNumber = nextNumber
                    };
                    _context.Questions.Add(newQuestion);
                    _context.SaveChanges();
                    questionIndex = questions.Count; // index of the new question
                }
            }
            else if (action == "prev" && questionIndex > 0)
            {
                questionIndex--;
            }

            // Redirect to GET to avoid reposts
            return RedirectToAction("Edit", new { quizSlug = quizSlug, question = questionIndex });
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            // Get current user's ID from claims
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null)
                return Unauthorized();

            int quizMasterId = int.Parse(userIdClaim.Value);

            // Find the quiz and ensure it belongs to the current quizmaster
            var quiz = _context.Quizzes.FirstOrDefault(q => q.QuizId == id && q.QuizMasterId == quizMasterId);
            if (quiz == null)
                return NotFound();

            _context.Quizzes.Remove(quiz);
            _context.SaveChanges();

            // Redirect back to the manage page
            return RedirectToAction("Manage");
        }

        public static string GenerateSlug(string phrase)
        {
            string str = phrase.ToLowerInvariant();
            str = System.Text.RegularExpressions.Regex.Replace(str, @"\s+", "-"); // spaces to hyphens
            str = System.Text.RegularExpressions.Regex.Replace(str, @"[^a-z0-9\-]", ""); // remove invalid chars
            str = System.Text.RegularExpressions.Regex.Replace(str, @"-+", "-"); // multiple hyphens to one
            str = str.Trim('-');
            return str;
        }

        public IActionResult LobbyOverview()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null)
                return Unauthorized();

            int quizMasterId = int.Parse(userIdClaim.Value);

            var lobbies = _context.Lobbies
                .Where(l => l.IsActive)
                .Include(l => l.Quiz)
                .Include(l => l.Quiz.QuizMaster)
                .Select(l => new LobbyListItemViewModel
                {
                    LobbyCode = l.LobbyCode,
                    QuizName = l.Quiz.QuizName,
                    QuizMasterName = l.Quiz.QuizMaster != null ? l.Quiz.QuizMaster.Name : "Onbekend"
                })
                .ToList();

            var availableQuizzes = _context.Quizzes
                .Where(q => q.QuizMasterId == quizMasterId)
                .Select(q => new QuizViewModel
                {
                    Id = q.QuizId,
                    QuizName = q.QuizName,
                    QuizSlug = q.QuizSlug
                })
                .ToList();

            var model = new LobbyListViewModel
            {
                Lobbies = lobbies,
                AvailableQuizzes = availableQuizzes
            };

            return View("LobbyOverview", model);
        }

        [HttpGet]
        public IActionResult Lobby(string lobbyCode)
        {
            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                TempData["Error"] = "Geen lobbycode opgegeven.";
                return RedirectToAction("LobbyOverview");
            }

            var lobby = _context.Lobbies
                .Include(l => l.Quiz)
                    .ThenInclude(q => q.QuizMaster)
                .FirstOrDefault(l => l.LobbyCode == lobbyCode && l.IsActive);

            if (lobby == null)
            {
                TempData["Error"] = "Lobby niet gevonden.";
                return RedirectToAction("LobbyOverview");
            }

            var model = new LobbyViewModel
            {
                LobbyCode = lobby.LobbyCode,
                QuizName = lobby.Quiz.QuizName,
                QuizMasterName = lobby.Quiz.QuizMaster?.Name ?? "Onbekend",
                QuizMasterId = lobby.Quiz.QuizMasterId,
                Players = new List<string>() // SignalR will populate this
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult CreateLobby(int quizId)
        {
            // Get current user's ID from claims
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null)
                return Unauthorized();

            int quizMasterId = int.Parse(userIdClaim.Value);

            // Ensure the quiz belongs to the quizmaster
            var quiz = _context.Quizzes
                .FirstOrDefault(q => q.QuizId == quizId && q.QuizMasterId == quizMasterId);

            if (quiz == null)
                return NotFound("Quiz niet gevonden of geen toegang.");

            // Generate a unique lobby code (6 uppercase letters/numbers)
            string lobbyCode;
            do
            {
                lobbyCode = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
            } while (_context.Lobbies.Any(l => l.LobbyCode == lobbyCode));

            // Create and save the lobby
            var lobby = new Lobby
            {
                LobbyCode = lobbyCode,
                QuizId = quiz.QuizId,
                IsActive = true,
                Players = new List<User>() // Optionally add the quizmaster as the first player
            };
            _context.Lobbies.Add(lobby);
            _context.SaveChanges();

            // Redirect to the lobby page
            return RedirectToAction("Lobby", new { lobbyCode = lobby.LobbyCode });
        }

        [HttpGet]
        public async Task<IActionResult> Play(string lobbyCode)
        {
            if (string.IsNullOrEmpty(lobbyCode))
                return NotFound();

            var lobby = await _context.Lobbies
                .Include(l => l.Quiz)
                    .ThenInclude(q => q.Questions)
                .Include(l => l.Quiz)
                    .ThenInclude(q => q.QuizMaster)
                .FirstOrDefaultAsync(l => l.LobbyCode == lobbyCode);

            if (lobby == null)
                return NotFound();

            // Get current user's ID from JWT claims (not just by name)
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            int currentUserId = 0;
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var id))
            {
                currentUserId = id;
            }

            // Check if current user is the quizmaster of THIS specific lobby
            bool isQuizMasterOfThisLobby = currentUserId > 0 && lobby.Quiz?.QuizMasterId == currentUserId;

            var questions = (lobby.Quiz?.Questions ?? new List<Question>()).OrderBy(q => q.QuestionNumber).ToList();

            var model = new QuizPlayFullViewModel
            {
                LobbyCode = lobbyCode,
                QuizName = lobby.Quiz?.QuizName ?? "Quiz",
                QuizMasterId = lobby.Quiz?.QuizMasterId ?? 0,
                IsQuizMasterOfThisLobby = isQuizMasterOfThisLobby,
                CurrentQuestionIndex = 0,
                Questions = questions.Select(q =>
                {
                    var answers = new List<string>
                    {
                        q.CorrectAnswer,
                        q.FalseAnswer1,
                        q.FalseAnswer2,
                        q.FalseAnswer3
                    };

                    // Only show correct answer to the actual quizmaster of this lobby
                    var qvm = new QuestionPlayViewModel
                    {
                        QuestionId = q.QuestionId,
                        QuestionText = q.QuestionText,
                        Answers = answers,
                        CorrectAnswerIndex = isQuizMasterOfThisLobby ? 0 : (int?)null
                    };
                    return qvm;
                }).ToList()
            };

            return View(model);
        }
    }
}