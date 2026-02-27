using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Polly;
using OcufiiAPI.Configs;

namespace OcufiiAPI.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = false)
        {
            var mailMessage = new MailMessage(_settings.FromEmail, toEmail)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            using var smtpClient = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                Credentials = new NetworkCredential(_settings.SmtpUsername, _settings.SmtpPassword),
                EnableSsl = _settings.EnableSsl
            };

            // Retry policy for performance/resilience (up to 3 retries on transient errors)
            var retryPolicy = Policy.Handle<SmtpException>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            await retryPolicy.ExecuteAsync(async () => await smtpClient.SendMailAsync(mailMessage));
        }
    }
}