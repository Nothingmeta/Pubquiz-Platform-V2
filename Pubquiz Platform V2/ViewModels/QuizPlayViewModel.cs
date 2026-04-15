namespace Pubquiz_Platform_V2.ViewModels
{
    public class QuizPlayViewModel
    {
        public string QuizName { get; set; } = string.Empty;
        public string CurrentQuestionText { get; set; } = string.Empty;
        public List<string> Answers { get; set; } = new();
        public int QuestionId { get; set; }
        public string LobbyCode { get; set; } = string.Empty;

        // Overview: who got it right, wrong, or did not answer
        public List<string> CorrectPlayers { get; set; } = new();
        public List<string> IncorrectPlayers { get; set; } = new();
        public List<string> NoAnswerPlayers { get; set; } = new();
    }
}
