// Models/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace StickyFlow.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public required string FullName { get; set; }

        public DateTime MemberSince { get; set; } = DateTime.UtcNow;
        public string? RefreshToken { get; set; }
        public DateTime RefreshTokenExpiryTime { get; set; }
        public bool IsDeleted { get; set; }

        // Navigation properties
        public virtual UserProfile? Profile { get; set; }
        public virtual ICollection<Note> Notes { get; set; } = new List<Note>();
        public virtual ICollection<ReminderLog> Reminders { get; set; } = new List<ReminderLog>();
    }
}