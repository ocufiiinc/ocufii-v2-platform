using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;
using OcufiiAPI.Models;

namespace OcufiiAPI.Controllers
{
    [ApiController]
    [Route("api/debug")]
    public class DebugController : ControllerBase
    {
        private readonly OcufiiDbContext _db;
        private readonly IWebHostEnvironment _env;

        public DebugController(OcufiiDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private bool AllowAll => _env.IsDevelopment() ||
                                 _env.EnvironmentName.Equals("TestDevWeb", StringComparison.OrdinalIgnoreCase);

        [HttpGet("status")]
        public IActionResult Status()
        {
            if (!AllowAll) return Forbid();
            return Ok(new
            {
                Environment = _env.EnvironmentName,
                DebugMode = "ACTIVE — NO AUTH — FULL POWER",
                Endpoints = new[]
                {
                    "GET    /api/debug/tables",
                    "GET    /api/debug/table/{name}",
                    "POST   /api/debug/delete-user-by-email",
                    "POST   /api/debug/seed/roles",
                    "POST   /api/debug/seed/tenant"
                }
            });
        }

        [HttpGet("tables")]
        public IActionResult GetTables()
        {
            if (!AllowAll) return Forbid();

            var tables = _db.Model.GetEntityTypes()
                .Select(t => t.GetTableName())
                .Where(n => n != null)
                .OrderBy(n => n)
                .ToList();

            return Ok(new { count = tables.Count, tables });
        }

        [HttpGet("table/{tableName}")]
        public async Task<IActionResult> DumpTable(string tableName)
        {
            if (!AllowAll) return Forbid();

            var entityType = _db.Model.GetEntityTypes()
                .FirstOrDefault(t => string.Equals(t.GetTableName(), tableName, StringComparison.OrdinalIgnoreCase));

            if (entityType == null)
                return NotFound($"Table '{tableName}' not found in DbContext");

            try
            {
                var sql = $"SELECT * FROM \"{tableName}\"";
                var connection = _db.Database.GetDbConnection();
                var wasOpen = connection.State == System.Data.ConnectionState.Open;
                if (!wasOpen) await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                using var reader = await command.ExecuteReaderAsync();

                var results = new List<Dictionary<string, object>>();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        row[reader.GetName(i)] = val;
                    }
                    results.Add(row);
                }

                if (!wasOpen) connection.Close();

                return Ok(new { table = tableName, count = results.Count, data = results });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Query failed", details = ex.Message });
            }
        }

        // FIXED: SAFE DELETE BY EMAIL — WORKS EVEN IF TABLES DON'T EXIST
        [HttpPost("delete-user-by-email")]
        public async Task<IActionResult> DeleteUserByEmail([FromBody] DeleteUserRequest request)
        {
            if (!AllowAll) return Forbid();

            var email = request.Email.Trim();
            var user = await _db.Users.FirstOrDefaultAsync(u => EF.Functions.ILike(u.Email, email));

            if (user == null)
                return NotFound($"User with email '{email}' not found");

            var userId = user.UserId;
            int deleted = 0;

            // Only delete from tables that actually exist
            var existingTables = _db.Model.GetEntityTypes()
                .Select(t => t.GetTableName())
                .Where(n => n != null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var sqlCommands = new List<string>();

            if (existingTables.Contains("UserRoles"))
                sqlCommands.Add($"DELETE FROM \"UserRoles\" WHERE \"UserId\" = {{0}}");

            if (existingTables.Contains("UserSettings"))
                sqlCommands.Add($"DELETE FROM \"UserSettings\" WHERE \"user_id\" = {{0}}");

            if (existingTables.Contains("UserAssistSettings"))
                sqlCommands.Add($"DELETE FROM \"UserAssistSettings\" WHERE \"user_id\" = {{0}}");

            // Always delete from Users last
            sqlCommands.Add($"DELETE FROM \"Users\" WHERE \"UserId\" = {{0}}");

            if (sqlCommands.Any())
            {
                var fullSql = string.Join(";\n", sqlCommands);
                deleted = await _db.Database.ExecuteSqlRawAsync(fullSql, userId);
            }

            return Ok(new
            {
                message = $"All data for '{email}' (ID: {userId}) deleted successfully",
                deletedRecords = deleted,
                tablesChecked = existingTables.ToArray(),
                deletedAt = DateTime.UtcNow
            });
        }

        [HttpPost("seed/roles")]
        public async Task<IActionResult> SeedRoles()
        {
            if (!AllowAll) return Forbid();

            var roles = new (string name, string desc)[]
            {
                ("admin", "Full access"),
                ("viewer", "Standard user"),
                ("manager", "Manager")
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
        public async Task<IActionResult> SeedTenant()
        {
            if (!AllowAll) return Forbid();

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

            return Ok(new { message = "Default tenant ready" });
        }
    }

    public class DeleteUserRequest
    {
        public string Email { get; set; } = "";
    }
}