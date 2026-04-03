using Pubquiz_Platform.Data.Entities;

namespace Pubquiz_Platform_V2.Data.Entities
{
    public class LobbyState
    {
        public string QuizName { get; set; }
        public List<Question> Questions { get; set; } = new();
        public int CurrentIndex { get; set; } = 0; // 0-based
        public List<string> Players { get; set; } = new();
        public Dictionary<string, int?> AnswersForCurrent { get; set; } = new();
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