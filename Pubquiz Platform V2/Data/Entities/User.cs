namespace Pubquiz_Platform.Data.Entities
{
    public class User
    {
        public int UserId { get; set; }

        public string Email { get; set; }
        public string Name { get; set; }
        public string Password { get; set; } // In productie: altijd hashen!
        public string Role { get; set; } // speler of quizmaster

        // Navigatie
        public ICollection<Quiz> Quizzes { get; set; }
    }
}
