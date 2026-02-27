namespace OcufiiAPI.Services
{
    public interface INotificationService
    {
        Task SendNotificationAsync(string deviceToken, string title, string body, Dictionary<string, string> data = null);
        Task SendToMultipleAsync(List<string> deviceTokens, string title, string body, Dictionary<string, string> data = null);
    }
}
