namespace Pubquiz_Platform_V2.ViewModels
{
    public class LobbyListViewModel
    {
        public List<LobbyListItemViewModel> Lobbies { get; set; } = new();
        public List<QuizViewModel> AvailableQuizzes { get; set; } = new();
    }

    public class LobbyListItemViewModel
    {
        public string LobbyCode { get; set; } = string.Empty;
        public string QuizName { get; set; } = string.Empty;
        public string QuizMasterName { get; set; } = string.Empty;
    }
}