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

        public async Task SendPasswordResetOtpAsync(string toEmail, string otp, int expiryMinutes = 120)
        {
            var subject = "Ocufii Password Reset OTP";

            var body = $@"
                <h2>Password Reset Request</h2>
                <p>Hello,</p>
                <p>You requested to reset your Ocufii account password.</p>
                <p>Your one-time password (OTP) is: <strong>{otp}</strong></p>
                <p><strong>This OTP is valid for {expiryMinutes} minutes.</strong> Please use it to reset your password.</p>
                <p>Do not share this OTP with anyone. If you did not request this reset, please ignore this email or contact support immediately.</p>
                <p>Best regards,<br/>Ocufii Team</p>
            ";

            await SendEmailAsync(toEmail, subject, body, isHtml: true);
        }
    }
}