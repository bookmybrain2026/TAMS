using System.ComponentModel.DataAnnotations;

namespace TAMS.Models
{
    public class TutorRegistration
    {
        [Required]
        [MaxLength(50)]
        public string FName { get; set; }

        [Required]
        [MaxLength(50)]
        public string LName { get; set; }

        [Required(ErrorMessage = "Please select a subject.")]
        public string Subject { get; set; }

        [Required]
        [StringLength(12)]
        [RegularExpression(@"^\d{3}-\d{3}-\d{4}$", ErrorMessage = "Phone number must be in the format 123-456-7890.")]
        public string PhoneNumber { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; }

        [Required]
        [StringLength(100)]
        public string Username { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 4)]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password")]
        public string ConfirmPassword { get; set; }
    }
}
