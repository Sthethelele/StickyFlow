using System.ComponentModel.DataAnnotations;

namespace StickyFlow.Dtos
{
    public class StickyFlowRegisterRequest
    {
        [Required, EmailAddress] public string Email { get; set; }
        [Required, StringLength(100, MinimumLength = 6)] public string Password { get; set; }
        [Required] public string FullName { get; set; }
        public string? TimeZone { get; set; }
    }

    public class StickyFlowLoginRequest
    {
        [Required, EmailAddress] public string Email { get; set; }
        [Required] public string Password { get; set; }
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