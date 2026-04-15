using System.ComponentModel.DataAnnotations;

namespace Pubquiz_Platform_V2.ViewModels
{
    public class EnableTwoFactorViewModel
    {
        public string QrCodeDataUri { get; set; } = string.Empty;
        public string ManualEntryKey { get; set; } = string.Empty;
        public string TempSecret { get; set; } = string.Empty;
    }

    public class TwoFactorVerifyViewModel
    {
        [Required]
        [Display(Name = "Authentication code or recovery code")]
        public string Code { get; set; } = null!;

        public string PreAuthToken { get; set; } = string.Empty;
    }

    public class TwoFactorSetupViewModel
    {
        public string Secret { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}