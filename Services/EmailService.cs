using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Web;
using StickyFlow.Models;
using StickyFlow.Dtos;
using StickyFlow.Services;
using StickyFlow.Data;
using Microsoft.Extensions.Options;

namespace StickyFlow.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;
        private readonly SmtpSettings _smtpSettings;

        public EmailService(IConfiguration config, ILogger<EmailService> logger, IOptions<SmtpSettings> smtpSettings)
        {
            _config = config;
            _logger = logger;
            _smtpSettings = smtpSettings.Value;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlContent)
        {
            using var client = new SmtpClient(_smtpSettings.SmtpHost)
            {
                Port = _smtpSettings.SmtpPort,
                Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password),
                EnableSsl = true
            };

            var message = new MailMessage
            {
                From = new MailAddress(_smtpSettings.FromAddress, "StickyFlow"),
                Subject = subject,
                Body = htmlContent,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            try
            {
                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
                throw; // Re-throw to allow higher-level handling
            }
        }

        public async Task SendConfirmationEmailAsync(string email, string userId, string token)
        {
            var encodedToken = HttpUtility.UrlEncode(token);
            var confirmUrl = $"{_config["ClientUrl"]}/confirm-email?userId={userId}&token={encodedToken}";

            var html = $"""
                <h1>Confirm Your Email</h1>
                <p>Please confirm your account by <a href="{confirmUrl}">clicking here</a>.</p>
                <p>Or copy this link: {confirmUrl}</p>
                """;

            await SendEmailAsync(email, "Confirm Your Email", html);
        }

        public async Task SendPasswordResetEmailAsync(string email, string token)
        {
            var encodedToken = HttpUtility.UrlEncode(token);
            var resetUrl = $"{_config["ClientUrl"]}/reset-password?email={email}&token={encodedToken}";

            var html = $"""
                <h1>Reset Your Password</h1>
                <p>Reset your password by <a href="{resetUrl}">clicking here</a>.</p>
                <p>Or copy this link: {resetUrl}</p>
                <p>This link expires in 1 hour.</p>
                """;

            await SendEmailAsync(email, "Password Reset Request", html);
        }

        public async Task SendReminderEmailAsync(string email, string noteTitle, DateTime dueDate)
        {
            var subject = $"StickyFlow - Reminder: {noteTitle}";
            var body = $@"<h2>Reminder: {noteTitle}</h2>
                        <p>This is a reminder for your task due on {dueDate:MMM dd, yyyy HH:mm}.</p>
                        <p>Log in to StickyFlow to manage your tasks: <a href=""{_config["ClientUrl"]}/login"">Log In</a></p>";

            await SendEmailAsync(email, subject, body);
        }
    }
}