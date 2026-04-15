namespace Pubquiz_Platform_V2.ViewModels
{
    public class AuthResponseViewModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string PreAuthToken { get; set; } = string.Empty;
        public int UserId { get; set; }
    }

    public class TwoFactorResponseViewModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public List<string> RecoveryCodes { get; set; } = new();
    }
}