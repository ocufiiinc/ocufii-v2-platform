using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using OcufiiAPI.Configs;
using OcufiiAPI.Data;
using OcufiiAPI.Models;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace OcufiiAPI.Controllers
{
    [ApiController]
    [Route("api/debug")]
    public class DebugController : ControllerBase
    {
        private readonly OcufiiDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IOptions<SnoozeReasonConfig> _snoozeConfig;
        private readonly MqttConfig _mqttConfig;

        public DebugController(OcufiiDbContext db, IWebHostEnvironment env, IOptions<SnoozeReasonConfig> snoozeConfig, IOptions<MqttConfig> mqttConfig)
        {
            _db = db;
            _env = env;
            _snoozeConfig = snoozeConfig;
            _mqttConfig = mqttConfig.Value;
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
            var projectRoot = Directory.GetCurrentDirectory();
            var projectRootLogs = Path.Combine(projectRoot, "logs");

            if (Directory.Exists(projectRootLogs))
                return projectRootLogs;

            var publishedLogs = Path.Combine(AppContext.BaseDirectory, "logs");
            if (Directory.Exists(publishedLogs))
                return publishedLogs;

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

        private readonly PasswordHasher<User> _hasher = new();

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
                superAdminRole = new Role { RoleName = "super_admin", RoleDescription = "Ocufii Global Super Admin" };
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
                Password = _hasher.HashPassword(null!, adminPassword),
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

        [HttpPost("seed/notification-categories")]
        public async Task<IActionResult> SeedNotificationCategories()
        {
            if (!AllowAll) return Forbid();

            var categories = new[]
            {
        new NotificationCategory { Id = 1, Key = "security", Name = "Security Notifications" },
        new NotificationCategory { Id = 2, Key = "system", Name = "System Notifications" },
        new NotificationCategory { Id = 3, Key = "safety", Name = "Safety Alerts" },
        new NotificationCategory { Id = 4, Key = "snooze", Name = "Snooze Notifications" }
    };

            int added = 0;
            foreach (var cat in categories)
            {
                if (!await _db.NotificationCategories.AnyAsync(c => c.Id == cat.Id))
                {
                    _db.NotificationCategories.Add(cat);
                    added++;
                }
            }

            if (added > 0) await _db.SaveChangesAsync();

            return Ok(new { message = $"Seeded {added} notification categories" });
        }

        [HttpPost("seed/snooze-reasons")]
        public async Task<IActionResult> SeedSnoozeReasons()
        {
            if (!AllowAll) return Forbid();

            var config = _snoozeConfig.Value.SnoozeReasons;
            if (config == null || !config.Any())
                return BadRequest("No snooze reasons configured in appsettings");

            int added = 0;
            foreach (var item in config)
            {
                if (!await _db.SnoozeReasons.AnyAsync(sr => sr.Key == item.Key))
                {
                    _db.SnoozeReasons.Add(new SnoozeReason
                    {
                        Key = item.Key,
                        Label = item.Label,
                        CreatedAt = DateTime.UtcNow
                    });
                    added++;
                }
            }

            if (added > 0) await _db.SaveChangesAsync();

            return Ok(new
            {
                message = $"Seeded {added} snooze reasons (total {config.Count})"
            });
        }

        [HttpPost("seed/notifications")]
        public async Task<IActionResult> SeedNotifications()
        {
            if (!AllowAll) return Forbid();

            var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                           ?? "7bdcdd13-8eb7-4021-993c-30aeff71b704");

            var deviceIds = await _db.Devices.Select(d => d.Id).Take(10).ToListAsync();
            var random = new Random();

            var categories = new[]
            {
        (Id: (short)1, Key: "security"),
        (Id: (short)2, Key: "system"),
        (Id: (short)3, Key: "safety"),
        (Id: (short)4, Key: "snooze")
    };

            var types = new[]
            {
        "movement_detected", "battery_low", "offline", "online", "emergency", "distress",
        "active_shooter", "emergency_911", "emergency_988", "geofence_breach"
    };

            var titles = new[]
            {
        "Movement Detected", "Battery Low", "Device Offline", "Device Online",
        "Emergency Alert", "Distress Signal", "Active Shooter Alert",
        "Emergency 911", "Emergency 988", "Geofence Breach"
    };

            var bodies = new[]
            {
        "Unexpected movement detected on safety card.",
        "Battery level critically low — please charge.",
        "Safety card has gone offline.",
        "Safety card is now back online.",
        "Critical emergency — immediate assistance needed.",
        "User activated distress signal.",
        "Active shooter situation reported.",
        "Emergency services requested (911).",
        "Mental health crisis support requested (988).",
        "User has exited designated safe zone."
    };

            int added = 0;
            for (int i = 0; i < 50; i++)
            {
                var category = categories[random.Next(categories.Length)];
                var typeKey = types[random.Next(types.Length)];

                var notification = new Notification
                {
                    OwnerUserId = currentUserId,
                    InitiatorUserId = currentUserId,
                    CategoryId = category.Id,
                    TypeKey = typeKey,
                    Priority = (NotificationPriority)random.Next(0, 4),
                    State = NotificationState.Open,
                    Title = titles[random.Next(titles.Length)],
                    Body = bodies[random.Next(bodies.Length)],
                    Sound = random.Next(0, 4) switch
                    {
                        0 => "Emergency.wav",
                        1 => "Alert.wav",
                        2 => "NotificationSound.mp3",
                        _ => null
                    },
                    ContentAvailable = true,
                    DeviceId = deviceIds.Any() ? deviceIds[random.Next(deviceIds.Count)] : null,
                    BatteryLevel = (short)random.Next(5, 101),
                    SignalStrength = (short)random.Next(-100, -30),
                    SignalQuality = new[] { "POOR", "FAIR", "GOOD", "EXCELLENT" }[random.Next(4)],
                    Location = JsonSerializer.Serialize(new
                    {
                        lat = 24.7136 + (random.NextDouble() - 0.5) * 0.2,
                        lng = 46.6753 + (random.NextDouble() - 0.5) * 0.2
                    }),
                    RawEvent = JsonSerializer.Serialize(new { test = true, index = i }),
                    EventTimestamp = DateTime.UtcNow.AddHours(-random.Next(1, 720))
                };

                // Avoid duplicates
                if (!await _db.Notifications.AnyAsync(n =>
                    n.Title == notification.Title &&
                    n.Body == notification.Body &&
                    n.EventTimestamp == notification.EventTimestamp))
                {
                    _db.Notifications.Add(notification);
                    added++;
                }
            }

            if (added > 0)
            {
                await _db.SaveChangesAsync();

                var newNotifications = await _db.Notifications
                    .Where(n => n.OwnerUserId == currentUserId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(added)
                    .ToListAsync();

                foreach (var n in newNotifications)
                {
                    var delivery = new NotificationRecipient
                    {
                        NotificationId = n.Id,
                        RecipientUserId = currentUserId,
                        OriginDisplay = JsonSerializer.Serialize(new
                        {
                            ownerName = "Test User",
                            initiatorName = "System"
                        })
                    };
                    _db.NotificationRecipients.Add(delivery);
                }

                await _db.SaveChangesAsync();
            }

            return Ok(new
            {
                message = $"Seeded {added} sample notifications for current user",
                userId = currentUserId,
                note = "Use GET /notifications to view them. All notifications belong to the logged-in user."
            });
        }

        [HttpPost("test-gateway-filter")]
        public async Task<IActionResult> TestGatewayFilterWithAcknowledgement([FromBody] TestGatewayFilterRequest request)
        {
            if (!AllowAll) return Forbid();

            // Hard-coded configuration values
            const int relationshipRule = 1;
            const int duplicateRule = 1;
            const int duplicateTime = 10;
            
            var cleanMac = request.Mac.Replace(":", "").Replace("-", "").ToUpperInvariant();
            var allFlowAttempts = new List<object>();

            try
            {
                switch (request.Type)
                {
                    case DeviceTypeEnum.Gateway:
                        {
                            // Gateway registration - find all beacons for this gateway's user
                            var gatewayDevice = await _db.Devices
                                .Include(d => d.DeviceType)
                                .FirstOrDefaultAsync(d => d.MacAddress == cleanMac && d.DeviceType.Key == "gateway" && d.IsDeleted==false);

                            if (gatewayDevice == null)
                            {
                                return Ok(new
                                {
                                    success = false,
                                    message = $"Gateway with MAC {cleanMac} not found in database"
                                });
                            }

                            // Find all beacons for this user
                            var beaconDevices = await _db.Devices
                                .Include(d => d.DeviceType)
                                .Where(d => d.UserId == gatewayDevice.UserId && d.DeviceType.Key == "beacon" && d.IsDeleted==false)
                                .ToListAsync();

                            var beaconMacs = beaconDevices
                                .Select(b => b.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant())
                                .ToArray();

                            // Execute gateway registration flow (Steps 1, 2, 3)
                            var result = await ExecuteGatewayFlow(
                                gatewayMac: cleanMac,
                                beaconMacs: beaconMacs,
                                relationshipRule: relationshipRule,
                                duplicateRule: duplicateRule,
                                duplicateTime: duplicateTime,
                                allFlowAttempts: allFlowAttempts
                            );

                            return result;
                        }

                    case DeviceTypeEnum.Beacon:
                        {
                            // Beacon addition - find all gateways for this beacon's user
                            var beaconDevice = await _db.Devices
                                .Include(d => d.DeviceType)
                                .FirstOrDefaultAsync(d => d.MacAddress == cleanMac && d.DeviceType.Key == "beacon" && d.IsDeleted == false);

                            if (beaconDevice == null)
                            {
                                return Ok(new
                                {
                                    success = false,
                                    message = $"Beacon with MAC {cleanMac} not found in database"
                                });
                            }

                            // Find all gateways for this user
                            var gatewayDevices = await _db.Devices
                                .Include(d => d.DeviceType)
                                .Where(d => d.UserId == beaconDevice.UserId && d.DeviceType.Key == "gateway" && d.IsDeleted == false)
                                .ToListAsync();

                            if (gatewayDevices.Count == 0)
                            {
                                return Ok(new
                                {
                                    success = false,
                                    message = "No gateways found for this beacon"
                                });
                            }

                            var gatewayResults = new List<object>();

                            // For each gateway, execute Step 3 with all beacons for this user
                            foreach (var gateway in gatewayDevices)
                            {
                                var gatewayMac = gateway.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();

                                // Find all beacons for this user (same as beaconDevice.UserId)
                                var beaconsForUser = await _db.Devices
                                    .Include(d => d.DeviceType)
                                    .Where(d => d.UserId == beaconDevice.UserId && d.DeviceType.Key == "beacon" && d.IsDeleted==false)
                                    .ToListAsync();

                                var beaconMacs = beaconsForUser
                                    .Select(b => b.MacAddress.Replace(":", "").Replace("-", "").ToUpperInvariant())
                                    .ToArray();

                                // Execute only Step 3 for this gateway
                                var result = await ExecuteBeaconAddFlow(
                                    gatewayMac: gatewayMac,
                                    beaconMacs: beaconMacs,
                                    allFlowAttempts: allFlowAttempts
                                );

                                gatewayResults.Add(new
                                {
                                    gateway = gatewayMac,
                                    result
                                });
                            }

                            return Ok(new
                            {
                                success = true,
                                message = $"Beacon added to {gatewayDevices.Count} gateway(s)",
                                beaconMac = cleanMac,
                                gatewayResults
                            });
                        }

                    case DeviceTypeEnum.SafetyCard:
                        // Safety card handling - to be implemented
                        return Ok(new
                        {
                            success = false,
                            message = "SafetyCard handling is not yet implemented"
                        });

                    default:
                        return BadRequest(new
                        {
                            success = false,
                            message = "Invalid device type specified"
                        });
                }
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    stack = ex.StackTrace,
                    flowAttempts = allFlowAttempts
                });
            }
        }

        private async Task<IActionResult> ExecuteGatewayFlow(
            string gatewayMac,
            string[] beaconMacs,
            int relationshipRule,
            int duplicateRule,
            int duplicateTime,
            List<object> allFlowAttempts)
        {
            var deviceId = $"test{gatewayMac}";
            var publishTopic = $"MINI-02-58B6/{deviceId}/app_to_device";
            var subscribeTopic = $"MINI-02-58B6/test567/device_to_app";

            var steps = new List<object>();
            var receivedMessages = new List<object>();
            IMqttClient? mqttClient = null;
            
            const int maxFlowRetries = 2;

            try
            {
                for (int flowAttempt = 1; flowAttempt <= maxFlowRetries; flowAttempt++)
                {
                    steps = new List<object>();
                    receivedMessages = new List<object>();
                    
                    try
                    {
                        var factory = new MqttFactory();
                        mqttClient?.Dispose();
                        mqttClient = factory.CreateMqttClient();

                        mqttClient.ApplicationMessageReceivedAsync += e =>
                        {
                            var topic = e.ApplicationMessage.Topic;
                            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

                            try
                            {
                                var json = JsonSerializer.Deserialize<JsonDocument>(payload);
                                var msgId = json?.RootElement.GetProperty("msg_id").GetInt32();

                                receivedMessages.Add(new
                                {
                                    topic,
                                    msgId,
                                    payload,
                                    timestamp = DateTime.UtcNow
                                });
                            }
                            catch
                            {
                                receivedMessages.Add(new { topic, payload, error = "Failed to parse msg_id" });
                            }

                            return Task.CompletedTask;
                        };

                        steps.Add(new 
                        { 
                            step = "flow_attempt", 
                            attempt = flowAttempt,
                            maxAttempts = maxFlowRetries,
                            message = $"Starting gateway registration flow attempt {flowAttempt}/{maxFlowRetries}"
                        });

                        var clientOptions = new MqttClientOptionsBuilder()
                            .WithTcpServer(_mqttConfig.Host, _mqttConfig.Port)
                            .WithCredentials(_mqttConfig.Username, _mqttConfig.Password)
                            .WithClientId($"api-test-{Guid.NewGuid()}")
                            .WithTlsOptions(o =>
                            {
                                o.UseTls(_mqttConfig.UseTls);
                                o.WithCertificateValidationHandler(_ => true);
                            })
                            .Build();

                        await mqttClient.ConnectAsync(clientOptions, CancellationToken.None);
                        steps.Add(new { step = "connect", success = true, message = $"Connected to {_mqttConfig.Host}:{_mqttConfig.Port}" });

                        await Task.Delay(1000);
                        steps.Add(new { step = "wait_after_connect", success = true });

                        await mqttClient.SubscribeAsync(subscribeTopic);
                        steps.Add(new { step = "subscribe", success = true, topic = subscribeTopic });

                        async Task<object> PublishAndWaitForAck(int msgId, object payload, string stepName, int maxRetries = 3, int timeoutSeconds = 3)
                        {
                            for (int attempt = 1; attempt <= maxRetries; attempt++)
                            {
                                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                                {
                                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                                });

                                var message = new MqttApplicationMessageBuilder()
                                    .WithTopic(publishTopic)
                                    .WithPayload(json)
                                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                                    .Build();

                                var startTime = DateTime.UtcNow;
                                await mqttClient.PublishAsync(message, CancellationToken.None);

                                var timeout = TimeSpan.FromSeconds(timeoutSeconds);
                                while ((DateTime.UtcNow - startTime) < timeout)
                                {
                                    var ack = receivedMessages.FirstOrDefault(m =>
                                    {
                                        var msgIdProp = m.GetType().GetProperty("msgId");
                                        return msgIdProp != null && (int?)msgIdProp.GetValue(m) == msgId;
                                    });

                                    if (ack != null)
                                    {
                                        return new
                                        {
                                            step = stepName,
                                            msgId,
                                            success = true,
                                            published = true,
                                            acknowledged = true,
                                            attempt,
                                            totalAttempts = maxRetries,
                                            topic = publishTopic,
                                            payload = json,
                                            response = ack,
                                            message = $"msg_id={msgId} published and acknowledged on attempt {attempt}/{maxRetries}"
                                        };
                                    }

                                    await Task.Delay(50);
                                }

                                if (attempt < maxRetries)
                                {
                                    steps.Add(new
                                    {
                                        step = $"{stepName}_retry",
                                        attempt,
                                        message = $"Attempt {attempt}/{maxRetries} timed out, retrying..."
                                    });
                                    await Task.Delay(500);
                                }
                            }

                            return new
                            {
                                step = stepName,
                                msgId,
                                success = false,
                                published = true,
                                acknowledged = false,
                                attempt = maxRetries,
                                totalAttempts = maxRetries,
                                topic = publishTopic,
                                message = $"msg_id={msgId} published but no acknowledgement received after {maxRetries} attempts (timeout: {timeoutSeconds}s each)"
                            };
                        }

                        // Step 1: Relationship Filter
                        var step1Payload = new
                        {
                            msg_id = 1025,
                            device_info = new { device_id = deviceId, mac = gatewayMac },
                            data = new { rule = relationshipRule }
                        };
                        var step1Result = await PublishAndWaitForAck(1025, step1Payload, "1 - Relationship Filter");
                        steps.Add(step1Result);

                        var step1Success = (bool)(step1Result.GetType().GetProperty("acknowledged")?.GetValue(step1Result) ?? false);
                        if (!step1Success)
                        {
                            await mqttClient.DisconnectAsync();
                            steps.Add(new { step = "disconnect_on_failure", reason = "Step 1 failed" });
                            allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages });
                            
                            if (flowAttempt < maxFlowRetries)
                            {
                                await Task.Delay(1000);
                                continue;
                            }
                            
                            return Ok(new
                            {
                                success = false,
                                message = $"Step 1 (msg_id=1025) failed after {maxFlowRetries} flow attempts",
                                gatewayMac,
                                deviceId,
                                publishTopic,
                                subscribeTopic,
                                flowAttempts = allFlowAttempts
                            });
                        }

                        await Task.Delay(500);

                        // Step 2: Duplicate Filter
                        var step2Payload = new
                        {
                            msg_id = 1010,
                            device_info = new { device_id = deviceId, mac = gatewayMac },
                            data = new { rule = duplicateRule, time = duplicateTime }
                        };
                        var step2Result = await PublishAndWaitForAck(1010, step2Payload, "2 - Duplicate Filter");
                        steps.Add(step2Result);

                        var step2Success = (bool)(step2Result.GetType().GetProperty("acknowledged")?.GetValue(step2Result) ?? false);
                        if (!step2Success)
                        {
                            await mqttClient.DisconnectAsync();
                            steps.Add(new { step = "disconnect_on_failure", reason = "Step 2 failed" });
                            allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages });
                            
                            if (flowAttempt < maxFlowRetries)
                            {
                                await Task.Delay(1000);
                                continue;
                            }
                            
                            return Ok(new
                            {
                                success = false,
                                message = $"Step 2 (msg_id=1010) failed after {maxFlowRetries} flow attempts",
                                gatewayMac,
                                deviceId,
                                publishTopic,
                                subscribeTopic,
                                flowAttempts = allFlowAttempts
                            });
                        }

                        await Task.Delay(500);

                        // Step 3: MAC Filter
                        object step3Data;
                        if (beaconMacs.Length == 0)
                        {
                            // No beacons - send single zeros MAC
                            step3Data = new
                            {
                                precise = 0,
                                reverse = 0,
                                array_num = 1,
                                rule = new[] { "000000000000" }
                            };
                        }
                        else
                        {
                            // Has beacons - send beacon MACs
                            step3Data = new
                            {
                                precise = 0,
                                reverse = 0,
                                array_num = beaconMacs.Length,
                                rule = beaconMacs
                            };
                        }

                        var step3Payload = new
                        {
                            msg_id = 1028,
                            device_info = new { device_id = deviceId, mac = gatewayMac },
                            data = step3Data
                        };
                        var step3Result = await PublishAndWaitForAck(1028, step3Payload, "3 - MAC Filter");
                        steps.Add(step3Result);

                        var step3Success = (bool)(step3Result.GetType().GetProperty("acknowledged")?.GetValue(step3Result) ?? false);
                        if (!step3Success)
                        {
                            await mqttClient.DisconnectAsync();
                            steps.Add(new { step = "disconnect_on_failure", reason = "Step 3 failed" });
                            allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages });
                            
                            if (flowAttempt < maxFlowRetries)
                            {
                                await Task.Delay(1000);
                                continue;
                            }
                            
                            return Ok(new
                            {
                                success = false,
                                message = $"Step 3 (msg_id=1028) failed after {maxFlowRetries} flow attempts",
                                gatewayMac,
                                deviceId,
                                publishTopic,
                                subscribeTopic,
                                flowAttempts = allFlowAttempts
                            });
                        }

                        await mqttClient.DisconnectAsync();
                        steps.Add(new { step = "disconnect", success = true });

                        allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages });

                        return Ok(new
                        {
                            success = true,
                            message = "✅ Gateway registered successfully! All 3 steps completed with acknowledgements.",
                            gatewayMac,
                            deviceId,
                            beaconCount = beaconMacs.Length,
                            beaconMacs = beaconMacs,
                            publishTopic,
                            subscribeTopic,
                            flowAttempt,
                            steps,
                            receivedMessages,
                            allFlowAttempts
                        });
                    }
                    catch (Exception innerEx)
                    {
                        steps.Add(new { step = "error_in_flow", error = innerEx.Message });
                        allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages, error = innerEx.Message });
                        
                        if (mqttClient != null)
                        {
                            try { await mqttClient.DisconnectAsync(); } catch { }
                        }
                        
                        if (flowAttempt < maxFlowRetries)
                        {
                            await Task.Delay(1000);
                            continue;
                        }
                        
                        throw;
                    }
                }
                
                return Ok(new
                {
                    success = false,
                    message = "Unexpected end of flow",
                    flowAttempts = allFlowAttempts
                });
            }
            finally
            {
                if (mqttClient != null)
                {
                    mqttClient.Dispose();
                }
            }
        }

        private async Task<object> ExecuteBeaconAddFlow(
            string gatewayMac,
            string[] beaconMacs,
            List<object> allFlowAttempts)
        {
            var deviceId = $"test{gatewayMac}";
            var publishTopic = $"MINI-02-58B6/{deviceId}/app_to_device";
            var subscribeTopic = $"MINI-02-58B6/test567/device_to_app";

            var steps = new List<object>();
            var receivedMessages = new List<object>();
            IMqttClient? mqttClient = null;
            
            const int maxFlowRetries = 2;

            try
            {
                for (int flowAttempt = 1; flowAttempt <= maxFlowRetries; flowAttempt++)
                {
                    steps = new List<object>();
                    receivedMessages = new List<object>();
                    
                    try
                    {
                        var factory = new MqttFactory();
                        mqttClient?.Dispose();
                        mqttClient = factory.CreateMqttClient();

                        mqttClient.ApplicationMessageReceivedAsync += e =>
                        {
                            var topic = e.ApplicationMessage.Topic;
                            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

                            try
                            {
                                var json = JsonSerializer.Deserialize<JsonDocument>(payload);
                                var msgId = json?.RootElement.GetProperty("msg_id").GetInt32();

                                receivedMessages.Add(new
                                {
                                    topic,
                                    msgId,
                                    payload,
                                    timestamp = DateTime.UtcNow
                                });
                            }
                            catch
                            {
                                receivedMessages.Add(new { topic, payload, error = "Failed to parse msg_id" });
                            }

                            return Task.CompletedTask;
                        };

                        steps.Add(new 
                        { 
                            step = "flow_attempt", 
                            attempt = flowAttempt,
                            maxAttempts = maxFlowRetries,
                            message = $"Starting beacon add flow attempt {flowAttempt}/{maxFlowRetries}"
                        });

                        var clientOptions = new MqttClientOptionsBuilder()
                            .WithTcpServer(_mqttConfig.Host, _mqttConfig.Port)
                            .WithCredentials(_mqttConfig.Username, _mqttConfig.Password)
                            .WithClientId($"api-test-{Guid.NewGuid()}")
                            .WithTlsOptions(o =>
                            {
                                o.UseTls(_mqttConfig.UseTls);
                                o.WithCertificateValidationHandler(_ => true);
                            })
                            .Build();

                        await mqttClient.ConnectAsync(clientOptions, CancellationToken.None);
                        steps.Add(new { step = "connect", success = true, message = $"Connected to {_mqttConfig.Host}:{_mqttConfig.Port}" });

                        await Task.Delay(1000);

                        await mqttClient.SubscribeAsync(subscribeTopic);
                        steps.Add(new { step = "subscribe", success = true, topic = subscribeTopic });

                        async Task<object> PublishAndWaitForAck(int msgId, object payload, string stepName, int maxRetries = 3, int timeoutSeconds = 3)
                        {
                            for (int attempt = 1; attempt <= maxRetries; attempt++)
                            {
                                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                                {
                                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                                });

                                var message = new MqttApplicationMessageBuilder()
                                    .WithTopic(publishTopic)
                                    .WithPayload(json)
                                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                                    .Build();

                                var startTime = DateTime.UtcNow;
                                await mqttClient.PublishAsync(message, CancellationToken.None);

                                var timeout = TimeSpan.FromSeconds(timeoutSeconds);
                                while ((DateTime.UtcNow - startTime) < timeout)
                                {
                                    var ack = receivedMessages.FirstOrDefault(m =>
                                    {
                                        var msgIdProp = m.GetType().GetProperty("msgId");
                                        return msgIdProp != null && (int?)msgIdProp.GetValue(m) == msgId;
                                    });

                                    if (ack != null)
                                    {
                                        return new
                                        {
                                            step = stepName,
                                            msgId,
                                            success = true,
                                            published = true,
                                            acknowledged = true,
                                            attempt,
                                            totalAttempts = maxRetries,
                                            topic = publishTopic,
                                            payload = json,
                                            response = ack,
                                            message = $"msg_id={msgId} published and acknowledged on attempt {attempt}/{maxRetries}"
                                        };
                                    }

                                    await Task.Delay(50);
                                }

                                if (attempt < maxRetries)
                                {
                                    steps.Add(new
                                    {
                                        step = $"{stepName}_retry",
                                        attempt,
                                        message = $"Attempt {attempt}/{maxRetries} timed out, retrying..."
                                    });
                                    await Task.Delay(500);
                                }
                            }

                            return new
                            {
                                step = stepName,
                                msgId,
                                success = false,
                                published = true,
                                acknowledged = false,
                                attempt = maxRetries,
                                totalAttempts = maxRetries,
                                topic = publishTopic,
                                message = $"msg_id={msgId} published but no acknowledgement received after {maxRetries} attempts (timeout: {timeoutSeconds}s each)"
                            };
                        }

                        // Only Step 3: MAC Filter
                        object step3Data;
                        if (beaconMacs.Length == 0)
                        {
                            // No beacons - send single zeros MAC
                            step3Data = new
                            {
                                precise = 0,
                                reverse = 0,
                                array_num = 1,
                                rule = new[] { "000000000000" }
                            };
                        }
                        else
                        {
                            // Has beacons - send beacon MACs
                            step3Data = new
                            {
                                precise = 0,
                                reverse = 0,
                                array_num = beaconMacs.Length,
                                rule = beaconMacs
                            };
                        }

                        var step3Payload = new
                        {
                            msg_id = 1028,
                            device_info = new { device_id = deviceId, mac = gatewayMac },
                            data = step3Data
                        };
                        var step3Result = await PublishAndWaitForAck(1028, step3Payload, "3 - MAC Filter");
                        steps.Add(step3Result);

                        var step3Success = (bool)(step3Result.GetType().GetProperty("acknowledged")?.GetValue(step3Result) ?? false);
                        if (!step3Success)
                        {
                            await mqttClient.DisconnectAsync();
                            steps.Add(new { step = "disconnect_on_failure", reason = "Step 3 failed" });
                            allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages });
                            
                            if (flowAttempt < maxFlowRetries)
                            {
                                await Task.Delay(1000);
                                continue;
                            }
                            
                            return new
                            {
                                success = false,
                                message = $"Step 3 (msg_id=1028) failed after {maxFlowRetries} flow attempts",
                                gatewayMac,
                                deviceId,
                                publishTopic,
                                subscribeTopic,
                                flowAttempts = allFlowAttempts
                            };
                        }

                        await mqttClient.DisconnectAsync();
                        steps.Add(new { step = "disconnect", success = true });

                        allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages });

                        return new
                        {
                            success = true,
                            message = "✅ Beacon added successfully! Step 3 (MAC Filter) completed with acknowledgement.",
                            gatewayMac,
                            deviceId,
                            beaconCount = beaconMacs.Length,
                            beaconMacs = beaconMacs,
                            publishTopic,
                            subscribeTopic,
                            flowAttempt,
                            steps,
                            receivedMessages
                        };
                    }
                    catch (Exception innerEx)
                    {
                        steps.Add(new { step = "error_in_flow", error = innerEx.Message });
                        allFlowAttempts.Add(new { attempt = flowAttempt, steps, receivedMessages, error = innerEx.Message });
                        
                        if (mqttClient != null)
                        {
                            try { await mqttClient.DisconnectAsync(); } catch { }
                        }
                        
                        if (flowAttempt < maxFlowRetries)
                        {
                            await Task.Delay(1000);
                            continue;
                        }
                        
                        throw;
                    }
                }
                
                return new
                {
                    success = false,
                    message = "Unexpected end of flow",
                    flowAttempts = allFlowAttempts
                };
            }
            finally
            {
                if (mqttClient != null)
                {
                    mqttClient.Dispose();
                }
            }
        }
    }

    public enum DeviceTypeEnum
    {
        Gateway = 1,
        Beacon = 2,
        SafetyCard = 3
    }

    public class TestGatewayFilterRequest
    {
        public string Mac { get; set; } = string.Empty;
        public DeviceTypeEnum Type { get; set; }
    }

    public class DeleteUserRequest
    {
        public string Email { get; set; } = "";
    }
}