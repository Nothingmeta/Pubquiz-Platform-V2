namespace Pubquiz_Platform_V2.ViewModels
{
    public class QuizViewModel
    {
        public int Id { get; set; }
        public string QuizName { get; set; }
        public string QuizSlug { get; set; } // Slug for URL-friendly quiz name
    }
}
