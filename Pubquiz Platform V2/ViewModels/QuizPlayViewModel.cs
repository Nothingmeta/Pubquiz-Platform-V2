namespace Pubquiz_Platform_V2.ViewModels
{
    public class QuizPlayViewModel
    {
        public string QuizName { get; set; }
        public string CurrentQuestionText { get; set; }
        public List<string> Answers { get; set; }
        public int QuestionId { get; set; }
        public string LobbyCode { get; set; }

        // Overview: who got it right, wrong, or did not answer
        public List<string> CorrectPlayers { get; set; } = new();
        public List<string> IncorrectPlayers { get; set; } = new();
        public List<string> NoAnswerPlayers { get; set; } = new();
    }
}
