namespace OcufiiAPI.Services
{
    public interface IAuthService
    {
        Task<string> LoginAsync(string email, string password);
        Task<bool> RegisterAsync(string email, string password, string firstName, string role = "viewer");
    }
}
