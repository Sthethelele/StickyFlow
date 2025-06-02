using StickyFlow.Models;

namespace StickyFlow.Extensions
{
    public static class NoteExtensions
    {
        public static void UpdateAuditFields(this Note note, string userId, bool isNew = false)
        {
            if (isNew)
            {
                note.CreatedAt = DateTime.UtcNow;
                note.LastModified = DateTime.UtcNow;
                note.CreatedBy = userId;
            }
            note.LastModified = DateTime.UtcNow;
            note.LastModifiedBy = userId;
        }
    }
}