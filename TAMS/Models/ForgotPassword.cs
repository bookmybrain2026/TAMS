using System.ComponentModel.DataAnnotations;

namespace TAMS.Models
{
    public class ForgotPassword
    {
        [Required(ErrorMessage = "Username is required.")]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "New Password is required.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Confirm Password is required.")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }

        public bool ShowPasswordFields { get; set; } = false;


        //added
        public string RecoveryCode { get; set; }

        public bool ShowRecoveryCodeField { get; set; }

        public bool CodeVerified { get; set; }
    }
}