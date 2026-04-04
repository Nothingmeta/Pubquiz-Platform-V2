using System.ComponentModel.DataAnnotations;

namespace Pubquiz_Platform_V2.ViewModels
{
    public class EnableTwoFactorViewModel
    {
        public string QrCodeDataUri { get; set; } = null!;
        public string ManualEntryKey { get; set; } = null!;
    }

    public class TwoFactorVerifyViewModel
    {
        [Required]
        [Display(Name = "Authentication code or recovery code")]
        public string Code { get; set; } = null!;
    }
}