namespace OcufiiAPI.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = false);
        Task SendPasswordResetOtpAsync(string toEmail, string otp, int expiryMinutes = 120);
    }
}
