using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;
using OcufiiAPI.Extensions;
using OcufiiAPI.Models;
using System.Security.Cryptography;
using System.Text.Json;

namespace OcufiiAPI.Controllers;

[ApiController]
[Route("devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly OcufiiDbContext _db;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(OcufiiDbContext db, ILogger<DevicesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse>> ListDevices(
        [FromQuery] string? type,
        [FromQuery] bool? isEnabled,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 50;

        var query = _db.Devices
            .Include(d => d.DeviceType)
            .Where(d => !d.IsDeleted)
            .AsNoTracking();

        var currentUserId = User.GetUserId();
        var isAdmin = User.IsInRole("admin");

        if (!isAdmin)
        {
            query = query.Where(d => d.UserId == currentUserId || d.UserId == null);
        }

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(d => d.DeviceType.Key == type.ToLowerInvariant());

        if (isEnabled.HasValue)
            query = query.Where(d => d.IsEnabled == isEnabled.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLowerInvariant();
            query = query.Where(d =>
                (d.Name != null && d.Name.ToLower().Contains(s)) ||
                d.MacAddress.ToLower().Contains(s));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                type = d.DeviceType.Key,
                d.MacAddress,
                d.Name,
                d.Location,
                d.IsEnabled,
                d.IsDeleted,
                Attributes = d.Attributes,
                d.CreatedAt,
                d.UpdatedAt
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "Devices retrieved successfully")
        {
            Data = new { items, total, page, pageSize }
        });
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse>> CreateDevice([FromBody] JsonElement body)
    {
        if (!body.TryGetProperty("type", out var typeProp) || string.IsNullOrWhiteSpace(typeProp.GetString()))
            return BadRequest(new ApiResponse(false, "Missing or invalid 'type'"));

        var typeKey = typeProp.GetString()!.Trim().ToLowerInvariant();
        var deviceType = await _db.DeviceTypes.FirstOrDefaultAsync(dt => dt.Key == typeKey);
        if (deviceType == null)
            return BadRequest(new ApiResponse(false, "Invalid device type"));

        if (!body.TryGetProperty("macAddress", out var macProp) || string.IsNullOrWhiteSpace(macProp.GetString()))
            return BadRequest(new ApiResponse(false, "Missing or invalid 'macAddress'"));

        var macAddress = macProp.GetString()!.Trim();
        if (await _db.Devices.AnyAsync(d => d.MacAddress == macAddress))
            return Conflict(new ApiResponse(false, "MacAddress already exists"));

        var currentUserId = User.GetUserId();
        var tenantIdClaim = User.FindFirst("tenant_id")?.Value
                             ?? Guid.Parse("00000000-0000-0000-0000-000000000001").ToString();

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceTypeId = deviceType.Id,
            MacAddress = macAddress,
            Name = body.TryGetProperty("name", out var n) ? n.GetString()?.Trim() : null,
            Location = body.TryGetProperty("location", out var l) ? l.GetString()?.Trim() : null,
            Information = body.TryGetProperty("information", out var i) ? i.GetString()?.Trim() : null,
            Attributes = body.TryGetProperty("attributes", out var a) ? a.ToString() : "{}",
            UserId = currentUserId,  
            TenantId = Guid.Parse(tenantIdClaim),  
            IsEnabled = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        return Created($"/devices/{device.Id}", new ApiResponse(true, "Device created successfully")
        {
            Data = new { deviceId = device.Id }
        });
    }

    [HttpGet("{deviceId:guid}")]
    public async Task<ActionResult<ApiResponse>> GetDevice(Guid deviceId)
    {
        var device = await _db.Devices
            .Include(d => d.DeviceType)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsDeleted);

        if (device == null)
            return NotFound(new ApiResponse(false, "Device not found"));

        return Ok(new ApiResponse(true, "Device retrieved successfully")
        {
            Data = new
            {
                device.Id,
                type = device.DeviceType.Key,
                device.MacAddress,
                device.Name,
                device.Location,
                device.IsEnabled,
                device.IsDeleted,
                Attributes = device.Attributes,
                device.CreatedAt,
                device.UpdatedAt
            }
        });
    }

    [HttpPatch("{deviceId:guid}")]
    public async Task<ActionResult<ApiResponse>> UpdateDevice(Guid deviceId, [FromBody] JsonElement payload)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsDeleted);
        if (device == null)
            return NotFound(new ApiResponse(false, "Device not found"));

        if (payload.ValueKind != JsonValueKind.Object)
            return BadRequest(new ApiResponse(false, "Invalid JSON payload"));

        foreach (var prop in payload.EnumerateObject())
        {
            switch (prop.Name.ToLowerInvariant())
            {
                case "name": device.Name = prop.Value.GetString()?.Trim(); break;
                case "location": device.Location = prop.Value.GetString()?.Trim(); break;
                case "isenabled":
                    if (prop.Value.ValueKind == JsonValueKind.True) device.IsEnabled = true;
                    else if (prop.Value.ValueKind == JsonValueKind.False) device.IsEnabled = false;
                    break;
                case "attributes": device.Attributes = prop.Value.ToString(); break;
            }
        }

        device.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Device updated successfully"));
    }

    [HttpDelete("{deviceId:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteDevice(Guid deviceId)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsDeleted);
        if (device == null)
            return NotFound(new ApiResponse(false, "Device not found"));

        device.IsDeleted = true;
        device.IsEnabled = false;
        device.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Device deleted successfully"));
    }

    [HttpPost("{deviceId:guid}/credentials")]
    public async Task<ActionResult<ApiResponse>> IssueCredentials(Guid deviceId, [FromBody] IssueCredentialsRequest? request)
    {
        request ??= new IssueCredentialsRequest();

        var device = await _db.Devices
            .Include(d => d.DeviceType)
            .FirstOrDefaultAsync(d => d.Id == deviceId && !d.IsDeleted);

        if (device == null)
            return NotFound(new ApiResponse(false, "Device not found"));
        if (!device.DeviceType.ConnectsToMqtt)
            return BadRequest(new ApiResponse(false, "This device type does not support MQTT"));

        var existing = await _db.DeviceCredentials.FirstOrDefaultAsync(c => c.DeviceId == deviceId);

        if (existing != null && !request.Regenerate)
            return Conflict(new ApiResponse(false, "Credentials already exist. Use regenerate=true"));

        if (existing != null) _db.DeviceCredentials.Remove(existing);

        var username = $"{device.DeviceType.Key}_{device.MacAddress.Replace(":", "").ToLowerInvariant()}";
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var cred = new DeviceCredential
        {
            DeviceId = device.Id,
            MqttUsername = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsEnabled = true,
            LastRotatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.DeviceCredentials.Add(cred);
        await _db.SaveChangesAsync();

        return Created("", new ApiResponse(true, "Credentials issued successfully")
        {
            Data = new { mqttUsername = username, mqttPassword = password }
        });
    }

    [HttpGet("{deviceId:guid}/credentials")]
    //[Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse>> GetCredentialsMetadata(Guid deviceId)
    {
        var cred = await _db.DeviceCredentials.FirstOrDefaultAsync(c => c.DeviceId == deviceId);
        if (cred == null)
            return NotFound(new ApiResponse(false, "Credentials not found"));

        return Ok(new ApiResponse(true, "Credentials retrieved")
        {
            Data = new
            {
                cred.MqttUsername,
                cred.IsEnabled,
                cred.LastRotatedAt
            }
        });
    }

    [HttpDelete("{deviceId:guid}/credentials")]
    public async Task<ActionResult<ApiResponse>> RevokeCredentials(Guid deviceId)
    {
        var cred = await _db.DeviceCredentials.FirstOrDefaultAsync(c => c.DeviceId == deviceId);
        if (cred == null)
            return NotFound(new ApiResponse(false, "Credentials not found"));

        _db.DeviceCredentials.Remove(cred);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Credentials revoked successfully"));
    }

    [HttpPost("{deviceId:guid}/verify-credentials")]
    public async Task<ActionResult<ApiResponse>> VerifyCredentials(Guid deviceId, [FromBody] VerifyCredentialsRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.MqttUsername) || string.IsNullOrEmpty(request.MqttPassword))
            return BadRequest(new ApiResponse(false, "MqttUsername and MqttPassword are required"));

        var cred = await _db.DeviceCredentials
            .FirstOrDefaultAsync(c => c.DeviceId == deviceId && c.IsEnabled);

        if (cred == null)
            return NotFound(new ApiResponse(false, "Credentials not found or disabled"));

        // Compare username first
        if (!string.Equals(cred.MqttUsername, request.MqttUsername, StringComparison.OrdinalIgnoreCase))
            return Ok(new ApiResponse(true, "Verification result")
            {
                Data = new { isValid = false }
            });

        // Verify password using BCrypt
        bool isValid = BCrypt.Net.BCrypt.Verify(request.MqttPassword, cred.PasswordHash);

        return Ok(new ApiResponse(true, "Verification result")
        {
            Data = new { isValid }
        });
    }

    [HttpGet("{deviceId:guid}/telemetry")]
    public async Task<ActionResult<ApiResponse>> GetTelemetry(Guid deviceId, [FromQuery] int limit = 100, [FromQuery] DateTime? since = null)
    {
        if (limit < 1 || limit > 1000) limit = 100;

        var query = _db.DeviceTelemetry.Where(t => t.DeviceId == deviceId);
        if (since.HasValue) query = query.Where(t => t.ReceivedAt >= since.Value);

        var items = await query
            .OrderByDescending(t => t.ReceivedAt)
            .Take(limit)
            .Select(t => new
            {
                t.Id,
                sourceType = t.SourceType.ToString().ToLowerInvariant(),
                t.ViaDeviceId,
                t.BatteryLevel,
                t.SignalStrength,
                t.SignalQuality,
                t.Payload,
                t.ReceivedAt,
                t.DeviceTimestamp
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "Telemetry retrieved successfully")
        {
            Data = new { items }
        });
    }
}

public class IssueCredentialsRequest
{
    public bool Regenerate { get; set; } = false;
}