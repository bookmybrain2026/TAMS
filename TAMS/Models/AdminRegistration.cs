using System.ComponentModel.DataAnnotations;

namespace TAMS.Models
{
    public class AdminRegistration
    {
        [Required]
        [StringLength(50)]
        public string FName { get; set; } = "";

        [Required]
        [StringLength(50)]
        public string LName { get; set; } = "";

        [Required]
        [StringLength(13)]
        public string PhoneNumber { get; set; } = "";

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = "";

        [Required]
        [StringLength(100)]
        public string Username { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = "";
    }
}
