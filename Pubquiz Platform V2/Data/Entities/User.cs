namespace Pubquiz_Platform.Data.Entities
{
    public class User
    {
        public int UserId { get; set; }

        public string Email { get; set; }
        public string Name { get; set; }
        public string Password { get; set; } // In productie: altijd hashen!
        public string Role { get; set; } // speler of quizmaster


        public bool IsTwoFactorEnabled { get; set; } = false;
        // Stores the secret protected with IDataProtection
        public string? ProtectedTwoFactorSecret { get; set; }

        // Recovery codes (protected)
        public string? ProtectedRecoveryCodes { get; set; }

        // Simple rate-limiting / lockout
        public int TwoFactorFailedCount { get; set; } = 0;
        public DateTimeOffset? TwoFactorLockoutEnd { get; set; }

        // Navigatie
        public ICollection<Quiz> Quizzes { get; set; }
    }
}
