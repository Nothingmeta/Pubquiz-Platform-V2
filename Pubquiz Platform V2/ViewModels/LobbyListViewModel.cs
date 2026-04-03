namespace Pubquiz_Platform_V2.ViewModels
{
    public class LobbyListViewModel
    {
        public List<LobbyListItemViewModel> Lobbies { get; set; } = new();
        public List<QuizViewModel> AvailableQuizzes { get; set; } = new(); // Add this

    }

    public class LobbyListItemViewModel
    {
        public string LobbyCode { get; set; }
        public string QuizName { get; set; }
        public string QuizMasterName { get; set; }
    }
}