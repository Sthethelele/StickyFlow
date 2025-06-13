using StickyFlow.Dtos;

namespace StickyFlow.DTOs
{
    public class ReminderLogDto
    {
        public Guid Id { get; set; }
        public Guid NoteId { get; set; }
        public NoteDto Note { get; set; }
        public DateTime ScheduledDate { get; set; }
    }
}
