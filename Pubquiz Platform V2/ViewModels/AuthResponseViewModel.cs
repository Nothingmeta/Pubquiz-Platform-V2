namespace Pubquiz_Platform_V2.ViewModels
{
    public class AuthResponseViewModel
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string AccessToken { get; set; }
        public string PreAuthToken { get; set; }
        public int UserId { get; set; }
    }

    public class TwoFactorResponseViewModel
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string AccessToken { get; set; }
        public List<string> RecoveryCodes { get; set; }
    }
}