namespace Pubquiz_Platform.Data.Entities
{
    public class Quiz
    {
        public int QuizId { get; set; }
        public string QuizName { get; set; }
        public string QuizSlug { get; set; } // Add this

        // Relatie met User (Quizmaster)
        public int QuizMasterId { get; set; }
        public User QuizMaster { get; set; }

        // Navigatie naar vragen
        public ICollection<Question> Questions { get; set; }
    }
}
