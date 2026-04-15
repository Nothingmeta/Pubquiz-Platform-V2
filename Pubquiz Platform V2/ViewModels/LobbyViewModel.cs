namespace Pubquiz_Platform_V2.ViewModels
{
    public class LobbyViewModel
    {
        public string LobbyCode { get; set; } = string.Empty;
        public string QuizName { get; set; } = string.Empty;
        public string QuizMasterName { get; set; } = string.Empty;
        public int QuizMasterId { get; set; }
        public List<string> Players { get; set; } = new();
    }
}