// Models/ReminderLog.cs
using StickyFlow.Services;
using System;
using System.ComponentModel.DataAnnotations;

namespace StickyFlow.Models
{
    public class ReminderLog
    {
        [Key]
        public Guid LogId { get; set; } = Guid.NewGuid();
        public Guid NoteId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime ScheduledTime { get; set; }
        public ReminderStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Note? Note { get; set; }
        public virtual ApplicationUser? User { get; set; }
    }
}