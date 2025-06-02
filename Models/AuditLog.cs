using System.ComponentModel.DataAnnotations;

namespace StickyFlow.Models
{
    public class AuditLog
    {
        [Key]
        public Guid AuditLogId { get; set; }
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string KeyValues { get; set; } = string.Empty;
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? ChangedColumns { get; set; }
    }
}