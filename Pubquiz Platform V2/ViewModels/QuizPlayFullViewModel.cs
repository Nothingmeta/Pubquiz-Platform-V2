namespace Pubquiz_Platform_V2.ViewModels
{
    public class QuizPlayFullViewModel
    {
        public string QuizName { get; set; }
        public string LobbyCode { get; set; }
        public List<QuestionPlayViewModel> Questions { get; set; } = new();
        // client can use this to track which question to display
        public int CurrentQuestionIndex { get; set; } = 0;
    }
}