namespace Pubquiz_Platform_V2.ViewModels
{
    public class QuizEditViewModel
    {
        public int QuizId { get; set; }
        public string QuizName { get; set; }
        public string QuizSlug { get; set; }
        public List<QuestionEditViewModel> Questions { get; set; }
        public int CurrentQuestionIndex { get; set; }
        public QuestionEditViewModel CurrentQuestion { get; set; } // <-- Add this
        public int TotalQuestions { get; set; }


    }

}
