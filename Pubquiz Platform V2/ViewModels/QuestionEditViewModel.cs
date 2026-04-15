using System.ComponentModel.DataAnnotations;

namespace Pubquiz_Platform_V2.ViewModels
{
    public class QuestionEditViewModel
    {
        public int? QuestionId { get; set; }

        [Required(ErrorMessage = "Question text is required.")]
        public string QuestionText { get; set; } = string.Empty;

        public List<string> Answers { get; set; } = new() { "", "", "", "" };

        public int CorrectAnswerIndex { get; set; }
        public int QuestionNumber { get; set; }
    }
}
