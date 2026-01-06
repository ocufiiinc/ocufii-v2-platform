using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;
using OcufiiAPI.Models;
using System.Text.Json;

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

        [HttpPost("seed/devices-for-user")]
        public async Task<IActionResult> SeedDevicesForUser([FromQuery] string email)
        {
            if (!AllowAll) return Forbid();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return NotFound($"User '{email}' not found");

            var gatewayType = await _db.DeviceTypes.FirstAsync(dt => dt.Key == "gateway");
            var beaconType = await _db.DeviceTypes.FirstAsync(dt => dt.Key == "beacon");

            var devices = new List<Device>
    {
        new Device
        {
            Id = Guid.NewGuid(),
            DeviceTypeId = gatewayType.Id,
            MacAddress = "GW:US:" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            Name = $"{user.FirstName}'s Personal Gateway",
            Location = "Home Office",
            UserId = user.UserId,
            TenantId = user.TenantId,
            IsEnabled = true,
            Attributes = "{\"owner\":\"" + user.Email + "\"}",
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        },
        new Device
        {
            Id = Guid.NewGuid(),
            DeviceTypeId = beaconType.Id,
            MacAddress = "BE:US:" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            Name = $"{user.FirstName}'s Safety Beacon",
            Location = "Carried by user",
            UserId = user.UserId,
            TenantId = user.TenantId,
            IsEnabled = true,
            Attributes = "{\"type\":\"personal\"}",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        }
    };

            _db.Devices.AddRange(devices);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = $"Seeded 2 devices for {email}",
                userId = user.UserId,
                devices = devices.Select(d => new { d.Id, d.Name, d.MacAddress })
            });
        }


        [HttpPost("seed/devices")]
        public async Task<IActionResult> SeedDevices()
        {
            if (!AllowAll) return Forbid();

            if (await _db.Devices.AnyAsync())
                return Ok(new { message = "Devices already seeded. Use /api/debug/table/Devices to view." });

            // Ensure DeviceTypes exist
            var types = new[]
            {
        new { Key = "gateway", Name = "Gateway", ConnectsToMqtt = true, RequiresAuth = true },
        new { Key = "beacon", Name = "Beacon", ConnectsToMqtt = false, RequiresAuth = false },
        new { Key = "safety_card", Name = "Safety Card", ConnectsToMqtt = true, RequiresAuth = true }
    };

            foreach (var t in types)
            {
                if (!await _db.DeviceTypes.AnyAsync(dt => dt.Key == t.Key))
                {
                    _db.DeviceTypes.Add(new DeviceType
                    {
                        Key = t.Key,
                        Name = t.Name,
                        ConnectsToMqtt = t.ConnectsToMqtt,
                        RequiresAuth = t.RequiresAuth,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            await _db.SaveChangesAsync();

            var gatewayType = await _db.DeviceTypes.FirstAsync(dt => dt.Key == "gateway");
            var beaconType = await _db.DeviceTypes.FirstAsync(dt => dt.Key == "beacon");
            var cardType = await _db.DeviceTypes.FirstAsync(dt => dt.Key == "safety_card");

            var gateway = new Device
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                DeviceTypeId = gatewayType.Id,
                MacAddress = "AA:BB:CC:11:22:33",
                Name = "Main Lobby Gateway",
                Location = "HQ Entrance - Riyadh",
                IsEnabled = true,
                IsDeleted = false,
                Attributes = JsonSerializer.Serialize(new
                {
                    gatewayType = "G1-Pro",
                    firmware = "2.8.1",
                    wifiSettings = new { ssid = "Ocufii-Secure", password = "KSA2025!" }
                }),
                CreatedAt = DateTime.UtcNow.AddDays(-90),
                UpdatedAt = DateTime.UtcNow
            };

            var beacons = new[]
            {
        new Device { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), DeviceTypeId = beaconType.Id, MacAddress = "BE:01:00:00:00:01", Name = "Front Door Beacon", Location = "Main Gate", IsEnabled = true, Attributes = "{\"type\":\"iBeacon\",\"major\":1,\"minor\":1}", CreatedAt = DateTime.UtcNow.AddDays(-60) },
        new Device { Id = Guid.Parse("22222222-2222-2222-2222-222222222223"), DeviceTypeId = beaconType.Id, MacAddress = "BE:01:00:00:00:02", Name = "Prayer Room Beacon", Location = "Floor 3", IsEnabled = true, Attributes = "{\"type\":\"Eddystone-UID\",\"namespace\":\"0x1234567890\"}", CreatedAt = DateTime.UtcNow.AddDays(-45) },
        new Device { Id = Guid.Parse("22222222-2222-2222-2222-222222222224"), DeviceTypeId = beaconType.Id, MacAddress = "BE:01:00:00:00:03", Name = "Emergency Exit Beacon", Location = "Stairwell B", IsEnabled = true, Attributes = "{\"type\":\"iBeacon\",\"major\":999}", CreatedAt = DateTime.UtcNow.AddDays(-30) }
    };

            var safetyCard = new Device
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                DeviceTypeId = cardType.Id,
                MacAddress = "SC:AA:BB:CC:DD:EE",
                Name = "Worker Safety Card - Ahmed",
                Location = "Ahmed Al-Ghamdi",
                IsEnabled = true,
                IsDeleted = false,
                Attributes = JsonSerializer.Serialize(new { employeeId = "EMP-001", department = "Operations", emergencyContact = "+966501234567" }),
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            };

            _db.Devices.AddRange(beacons.Prepend(gateway).Append(safetyCard));
            await _db.SaveChangesAsync();

            var gwCred = new DeviceCredential
            {
                DeviceId = gateway.Id,
                MqttUsername = "gateway_aabbcc112233",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("GwSecret2025KSA!"),
                IsEnabled = true,
                LastRotatedAt = DateTime.UtcNow.AddDays(-10),
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            var cardCred = new DeviceCredential
            {
                DeviceId = safetyCard.Id,
                MqttUsername = "safety_card_scaabbccddee",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("CardSecretKSA2025!"),
                IsEnabled = true,
                LastRotatedAt = DateTime.UtcNow.AddDays(-5),
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            };

            _db.DeviceCredentials.AddRange(gwCred, cardCred);

            var random = new Random();
            var now = DateTime.UtcNow;
            var telemetries = new List<DeviceTelemetry>();

            for (int i = 0; i < 120; i++)
            {
                var beacon = beacons[random.Next(beacons.Length)];
                var minutesAgo = random.Next(0, 180);

                telemetries.Add(new DeviceTelemetry
                {
                    DeviceId = beacon.Id,
                    SourceType = TelemetrySource.beacon,
                    ViaDeviceId = gateway.Id,
                    BatteryLevel = (short)random.Next(50, 100),
                    SignalStrength = (short)random.Next(-85, -45),
                    SignalQuality = random.Next(0, 3) switch { 0 => "excellent", 1 => "good", _ => "fair" },
                    Payload = JsonSerializer.Serialize(new
                    {
                        rssi = -random.Next(45, 90),
                        txPower = -59,
                        seenBy = "gateway_aabbcc112233",
                        timestamp = now.AddMinutes(-minutesAgo).ToString("o")
                    }),
                    ReceivedAt = now.AddMinutes(-minutesAgo),
                    DeviceTimestamp = now.AddMinutes(-minutesAgo).AddSeconds(random.Next(-30, 30))
                });
            }

            _db.DeviceTelemetry.AddRange(telemetries);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "CORE DEVICES MODULE FULLY SEEDED — READY FOR TESTING",
                seeded = new
                {
                    deviceTypes = 3,
                    devices = 5,
                    credentials = 2,
                    telemetryEntries = telemetries.Count
                },
                testNow = new
                {
                    listBeacons = "GET /devices?type=beacon",
                    beaconsSeenByGateway = "GET /devices/11111111-1111-1111-1111-111111111111/telemetry?limit=50",
                    gatewayCredentials = "POST /devices/11111111-1111-1111-1111-111111111111/credentials"
                },
                goldenGatewayId = "11111111-1111-1111-1111-111111111111"
            });
        }

        [HttpGet("logs")]
        public IActionResult GetLogFiles()
        {
            if (!AllowAll) return Forbid();

            var logPath = GetLogsPath();  

            if (!Directory.Exists(logPath))
                return Ok(new { message = "Logs folder created", path = logPath });

            var files = Directory.GetFiles(logPath, "ocufii-*.log")
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

            var logPath = GetLogsPath();
            var possibleFiles = Directory.GetFiles(logPath, $"ocufii-{safeDate.Replace("-", "")}.*")
                .FirstOrDefault(f => f.EndsWith(".log") || f.EndsWith(".txt"));

            if (possibleFiles == null || !System.IO.File.Exists(possibleFiles))
                return NotFound($"Log not found for date: {safeDate}");

            try
            {
                // SAFE READ — allows shared access (Serilog is writing)
                using var stream = new FileStream(possibleFiles, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                return Content(content, "text/plain");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(false, $"Failed to read log: {ex.Message}"));
            }
        }

        [HttpGet("logs/{date}/download")]
        public IActionResult DownloadLog(string date)
        {
            if (!AllowAll) return Forbid();

            var safeDate = Path.GetFileName(date);
            if (!DateTime.TryParseExact(safeDate, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _))
                return BadRequest("Date format: YYYY-MM-DD");

            var logPath = GetLogsPath();
            var possibleFiles = Directory.GetFiles(logPath, $"ocufii-{safeDate.Replace("-", "")}.*")
                .FirstOrDefault(f => f.EndsWith(".log") || f.EndsWith(".txt"));

            if (possibleFiles == null || !System.IO.File.Exists(possibleFiles))
                return NotFound();

            var fileName = Path.GetFileName(possibleFiles);

            try
            {
                // SAFE READ — shared access
                var stream = new FileStream(possibleFiles, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return File(stream, "text/plain", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(false, $"Failed to download log: {ex.Message}"));
            }
        }

        private string GetLogsPath()
        {
            var projectRoot = Directory.GetCurrentDirectory();           // ← THIS IS PROJECT ROOT
            var projectRootLogs = Path.Combine(projectRoot, "logs");

            // 1. DEVELOPMENT & TESTDEVWEB: Use project root logs folder
            //var projectRootLogs = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "logs"));
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

        private readonly PasswordHasher<User> _hasher = new(); // ← Add this field in DebugController

        [HttpPost("seed/super-admin")]
        public async Task<IActionResult> SeedSuperAdmin()
        {
            if (!AllowAll) return Forbid();

            const string adminEmail = "admin@ocufii.com";
            const string adminPassword = "Admin@2025!";

            var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);
            if (existing != null)
            {
                return Ok(new
                {
                    message = "Super Admin already exists",
                    email = adminEmail,
                    password = "Use change-password endpoint"
                });
            }

            var superAdminRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "super_admin");
            if (superAdminRole == null)
            {
                superAdminRole = new Role { RoleName = "super_admin", RoleDescription = "Ocufii Global Super Admin"};
                _db.Roles.Add(superAdminRole);
                await _db.SaveChangesAsync();
            }

            var admin = new User
            {
                UserId = Guid.NewGuid(),
                Email = adminEmail,
                FirstName = "Ocufii",
                LastName = "Super Admin",
                PhoneNumber = "+966500000000",
                Company = "Ocufii Global",
                Username = "superadmin",
                Password = _hasher.HashPassword(null!, adminPassword), // ← CORRECT HASH
                RoleId = superAdminRole.RoleId,
                TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                IsEnabled = true,
                IsDeleted = false,
                DateSubmitted = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow
            };

            _db.Users.Add(admin);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Super Admin created — LOGIN NOW",
                email = adminEmail,
                password = adminPassword,
                note = "CHANGE PASSWORD AFTER FIRST LOGIN"
            });
        }

        // POST /api/debug/seed/features
        [HttpPost("seed/features")]
        public async Task<IActionResult> SeedFeatures()
        {
            if (!AllowAll) return Forbid();

            var features = new[]
            {
        new { Key = "gateway_management", Name = "Gateway Management", Description = "Full control over gateways" },
        new { Key = "beacon_view", Name = "Beacon View", Description = "View beacon data" },
        new { Key = "beacon_edit", Name = "Beacon Edit", Description = "Edit beacon settings" },
        new { Key = "safety_card_control", Name = "Safety Card Control", Description = "Manage safety cards" },
        new { Key = "telemetry_access", Name = "Real-Time Telemetry", Description = "View live telemetry" },
        new { Key = "user_management", Name = "User Management", Description = "Create and manage dependent users" },
        new { Key = "reports_access", Name = "Reports & Analytics", Description = "Access to reports" }
    };

            int added = 0;
            foreach (var f in features)
            {
                if (!await _db.Features.AnyAsync(x => x.Key == f.Key))
                {
                    _db.Features.Add(new Feature
                    {
                        Id = Guid.NewGuid(),
                        Key = f.Key,
                        Name = f.Name,
                        Description = f.Description,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                    added++;
                }
            }

            if (added > 0) await _db.SaveChangesAsync();

            return Ok(new { message = $"Seeded {added} features (total {features.Length})" });
        }

    //    // POST /api/debug/seed/feature-flags
    //    [HttpPost("seed/feature-flags")]
    //    public async Task<IActionResult> SeedFeatureFlags()
    //    {
    //        if (!AllowAll) return Forbid();

    //        var defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    //        var flags = new[]
    //        {
    //    new { FlagName = "enable_panic_button", IsEnabled = true, Config = "{\"timeout\":30,\"escalation\":true}" },
    //    new { FlagName = "enable_geofencing", IsEnabled = true, Config = "{\"radius\":100}" },
    //    new { FlagName = "enable_live_tracking", IsEnabled = true, Config = "{}" },
    //    new { FlagName = "enable_reports", IsEnabled = true, Config = "{\"daily\":true,\"weekly\":true}" },
    //    new { FlagName = "enable_multi_language", IsEnabled = false, Config = "{\"languages\":[\"en\",\"ar\"]}" }
    //};

    //        int added = 0;
    //        foreach (var f in flags)
    //        {
    //            if (!await _db.FeatureFlags.AnyAsync(ff => ff.FlagName == f.FlagName && ff.TenantId == defaultTenantId))
    //            {
    //                _db.FeatureFlags.Add(new FeatureFlag
    //                {
    //                    TenantId = defaultTenantId,
    //                    FlagName = f.FlagName,
    //                    IsEnabled = f.IsEnabled,
    //                    Config = f.Config,
    //                    UpdatedAt = DateTime.UtcNow
    //                });
    //                added++;
    //            }
    //        }

    //        if (added > 0) await _db.SaveChangesAsync();

    //        return Ok(new { message = $"Seeded {added} feature flags for default tenant" });
    //    }

    }

    public class DeleteUserRequest
    {
        public string Email { get; set; } = "";
    }
}