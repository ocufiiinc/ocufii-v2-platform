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

        // 1. STATUS
        [HttpGet("status")]
        public IActionResult Status()
        {
            if (!AllowAll) return Forbid();
            return Ok(new
            {
                Environment = _env.EnvironmentName,
                DebugMode = "FULL POWER — NO AUTH — TESTDEVWEB",
                Endpoints = new[]
                {
                    "GET    /api/debug/tables",
                    "GET    /api/debug/table/{name}",
                    "POST   /api/debug/delete-user-by-email",
                    "GET    /api/debug/logs",
                    "GET    /api/debug/logs/2025-11-23",
                    "GET    /api/debug/logs/2025-11-23/download"
                }
            });
        }

        // 2. LIST ALL TABLES
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

        // 3. DUMP ANY TABLE
        [HttpGet("table/{tableName}")]
        public async Task<IActionResult> DumpTable(string tableName)
        {
            if (!AllowAll) return Forbid();

            var entityType = _db.Model.GetEntityTypes()
                .FirstOrDefault(t => string.Equals(t.GetTableName(), tableName, StringComparison.OrdinalIgnoreCase));

            if (entityType == null)
                return NotFound($"Table '{tableName}' not found");

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

        // 4. DELETE USER BY EMAIL — SAFE
        [HttpPost("delete-user-by-email")]
        public async Task<IActionResult> DeleteUserByEmail([FromBody] DeleteUserRequest request)
        {
            if (!AllowAll) return Forbid();

            var email = request.Email.Trim();
            var user = await _db.Users.FirstOrDefaultAsync(u => EF.Functions.ILike(u.Email, email));
            if (user == null) return NotFound($"User '{email}' not found");

            var userId = user.UserId;
            var tables = _db.Model.GetEntityTypes().Select(t => t.GetTableName()).Where(n => n != null).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var sql = new List<string>();
            if (tables.Contains("UserRoles")) sql.Add($"DELETE FROM \"UserRoles\" WHERE \"UserId\" = {{0}}");
            if (tables.Contains("UserSettings")) sql.Add($"DELETE FROM \"UserSettings\" WHERE \"user_id\" = {{0}}");
            if (tables.Contains("UserAssistSettings")) sql.Add($"DELETE FROM \"UserAssistSettings\" WHERE \"user_id\" = {{0}}");
            sql.Add($"DELETE FROM \"Users\" WHERE \"UserId\" = {{0}}");

            if (sql.Any())
                await _db.Database.ExecuteSqlRawAsync(string.Join(";\n", sql), userId);

            return Ok(new { message = $"User '{email}' and all data deleted", userId });
        }

        [HttpGet("logs")]
        public IActionResult GetLogFiles()
        {
            if (!AllowAll) return Forbid();

            var logPath = GetLogsPath();  // ← THIS NOW WORKS EVERYWHERE

            if (!Directory.Exists(logPath))
                return Ok(new { message = "Logs folder created", path = logPath });

            var files = Directory.GetFiles(logPath, "ocufii-*.txt")
                .Select(fullPath =>
                {
                    var fi = new FileInfo(fullPath);
                    var dateStr = fi.Name["ocufii-".Length..^4];
                    var date = $"{dateStr.Substring(0, 4)}-{dateStr.Substring(4, 2)}-{dateStr.Substring(6, 2)}";

                    return new
                    {
                        date,
                        file = fi.Name,
                        sizeKb = Math.Round(fi.Length / 1024.0, 2),
                        modified = fi.LastWriteTime
                    };
                })
                .OrderByDescending(x => x.date)
                .ToList();

            return Ok(new
            {
                count = files.Count,
                logsFolder = logPath,
                logs = files
            });
        }

        [HttpGet("logs/{date}")]
        public IActionResult ViewLog(string date)
        {
            if (!AllowAll) return Forbid();

            var safeDate = Path.GetFileName(date);
            if (!DateTime.TryParseExact(safeDate, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _))
                return BadRequest("Date format: YYYY-MM-DD");

            var fileName = $"ocufii-{safeDate.Replace("-", "")}.txt";
            var filePath = Path.Combine(GetLogsPath(), fileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound($"Log not found: {fileName}");

            return Content(System.IO.File.ReadAllText(filePath), "text/plain");
        }

        [HttpGet("logs/{date}/download")]
        public IActionResult DownloadLog(string date)
        {
            if (!AllowAll) return Forbid();

            var safeDate = Path.GetFileName(date);
            if (!DateTime.TryParseExact(safeDate, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _))
                return BadRequest("Date format: YYYY-MM-DD");

            var fileName = $"ocufii-{safeDate.Replace("-", "")}.txt";
            var filePath = Path.Combine(GetLogsPath(), fileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            return File(System.IO.File.ReadAllBytes(filePath), "text/plain", fileName);
        }

        private string GetLogsPath()
        {
            // 1. DEVELOPMENT & TESTDEVWEB: Use project root logs folder
            var projectRootLogs = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "logs"));
            if (Directory.Exists(projectRootLogs))
                return projectRootLogs;

            // 2. PUBLISHED / PRODUCTION: Use logs next to .exe
            var publishedLogs = Path.Combine(AppContext.BaseDirectory, "logs");
            if (Directory.Exists(publishedLogs))
                return publishedLogs;

            // 3. IF STILL NOT FOUND → CREATE IT NEXT TO EXE (for first publish)
            Directory.CreateDirectory(publishedLogs);
            return publishedLogs;
        }


        // 8. SEED ROLES & TENANT (unchanged)
        [HttpPost("seed/roles")]
        public async Task<IActionResult> SeedRoles()
        {
            if (!AllowAll) return Forbid();
            var roles = new (string name, string desc)[] { ("admin", "Full access"), ("viewer", "User"), ("manager", "Manager") };
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
                _db.Tenants.Add(new Tenant { ResellerId = tenantId, DateCreated = DateTime.UtcNow, DateUpdated = DateTime.UtcNow, ThemeConfig = "{}", CustomWorkflows = "[]" });
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