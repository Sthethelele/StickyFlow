// Models/Note.cs
using System.ComponentModel.DataAnnotations;

namespace StickyFlow.Models
{
    public class Note : BaseEntity
    {
        public Guid NoteId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public new bool IsDeleted { get; set; } // Add this line

        // Foreign keys
        public string UserId { get; set; } = string.Empty;
        public int PriorityId { get; set; }
        public int StatusId { get; set; }
        public DateTime? DueDate { get; set; }
        public string CreatedBy { get; set; }
        public string LastModifiedBy { get; set; }

        // Navigation properties
        public virtual ApplicationUser? User { get; set; }
        public virtual Priority? Priority { get; set; }
        public virtual Status? Status { get; set; }
        public virtual NoteMetadata? Metadata { get; set; }
        public virtual ICollection<ReminderLog> ReminderLogs { get; set; } = new List<ReminderLog>();
    }

    public class NoteMetadata
    {
        public DateTime? LastAccessed { get; set; }
    }
}