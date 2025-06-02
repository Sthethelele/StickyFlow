// Services/IReminderService.cs
namespace StickyFlow.Services
{
    public interface IReminderService
    {
        Task ScheduleReminder(Guid noteId, string userId, DateTime dueDate);
        Task CancelReminder(Guid noteId);
        Task RescheduleReminder(Guid noteId, DateTime newDueDate);
        Task ProcessDueReminders();
    }
}