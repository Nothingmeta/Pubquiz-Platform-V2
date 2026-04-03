namespace Pubquiz_Platform_V2.ViewModels
{
    public class QuestionPlayViewModel
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; }
        public List<string> Answers { get; set; } = new();
        // Only populated for quizmaster (null for regular players)
        public int? CorrectAnswerIndex { get; set; }
    }
}