using Microsoft.AspNetCore.Identity;           // ← ONLY THIS ONE for hashing
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OcufiiAPI.Configs;
using OcufiiAPI.Models;
using OcufiiAPI.Repositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OcufiiAPI.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<Role> _roleRepo;
        private readonly JwtConfig _jwtConfig;
        private readonly PasswordHasher<User> _passwordHasher;
        private const string FIXED_TENANT_ID = "00000000-0000-0000-0000-000000000001";

        public AuthController(
            IRepository<User> userRepo,
            IRepository<Role> roleRepo,
            JwtConfig jwtConfig)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _jwtConfig = jwtConfig;
            _passwordHasher = new PasswordHasher<User>();   // Microsoft official
        }

        // POST /auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = (await _userRepo.FindAsync(u => u.Email == dto.Email && !u.IsDeleted))
                .FirstOrDefault();

            if (user == null)
                return Ok(new { ok = false, message = "Invalid credentials" });

            var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.Password, dto.Password);
            if (verifyResult == PasswordVerificationResult.Failed)
                return Ok(new { ok = false, message = "Invalid credentials" });

            var (accessToken, refreshToken) = GenerateTokens(user);
            await StoreRefreshToken(user.UserId, refreshToken);

            return Ok(new
            {
                ok = true,
                message = "User Email and Password Matched.",
                user = new { email = user.Email },
                access_token = accessToken,
                refresh_token = refreshToken
            });
        }

        // GET /auth/email/{email}/validate
        [HttpGet("email/{email}/validate")]
        public async Task<IActionResult> ValidateEmail(string email)
        {
            var exists = (await _userRepo.FindAsync(u => u.Email == email)).Any();
            return Ok(new { ok = true, message = exists ? "Email already exists." : "Email available." });
        }

        // POST /auth/register
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if ((await _userRepo.FindAsync(u => u.Email == dto.Email)).Any())
                return Ok(new { ok = false, message = "Email already exists" });

            var role = (await _roleRepo.FindAsync(r => r.RoleName == "viewer")).FirstOrDefault()
           ?? (await _roleRepo.FindAsync(r => r.RoleName == "legacy_user")).FirstOrDefault();

            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = dto.Email,
                FirstName = dto.FirstName ?? "User",
                LastName = dto.LastName ?? "",
                PhoneNumber = dto.PhoneNumber ?? "",
                Company = dto.Company ?? "Legacy Company",
                RoleId = role!.RoleId,
                TenantId = Guid.Parse(FIXED_TENANT_ID),
                AccountType = "single",
                IsEnabled = true,
                IsDeleted = false,
                DateUpdated = DateTime.UtcNow
            };

            user.Password = _passwordHasher.HashPassword(user, dto.Password);

            await _userRepo.AddAsync(user);
            await _userRepo.SaveAsync();

            return Ok(new { ok = true, message = "" });
        }

        // DELETE /auth/users/{email}
        [HttpDelete("users/{email}")]
        public async Task<IActionResult> DeleteUser(string email)
        {
            var user = (await _userRepo.FindAsync(u => u.Email == email)).FirstOrDefault();
            if (user != null)
            {
                user.IsDeleted = true;
                user.IsEnabled = false;
                _userRepo.Update(user);
                await _userRepo.SaveAsync();
            }
            return Ok(new { ok = true });
        }

        // PUT /auth/users/{email}/password
        [HttpPut("users/{email}/password")]
        public async Task<IActionResult> ChangePassword(string email, [FromBody] ChangePasswordDto dto)
        {
            var user = (await _userRepo.FindAsync(u => u.Email == email)).FirstOrDefault();
            if (user != null)
            {
                user.Password = _passwordHasher.HashPassword(user, dto.NewPassword);
                user.DateUpdated = DateTime.UtcNow;
                _userRepo.Update(user);
                await _userRepo.SaveAsync();
            }
            return Ok(new { ok = true, message = "" });
        }

        // PUT /auth/users/{email}/bool
        [HttpPut("users/{email}/bool")]
        public async Task<IActionResult> UpdateBool(string email, [FromBody] UpdateBoolDto dto)
        {
            var user = (await _userRepo.FindAsync(u => u.Email == email)).FirstOrDefault();
            if (user == null) return Ok(new { ok = false });

            switch (dto.Field.ToLowerInvariant())
            {
                case "isenabled": user.IsEnabled = dto.Value; break;
                case "isdeleted": user.IsDeleted = dto.Value; break;
                case "accounthold": user.AccountHold = dto.Value; break;
            }

            user.DateUpdated = DateTime.UtcNow;
            _userRepo.Update(user);
            await _userRepo.SaveAsync();

            return Ok(new { ok = true, message = "" });
        }

        private (string accessToken, string refreshToken) GenerateTokens(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim("tenant_id", FIXED_TENANT_ID),
                new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "viewer")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.Secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var accessToken = new JwtSecurityToken(
                issuer: _jwtConfig.Issuer,
                audience: _jwtConfig.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(60),
                signingCredentials: creds);

            var refreshToken = Guid.NewGuid().ToString("N");

            return (new JwtSecurityTokenHandler().WriteToken(accessToken), refreshToken);
        }

        private Task StoreRefreshToken(Guid userId, string token)
        {
            // Full RefreshToken table + endpoint coming in next message
            return Task.CompletedTask;
        }
    }

    // DTOs
    public class LoginDto { public string Email { get; set; } = ""; public string Password { get; set; } = ""; }
    public class RegisterDto
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Company { get; set; }
    }
    public class ChangePasswordDto { public string NewPassword { get; set; } = ""; }
    public class UpdateBoolDto
    {
        public string Field { get; set; } = ""; public bool Value { get; set; }
    }
}