using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StickyFlow.Data;
using StickyFlow.Models;
using System;
using System.Threading.Tasks;

namespace StickyFlow.Services
{
    public class ReminderService : IReminderService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<ReminderService> _logger;
        private readonly IConfiguration _configuration;

        public ReminderService(
            ApplicationDbContext context,
            IEmailService emailService,
            ILogger<ReminderService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task ScheduleReminder(Guid noteId, string userId, DateTime dueDate)
        {
            try
            {
                // Calculate reminder time (e.g., 1 day before due date)
                var reminderTime = dueDate.AddDays(-1);

                // Check if a reminder already exists
                var existingReminder = await _context.ReminderLogs
                    .FirstOrDefaultAsync(r => r.NoteId == noteId && r.Status == ReminderStatus.Scheduled);

                if (existingReminder != null)
                {
                    _logger.LogWarning($"A reminder for note {noteId} is already scheduled.");
                    return;
                }

                var reminder = new ReminderLog
                {
                    NoteId = noteId,
                    UserId = userId,
                    ScheduledTime = reminderTime,
                    Status = ReminderStatus.Scheduled
                };

                _context.ReminderLogs.Add(reminder);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Reminder scheduled for note {noteId} at {reminderTime}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to schedule reminder for note {noteId}");
                throw;
            }
        }

        public async Task CancelReminder(Guid noteId)
        {
            try
            {
                var reminder = await _context.ReminderLogs
                    .FirstOrDefaultAsync(r => r.NoteId == noteId && r.Status == ReminderStatus.Scheduled);

                if (reminder != null)
                {
                    reminder.Status = ReminderStatus.Cancelled;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Reminder cancelled for note {noteId}");
                }
                else
                {
                    _logger.LogWarning($"No scheduled reminder found for note {noteId} to cancel.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to cancel reminder for note {noteId}");
                throw;
            }
        }

        public async Task RescheduleReminder(Guid noteId, DateTime newDueDate)
        {
            try
            {
                // Find the existing reminder
                var reminder = await _context.ReminderLogs
                    .FirstOrDefaultAsync(r => r.NoteId == noteId && r.Status == ReminderStatus.Scheduled);

                if (reminder == null)
                {
                    _logger.LogWarning($"No scheduled reminder found for note {noteId} to reschedule.");
                    return;
                }

                // Update the reminder time
                var newReminderTime = newDueDate.AddDays(-1);
                reminder.ScheduledTime = newReminderTime;
                reminder.Status = ReminderStatus.Scheduled;

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Reminder rescheduled for note {noteId} to {newReminderTime}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to reschedule reminder for note {noteId}");
                throw;
            }
        }

        // This would be called by your background job processor
        public async Task ProcessDueReminders()
        {
            var dueReminders = await _context.ReminderLogs
                .Where(r => r.ScheduledTime <= DateTime.UtcNow && r.Status == ReminderStatus.Scheduled)
                .Include(r => r.Note)
                .Include(r => r.User)
                .ToListAsync();

            foreach (var reminder in dueReminders)
            {
                try
                {
                    await _emailService.SendReminderEmailAsync(
                        reminder.User.Email,
                        reminder.Note.Title,
                        reminder.Note.DueDate ?? DateTime.UtcNow);

                    reminder.Status = ReminderStatus.Completed;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Reminder {reminder.LogId} sent for note {reminder.NoteId}");
                }
                catch (Exception ex)
                {
                    reminder.Status = ReminderStatus.Failed;
                    await _context.SaveChangesAsync();
                    _logger.LogError(ex, $"Failed to process reminder {reminder.LogId} for note {reminder.NoteId}");
                }
            }
        }
    }

    public enum ReminderStatus
    {
        Scheduled,
        Completed,
        Cancelled,
        Failed
    }
}