namespace OcufiiAPI.DTO
{
    public class LoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Company { get; set; }
        public bool AccountHold { get; set; } = false;
        public DateTime? SubscriptionDate { get; set; }
        public string? GtmInfo { get; set; }
        public string? UserName { get; set; }
    }

    public class RefreshDto
    {
        public string RefreshToken { get; set; } = string.Empty;
    }   

    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ChangeEmailDto
    {
        public string NewEmail { get; set; } = string.Empty;
    }
}
