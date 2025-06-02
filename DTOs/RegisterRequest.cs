using System.ComponentModel.DataAnnotations;
namespace StickyFlow.DTOs
{
    public class RegisterRequest
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public required string Password { get; set; }

        [Required]
        public required string FullName { get; set; }

        public string? TimeZone { get; set; }
    }
}