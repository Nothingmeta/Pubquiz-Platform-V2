namespace Pubquiz_Platform_V2.ViewModels
{
    public class LobbyViewModel
    {
        public string LobbyCode { get; set; }
        public string QuizName { get; set; }
        public string QuizMasterName { get; set; }
        public List<string> Players { get; set; } = new();
    }
}