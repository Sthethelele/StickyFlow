// IEmailService.cs (interface)
using System.Net.Mail;
using System.Net;
using System.Web;
using StickyFlow.Models;
using StickyFlow.Dtos;
using StickyFlow.Services;
using StickyFlow.Data;

namespace StickyFlow.Services
{
    public interface IEmailService
    {
        Task SendConfirmationEmailAsync(string email, string userId, string token);
        Task SendPasswordResetEmailAsync(string email, string token);
        Task SendReminderEmailAsync(string email, string noteTitle, DateTime dueDate);
    }
}
