using Microsoft.IdentityModel.Tokens;
using OcufiiAPI.Configs;
using OcufiiAPI.Models;
using OcufiiAPI.Repositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OcufiiAPI.Services
{
    public class AuthService : IAuthService
    {
        private readonly IRepository<User> _userRepo;
        private readonly JwtConfig _jwtConfig;
        private readonly IRepository<Role> _roleRepo;

        public AuthService(IRepository<User> userRepo, IRepository<Role> roleRepo, JwtConfig jwtConfig)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo; 
            _jwtConfig = jwtConfig;
        }

        public async Task<string> LoginAsync(string email, string password)
        {
            var user = (await _userRepo.FindAsync(u => u.Email == email && u.Password == password)).FirstOrDefault();
            if (user == null) throw new UnauthorizedAccessException("Invalid credentials");

            return GenerateJwtToken(user);
        }

        public async Task<bool> RegisterAsync(string email, string password, string firstName, string role = "viewer")
        {
            var roleEntity = (await _roleRepo.FindAsync(r => r.RoleName == role)).FirstOrDefault();
            if (roleEntity == null)
                return false;

            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = email,
                Password = password,
                FirstName = firstName,
                RoleId = roleEntity.RoleId,
                AccountType = "single",
                IsEnabled = true,
                DateUpdated = DateTime.UtcNow
            };

            await _userRepo.AddAsync(user);
            await _userRepo.SaveAsync();
            return true;
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "viewer")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.Secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtConfig.Issuer,
                audience: _jwtConfig.Audience,
                claims: claims,
                expires: DateTime.Now.AddMinutes(_jwtConfig.ExpiryMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
