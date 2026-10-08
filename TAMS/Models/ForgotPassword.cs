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

        public bool AccountVerified { get; set; }
    }
}