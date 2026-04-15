namespace Pubquiz_Platform_V2.ViewModels
{
    public class QuizEditViewModel
    {
        public int QuizId { get; set; }
        public string QuizName { get; set; } = string.Empty;
        public string QuizSlug { get; set; } = string.Empty;
        public List<QuestionEditViewModel> Questions { get; set; } = new();
        public int CurrentQuestionIndex { get; set; }
        public QuestionEditViewModel CurrentQuestion { get; set; } = new();
        public int TotalQuestions { get; set; }
    }
}
