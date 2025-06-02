using System.ComponentModel.DataAnnotations;

namespace StickyFlow.Models
{
    public class UserProfile
    {
        public int Id { get; set; }
        public string UserId { get; set; } // Foreign key to ApplicationUser

        [Required]
        public string FullName { get; set; }

        public string? TimeZone { get; set; } = "UTC";
        public string? ProfilePictureUrl { get; set; }

        // Navigation property
        public virtual ApplicationUser User { get; set; }
    }
}