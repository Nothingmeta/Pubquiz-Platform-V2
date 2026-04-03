using System.Collections.Generic;
namespace Pubquiz_Platform.Data.Entities
{
    public class Lobby
    {
        public int LobbyId { get; set; }
        public string LobbyCode { get; set; }
        public bool IsActive { get; set; } = true;

        // Foreign key to Quiz
        public int QuizId { get; set; }
        public Quiz Quiz { get; set; }

        // Navigation property for players (optional, if you have a Player entity)
        public List<User> Players { get; set; } = new();
    }
}