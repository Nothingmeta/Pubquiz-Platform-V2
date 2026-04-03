namespace Pubquiz_Platform.Data.Entities
{
    public class Question
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string CorrectAnswer { get; set; }
        public string FalseAnswer1 { get; set; }
        public string FalseAnswer2 { get; set; }
        public string FalseAnswer3 { get; set; }

        // Relatie met Quiz
        public int QuizId { get; set; }
        public Quiz Quiz { get; set; }

        // Order within the quiz
        public int QuestionNumber { get; set; }
    }
}
