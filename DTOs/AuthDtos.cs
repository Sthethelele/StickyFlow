using System.ComponentModel.DataAnnotations;

namespace StickyFlow.Dtos
{
    public class StickyFlowRegisterRequest
    {
        [Required(ErrorMessage = "FullName is required")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; }

        public string? TimeZone { get; set; }
    }

    public class StickyFlowLoginRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; }
    }

    public class StickyFlowForgotPasswordRequest
    {
        [Required, EmailAddress] public string Email { get; set; }
    }

    public class StickyFlowResetPasswordRequest
    {
        [Required] public string Token { get; set; }
        [Required, EmailAddress] public string Email { get; set; }
        [Required, StringLength(100, MinimumLength = 6)] public string NewPassword { get; set; }
    }
}