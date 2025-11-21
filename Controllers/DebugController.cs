using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;
using OcufiiAPI.Models;
using OcufiiAPI.Configs;
using System.Reflection;

namespace OcufiiAPI.Controllers
{
    [ApiController]
    [Route("api/debug")]
    public class DebugController : ControllerBase
    {
        private readonly OcufiiDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DebugController> _logger;

        public DebugController(OcufiiDbContext db, IWebHostEnvironment env, ILogger<DebugController> logger)
        {
            _db = db;
            _env = env;
            _logger = logger;
        }

        private bool IsAllowed => _env.IsDevelopment() ||
                                  _env.EnvironmentName.Equals("TestDevWeb", StringComparison.OrdinalIgnoreCase);

        // GET /api/debug/status → NO TOKEN NEEDED
        [HttpGet("status")]
        [AllowAnonymous] // ← Anyone can check if debug is active
        public IActionResult Status()
        {
            if (!IsAllowed) return Forbid();

            return Ok(new
            {
                Environment = _env.EnvironmentName,
                Timestamp = DateTime.UtcNow,
                Message = "Debug API ACTIVE — Use with admin token"
            });
        }

        // ALL OTHER ENDPOINTS REQUIRE ADMIN TOKEN
        [HttpPost("seed/roles")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> SeedRoles()
        {
            if (!IsAllowed) return Forbid();

            var roles = new (string name, string desc)[]
            {
                ("viewer",  "Standard viewer access"),
                ("admin",   "Full system administrator"),
                ("manager", "Site/Floor manager"),
                ("support", "Customer support"),
                ("technician", "Field technician")
            };

            int added = 0;
            foreach (var (name, desc) in roles)
            {
                if (!await _db.Roles.AnyAsync(r => r.RoleName == name))
                {
                    _db.Roles.Add(new Role { RoleName = name, RoleDescription = desc });
                    added++;
                }
            }
            if (added > 0) await _db.SaveChangesAsync();

            return Ok(new { message = $"Seeded {added} roles" });
        }

        [HttpPost("seed/tenant")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> SeedTenant()
        {
            if (!IsAllowed) return Forbid();

            var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            if (!await _db.Tenants.AnyAsync(t => t.ResellerId == tenantId))
            {
                _db.Tenants.Add(new Tenant
                {
                    ResellerId = tenantId,
                    DateCreated = DateTime.UtcNow,
                    DateUpdated = DateTime.UtcNow,
                    ThemeConfig = "{}",
                    CustomWorkflows = "[]"
                });
                await _db.SaveChangesAsync();
            }

            return Ok(new { message = "Default tenant created" });
        }

        [HttpPost("seed/create-admin")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> CreateFirstAdmin([FromBody] CreateAdminRequest request)
        {
            if (!IsAllowed) return Forbid();

            // Create user (your exact User table structure)
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = request.Email,
                FirstName = request.FirstName ?? "Admin",
                LastName = request.LastName ?? "User",
                PhoneNumber = request.PhoneNumber,
                Company = request.Company ?? "Ocufii",
                // No CreatedAt/UpdatedAt — your table doesn't have them
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Create settings
            _db.UserSettings.Add(new UserSetting { UserId = user.UserId });

            var defaultConfig = "{\"emergency\":{\"alarmSound\":false,\"alertMessage\":\"\",\"flashOn\":false,\"isEnabled\":true,\"screenFlashing\":false},\"distress\":{\"alarmSound\":false,\"alertMessage\":\"\",\"flashOn\":false,\"isEnabled\":true,\"screenFlashing\":false},\"activeShooter\":{\"alarmSound\":false,\"alertMessage\":\"\",\"flashOn\":false,\"isEnabled\":true,\"screenFlashing\":false},\"emergency911\":{\"alertMessage\":\"\",\"isEnabled\":true},\"emergency988\":{\"alertMessage\":\"\",\"isEnabled\":true}}";
            _db.UserAssistSettings.Add(new UserAssistSetting
            {
                UserId = user.UserId,
                Config = defaultConfig
            });

            // Assign admin role
            _db.Set<Dictionary<string, object>>("UserRoles")
               .Add(new Dictionary<string, object>
               {
                   { "UserId", user.UserId },
                   { "RoleName", "admin" }
               });

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "First admin created — NOW YOU CAN LOGIN",
                email = user.Email,
                userId = user.UserId,
                note = "Set password via normal register flow or direct DB update"
            });
        }

        [HttpPost("nuke")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Nuke()
        {
            if (!IsAllowed) return Forbid();

            await _db.Database.ExecuteSqlRawAsync(@"
                TRUNCATE TABLE ""Users"", ""UserSettings"", ""UserAssistSettings"", ""UserRoles"" RESTART IDENTITY CASCADE;
            ");

            return Ok(new { message = "Database wiped clean" });
        }
    }

    public class CreateAdminRequest
    {
        public string Email { get; set; } = "admin@ocufii.sa";
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Company { get; set; }
    }
}
