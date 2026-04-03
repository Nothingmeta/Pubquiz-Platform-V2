using System.ComponentModel.DataAnnotations;

namespace Pubquiz_Platform_V2.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Naam is verplicht.")]
        public string Name { get; set; }
        [Required(ErrorMessage = "E-mailadres is verplicht.")]
        [EmailAddress(ErrorMessage = "Voer een geldig e-mailadres in.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Wachtwoord is verplicht.")]
        [StringLength(128, MinimumLength = 12, ErrorMessage = "Het wachtwoord moet minimaal 12 en maximaal 128 tekens bevatten.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Rol is verplicht.")]
        [RegularExpression("speler|quizmaster", ErrorMessage = "Rol moet 'speler' of 'quizmaster' zijn.")]
        public string Role { get; set; }
    }
}
