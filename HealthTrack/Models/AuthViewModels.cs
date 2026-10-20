using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models
{
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Email or Username")]
        public string? EmailOrUsername { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "Full name")]
        public string? FullName { get; set; }

        [Required]
        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string? Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm password")]
        public string? ConfirmPassword { get; set; }

        [Required(ErrorMessage = "You must agree to the terms to create an account")]
        [Display(Name = "I agree to the terms and privacy policy")]
        public bool Consent { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string? Email { get; set; }
    }

    public class ResetPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [MinLength(6)]
        public string? Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password")]
        [Display(Name = "Confirm password")]
        public string? ConfirmPassword { get; set; }

        // Token will be supplied by backend as query param in real flows
        public string? Token { get; set; }
    }

    public class VerifyEmailViewModel
    {
        public string? Email { get; set; }
        public bool IsVerified { get; set; }
    }
}
