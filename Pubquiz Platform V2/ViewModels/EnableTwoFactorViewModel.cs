using System.ComponentModel.DataAnnotations;

namespace Pubquiz_Platform_V2.ViewModels
{
    public class EnableTwoFactorViewModel
    {
        public string QrCodeDataUri { get; set; }
        public string ManualEntryKey { get; set; }
        public string TempSecret { get; set; }
    }

    public class TwoFactorVerifyViewModel
    {
        [Required]
        [Display(Name = "Authentication code or recovery code")]
        public string Code { get; set; } = null!;
        
        public string PreAuthToken { get; set; }
    }

    public class TwoFactorSetupViewModel
    {
        public string Secret { get; set; }
        public string Code { get; set; }
    }
}